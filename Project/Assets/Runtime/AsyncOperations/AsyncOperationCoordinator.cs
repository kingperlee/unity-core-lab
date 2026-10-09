using System;
using System.Threading;
using System.Threading.Tasks;

namespace UnityCoreLab.AsyncOperations
{
    /// <summary>
    /// Runs one logical operation at a time and prevents stale completions from overwriting
    /// the state of a newer operation.
    /// </summary>
    public sealed class AsyncOperationCoordinator : IDisposable
    {
        private readonly object _gate = new object();

        private CancellationTokenSource _activeCancellation;
        private int _version;
        private bool _disposed;

        public event Action<OperationSnapshot> StateChanged;

        public int CurrentVersion => Volatile.Read(ref _version);

        public OperationSnapshot Current { get; private set; } =
            new OperationSnapshot(0, OperationState.Idle, TimeSpan.Zero);

        public async Task<OperationResult> RunAsync(
            IAsyncOperation operation,
            TimeSpan timeout,
            CancellationToken cancellationToken = default)
        {
            if (operation == null)
            {
                throw new ArgumentNullException(nameof(operation));
            }

            ThrowIfDisposed();

            int version = Interlocked.Increment(ref _version);
            var startedAt = DateTimeOffset.UtcNow;
            var timeoutCancellation = new CancellationTokenSource();
            var linkedCancellation = CancellationTokenSource.CreateLinkedTokenSource(
                cancellationToken,
                timeoutCancellation.Token);

            if (timeout > TimeSpan.Zero && timeout != Timeout.InfiniteTimeSpan)
            {
                timeoutCancellation.CancelAfter(timeout);
            }

            CancellationTokenSource previousCancellation;
            lock (_gate)
            {
                ThrowIfDisposed();
                previousCancellation = _activeCancellation;
                _activeCancellation = linkedCancellation;
            }

            previousCancellation?.Cancel();
            PublishIfCurrent(version, OperationState.Running, startedAt);

            OperationResult result;
            try
            {
                var context = new OperationContext(version, startedAt);
                result = await operation.ExecuteAsync(context, linkedCancellation.Token);
            }
            catch (OperationCanceledException) when (linkedCancellation.IsCancellationRequested)
            {
                result = timeoutCancellation.IsCancellationRequested && !cancellationToken.IsCancellationRequested
                    ? OperationResult.TimedOut()
                    : OperationResult.Cancelled();
            }
            catch (Exception exception)
            {
                result = OperationResult.Failed(exception.Message);
            }
            lock (_gate)
            {
                if (ReferenceEquals(_activeCancellation, linkedCancellation))
                {
                    _activeCancellation = null;
                }
            }

            // The linked source registers with the timeout source. Dispose it first so its
            // registration is removed while the timeout source is still alive.
            linkedCancellation.Dispose();
            timeoutCancellation.Dispose();

            if (version != CurrentVersion)
            {
                return OperationResult.Superseded();
            }

            PublishIfCurrent(version, result.State, startedAt, result.Error);
            return result;
        }

        public void CancelActive()
        {
            CancellationTokenSource cancellation;
            lock (_gate)
            {
                cancellation = _activeCancellation;
            }

            cancellation?.Cancel();
        }

        public void Dispose()
        {
            CancellationTokenSource cancellation;
            lock (_gate)
            {
                if (_disposed)
                {
                    return;
                }

                _disposed = true;
                Interlocked.Increment(ref _version);
                cancellation = _activeCancellation;
                _activeCancellation = null;
            }

            cancellation?.Cancel();
        }

        private void PublishIfCurrent(
            int version,
            OperationState state,
            DateTimeOffset startedAt,
            string error = null)
        {
            if (version != CurrentVersion)
            {
                return;
            }

            var snapshot = new OperationSnapshot(
                version,
                state,
                DateTimeOffset.UtcNow - startedAt,
                error);

            Current = snapshot;
            StateChanged?.Invoke(snapshot);
        }

        private void ThrowIfDisposed()
        {
            if (_disposed)
            {
                throw new ObjectDisposedException(nameof(AsyncOperationCoordinator));
            }
        }
    }
}
