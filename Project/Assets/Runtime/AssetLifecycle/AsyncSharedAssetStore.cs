using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace UnityCoreLab.AssetLifecycle
{
    /// <summary>
    /// Coalesces concurrent loads by key while keeping each waiter's cancellation
    /// independent from the underlying shared request.
    /// </summary>
    public sealed class AsyncSharedAssetStore<TAsset> :
        IDisposable,
        IAssetLeaseOwner<TAsset>
        where TAsset : class
    {
        private readonly object _gate = new object();
        private readonly IAsyncAssetProvider<TAsset> _provider;
        private readonly Dictionary<string, Entry> _entries =
            new Dictionary<string, Entry>(StringComparer.Ordinal);
        private readonly CancellationTokenSource _lifetimeCancellation =
            new CancellationTokenSource();

        private int _activeLoadCount;
        private bool _disposed;
        private bool _disposeSignalSent;

        public AsyncSharedAssetStore(IAsyncAssetProvider<TAsset> provider)
        {
            _provider = provider ?? throw new ArgumentNullException(nameof(provider));
        }

        public int LoadedAssetCount
        {
            get
            {
                lock (_gate)
                {
                    int count = 0;
                    foreach (Entry entry in _entries.Values)
                    {
                        if (entry.Asset != null)
                        {
                            count++;
                        }
                    }

                    return count;
                }
            }
        }

        public int LoadingAssetCount
        {
            get
            {
                lock (_gate)
                {
                    return _activeLoadCount;
                }
            }
        }

        public async Task<AssetLease<TAsset>> AcquireAsync(
            string key,
            CancellationToken cancellationToken = default)
        {
            ValidateKey(key);
            cancellationToken.ThrowIfCancellationRequested();

            Entry entry;
            bool startLoad = false;
            lock (_gate)
            {
                ThrowIfDisposed();

                if (!_entries.TryGetValue(key, out entry))
                {
                    entry = new Entry(key);
                    _entries.Add(key, entry);
                    _activeLoadCount++;
                    startLoad = true;
                }

                entry.WaiterCount++;
            }

            if (startLoad)
            {
                _ = LoadEntryAsync(entry);
            }

            TAsset asset;
            try
            {
                asset = await AwaitWithCancellation(
                    entry.Completion.Task,
                    cancellationToken);
                cancellationToken.ThrowIfCancellationRequested();
            }
            catch
            {
                FinishWaiter(entry);
                throw;
            }

            lock (_gate)
            {
                if (_disposed ||
                    !_entries.TryGetValue(key, out Entry current) ||
                    !ReferenceEquals(current, entry) ||
                    !ReferenceEquals(entry.Asset, asset))
                {
                    entry.WaiterCount--;
                    throw new ObjectDisposedException(
                        nameof(AsyncSharedAssetStore<TAsset>),
                        "The shared asset entry ended before ownership was acquired.");
                }

                entry.WaiterCount--;
                entry.ReferenceCount++;
            }

            return new AssetLease<TAsset>(this, key, asset);
        }

        public int GetReferenceCount(string key)
        {
            lock (_gate)
            {
                return !string.IsNullOrWhiteSpace(key) &&
                       _entries.TryGetValue(key, out Entry entry)
                    ? entry.ReferenceCount
                    : 0;
            }
        }

        public int GetWaiterCount(string key)
        {
            lock (_gate)
            {
                return !string.IsNullOrWhiteSpace(key) &&
                       _entries.TryGetValue(key, out Entry entry)
                    ? entry.WaiterCount
                    : 0;
            }
        }

        public void Dispose()
        {
            List<KeyValuePair<string, TAsset>> loadedAssets;
            List<Entry> entries;
            lock (_gate)
            {
                if (_disposed)
                {
                    return;
                }

                _disposed = true;
                loadedAssets = new List<KeyValuePair<string, TAsset>>();
                entries = new List<Entry>(_entries.Values);

                foreach (Entry entry in entries)
                {
                    if (entry.Asset != null)
                    {
                        loadedAssets.Add(
                            new KeyValuePair<string, TAsset>(entry.Key, entry.Asset));
                    }
                }

                _entries.Clear();
            }

            List<Exception> cleanupFailures = null;
            try
            {
                _lifetimeCancellation.Cancel();
            }
            catch (Exception exception)
            {
                AddCleanupFailure(ref cleanupFailures, exception);
            }

            foreach (Entry entry in entries)
            {
                entry.Completion.TrySetCanceled();
            }

            foreach (KeyValuePair<string, TAsset> pair in loadedAssets)
            {
                try
                {
                    _provider.Unload(pair.Key, pair.Value);
                }
                catch (Exception exception)
                {
                    AddCleanupFailure(ref cleanupFailures, exception);
                }
            }

            try
            {
                bool disposeCancellation;
                lock (_gate)
                {
                    _disposeSignalSent = true;
                    disposeCancellation = _activeLoadCount == 0;
                }

                if (disposeCancellation)
                {
                    _lifetimeCancellation.Dispose();
                }
            }
            catch (Exception exception)
            {
                AddCleanupFailure(ref cleanupFailures, exception);
            }

            if (cleanupFailures != null)
            {
                throw new AggregateException(
                    "One or more failures occurred while disposing the async asset store.",
                    cleanupFailures);
            }
        }

        bool IAssetLeaseOwner<TAsset>.IsLeaseAlive(string key, TAsset asset)
        {
            lock (_gate)
            {
                return !_disposed &&
                       _entries.TryGetValue(key, out Entry entry) &&
                       ReferenceEquals(entry.Asset, asset) &&
                       entry.ReferenceCount > 0;
            }
        }

        void IAssetLeaseOwner<TAsset>.Release(string key, TAsset asset)
        {
            TAsset assetToUnload = null;
            lock (_gate)
            {
                if (_disposed)
                {
                    return;
                }

                if (!_entries.TryGetValue(key, out Entry entry) ||
                    !ReferenceEquals(entry.Asset, asset) ||
                    entry.ReferenceCount <= 0)
                {
                    throw new InvalidOperationException(
                        $"The store no longer owns the asset for key '{key}'.");
                }

                entry.ReferenceCount--;
                if (entry.ReferenceCount == 0 && entry.WaiterCount == 0)
                {
                    _entries.Remove(key);
                    assetToUnload = entry.Asset;
                }
            }

            if (assetToUnload != null)
            {
                _provider.Unload(key, assetToUnload);
            }
        }

        private async Task LoadEntryAsync(Entry entry)
        {
            try
            {
                TAsset asset = await _provider.LoadAsync(
                    entry.Key,
                    _lifetimeCancellation.Token);
                if (asset == null)
                {
                    throw new InvalidOperationException(
                        $"The async asset provider returned null for key '{entry.Key}'.");
                }

                bool keepLoaded;
                lock (_gate)
                {
                    keepLoaded = !_disposed &&
                                 _entries.TryGetValue(entry.Key, out Entry current) &&
                                 ReferenceEquals(current, entry) &&
                                 entry.WaiterCount > 0;

                    if (keepLoaded)
                    {
                        entry.Asset = asset;
                    }
                    else if (_entries.TryGetValue(entry.Key, out current) &&
                             ReferenceEquals(current, entry))
                    {
                        _entries.Remove(entry.Key);
                    }
                }

                if (!keepLoaded)
                {
                    _provider.Unload(entry.Key, asset);
                }

                entry.Completion.TrySetResult(asset);
            }
            catch (OperationCanceledException)
                when (_lifetimeCancellation.IsCancellationRequested)
            {
                RemoveIfCurrent(entry);
                entry.Completion.TrySetCanceled();
            }
            catch (Exception exception)
            {
                bool hasWaiters = RemoveIfCurrent(entry);
                if (hasWaiters)
                {
                    entry.Completion.TrySetException(exception);
                }
                else
                {
                    // No caller remains to observe the provider failure. Complete the
                    // shared signal without retaining an unobserved faulted task.
                    entry.Completion.TrySetCanceled();
                }
            }
            finally
            {
                bool disposeCancellation;
                lock (_gate)
                {
                    _activeLoadCount--;
                    disposeCancellation = _disposed &&
                                          _disposeSignalSent &&
                                          _activeLoadCount == 0;
                }

                if (disposeCancellation)
                {
                    _lifetimeCancellation.Dispose();
                }
            }
        }

        private void FinishWaiter(Entry entry)
        {
            TAsset assetToUnload = null;
            lock (_gate)
            {
                if (entry.WaiterCount > 0)
                {
                    entry.WaiterCount--;
                }

                if (!_disposed &&
                    entry.Asset != null &&
                    entry.WaiterCount == 0 &&
                    entry.ReferenceCount == 0 &&
                    _entries.TryGetValue(entry.Key, out Entry current) &&
                    ReferenceEquals(current, entry))
                {
                    _entries.Remove(entry.Key);
                    assetToUnload = entry.Asset;
                }
            }

            if (assetToUnload != null)
            {
                _provider.Unload(entry.Key, assetToUnload);
            }
        }

        private bool RemoveIfCurrent(Entry entry)
        {
            lock (_gate)
            {
                bool hasWaiters = entry.WaiterCount > 0;
                if (_entries.TryGetValue(entry.Key, out Entry current) &&
                    ReferenceEquals(current, entry))
                {
                    _entries.Remove(entry.Key);
                }

                return hasWaiters;
            }
        }

        private static void AddCleanupFailure(
            ref List<Exception> failures,
            Exception exception)
        {
            if (failures == null)
            {
                failures = new List<Exception>();
            }

            failures.Add(exception);
        }

        private static async Task<TAsset> AwaitWithCancellation(
            Task<TAsset> task,
            CancellationToken cancellationToken)
        {
            if (!cancellationToken.CanBeCanceled || task.IsCompleted)
            {
                return await task;
            }

            var cancellationSignal = new TaskCompletionSource<bool>(
                TaskCreationOptions.RunContinuationsAsynchronously);
            using (cancellationToken.Register(
                       () => cancellationSignal.TrySetResult(true)))
            {
                Task completed = await Task.WhenAny(task, cancellationSignal.Task);
                if (!ReferenceEquals(completed, task))
                {
                    throw new OperationCanceledException(cancellationToken);
                }
            }

            return await task;
        }

        private static void ValidateKey(string key)
        {
            if (string.IsNullOrWhiteSpace(key))
            {
                throw new ArgumentException(
                    "An asset key cannot be null, empty, or whitespace.",
                    nameof(key));
            }
        }

        private void ThrowIfDisposed()
        {
            if (_disposed)
            {
                throw new ObjectDisposedException(nameof(AsyncSharedAssetStore<TAsset>));
            }
        }

        private sealed class Entry
        {
            public Entry(string key)
            {
                Key = key;
                Completion = new TaskCompletionSource<TAsset>(
                    TaskCreationOptions.RunContinuationsAsynchronously);
            }

            public string Key { get; }

            public TaskCompletionSource<TAsset> Completion { get; }

            public TAsset Asset { get; set; }

            public int WaiterCount { get; set; }

            public int ReferenceCount { get; set; }
        }
    }
}
