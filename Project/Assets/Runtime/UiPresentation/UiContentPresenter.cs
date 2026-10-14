using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace UnityCoreLab.UiPresentation
{
    /// <summary>
    /// Converts asynchronous source results into deterministic view snapshots.
    /// New refreshes supersede old ones, and disposal suppresses every late result.
    /// </summary>
    public sealed class UiContentPresenter<TItem> : IDisposable
    {
        private readonly object _gate = new object();
        private readonly IUiContentView<TItem> _view;
        private readonly IUiContentSource<TItem> _source;
        private Request _activeRequest;
        private UiContentSnapshot<TItem> _currentSnapshot;
        private long _revision;
        private bool _disposed;

        public UiContentPresenter(
            IUiContentView<TItem> view,
            IUiContentSource<TItem> source)
        {
            _view = view ?? throw new ArgumentNullException(nameof(view));
            _source = source ?? throw new ArgumentNullException(nameof(source));
            _view.RefreshRequested += HandleRefreshRequested;
        }

        public UiContentSnapshot<TItem> CurrentSnapshot
        {
            get
            {
                lock (_gate)
                {
                    return _currentSnapshot;
                }
            }
        }

        public bool IsLoading
        {
            get
            {
                lock (_gate)
                {
                    return _activeRequest != null;
                }
            }
        }

        public Task RefreshAsync()
        {
            Request request;
            Request previous;
            lock (_gate)
            {
                ThrowIfDisposed();
                previous = _activeRequest;
                request = new Request(++_revision);
                _activeRequest = request;
            }

            previous?.Cancel();
            PublishIfCurrent(
                request,
                UiContentSnapshot<TItem>.Loading(request.Revision));
            return LoadAndPresentAsync(request);
        }

        public void Dispose()
        {
            Request request;
            lock (_gate)
            {
                if (_disposed)
                {
                    return;
                }

                _disposed = true;
                request = _activeRequest;
                _activeRequest = null;
            }

            _view.RefreshRequested -= HandleRefreshRequested;
            request?.Cancel();
        }

        private async Task LoadAndPresentAsync(Request request)
        {
            try
            {
                IReadOnlyList<TItem> items = await _source.LoadAsync(request.Token);
                request.Token.ThrowIfCancellationRequested();

                UiContentSnapshot<TItem> snapshot;
                if (items == null)
                {
                    throw new InvalidOperationException(
                        "The UI content source returned null.");
                }

                snapshot = items.Count == 0
                    ? UiContentSnapshot<TItem>.Empty(request.Revision)
                    : UiContentSnapshot<TItem>.Content(request.Revision, items);
                PublishIfCurrent(request, snapshot);
            }
            catch (OperationCanceledException)
                when (request.IsCancellationRequested)
            {
                // Superseded and closed requests are expected control flow.
            }
            catch (Exception exception)
            {
                string message = string.IsNullOrWhiteSpace(exception.Message)
                    ? exception.GetType().Name
                    : exception.Message;
                PublishIfCurrent(
                    request,
                    UiContentSnapshot<TItem>.Error(
                        request.Revision,
                        message));
            }
            finally
            {
                lock (_gate)
                {
                    if (ReferenceEquals(_activeRequest, request))
                    {
                        _activeRequest = null;
                    }
                }

                request.Dispose();
            }
        }

        private bool PublishIfCurrent(
            Request request,
            UiContentSnapshot<TItem> snapshot)
        {
            lock (_gate)
            {
                if (_disposed ||
                    !ReferenceEquals(_activeRequest, request) ||
                    request.IsCancellationRequested)
                {
                    return false;
                }

                _currentSnapshot = snapshot;
            }

            _view.Render(snapshot);
            return true;
        }

        private void HandleRefreshRequested()
        {
            _ = RefreshAsync();
        }

        private void ThrowIfDisposed()
        {
            if (_disposed)
            {
                throw new ObjectDisposedException(nameof(UiContentPresenter<TItem>));
            }
        }

        private sealed class Request : IDisposable
        {
            private readonly object _gate = new object();
            private readonly CancellationTokenSource _cancellation =
                new CancellationTokenSource();
            private bool _disposed;

            public Request(long revision)
            {
                Revision = revision;
            }

            public long Revision { get; }

            public CancellationToken Token
            {
                get
                {
                    lock (_gate)
                    {
                        return _cancellation.Token;
                    }
                }
            }

            public bool IsCancellationRequested
            {
                get
                {
                    lock (_gate)
                    {
                        return _disposed || _cancellation.IsCancellationRequested;
                    }
                }
            }

            public void Cancel()
            {
                lock (_gate)
                {
                    if (!_disposed)
                    {
                        _cancellation.Cancel();
                    }
                }
            }

            public void Dispose()
            {
                lock (_gate)
                {
                    if (_disposed)
                    {
                        return;
                    }

                    _disposed = true;
                    _cancellation.Dispose();
                }
            }
        }
    }
}
