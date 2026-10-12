using System;
using System.Collections.Generic;

namespace UnityCoreLab.AssetLifecycle
{
    /// <summary>
    /// Deduplicates synchronous loads by key and tracks ownership through AssetLease.
    /// This baseline is intentionally single-threaded; async load coalescing and waiter
    /// cancellation are introduced in the next stage.
    /// </summary>
    public sealed class SharedAssetStore<TAsset> :
        IDisposable,
        IAssetLeaseOwner<TAsset>
        where TAsset : class
    {
        private readonly IAssetProvider<TAsset> _provider;
        private readonly Dictionary<string, Entry> _entries =
            new Dictionary<string, Entry>(StringComparer.Ordinal);

        private bool _disposed;

        public SharedAssetStore(IAssetProvider<TAsset> provider)
        {
            _provider = provider ?? throw new ArgumentNullException(nameof(provider));
        }

        public int LoadedAssetCount => _entries.Count;

        public AssetLease<TAsset> Acquire(string key)
        {
            ThrowIfDisposed();
            ValidateKey(key);

            if (_entries.TryGetValue(key, out Entry existing))
            {
                existing.ReferenceCount++;
                return new AssetLease<TAsset>(this, key, existing.Asset);
            }

            TAsset asset = _provider.Load(key);
            if (asset == null)
            {
                throw new InvalidOperationException(
                    $"The asset provider returned null for key '{key}'.");
            }

            _entries.Add(key, new Entry(asset));
            return new AssetLease<TAsset>(this, key, asset);
        }

        public bool IsLoaded(string key)
        {
            if (string.IsNullOrWhiteSpace(key))
            {
                return false;
            }

            return _entries.ContainsKey(key);
        }

        public int GetReferenceCount(string key)
        {
            if (string.IsNullOrWhiteSpace(key) ||
                !_entries.TryGetValue(key, out Entry entry))
            {
                return 0;
            }

            return entry.ReferenceCount;
        }

        public void Dispose()
        {
            if (_disposed)
            {
                return;
            }

            _disposed = true;
            var loadedEntries = new List<KeyValuePair<string, Entry>>(_entries);
            _entries.Clear();

            List<Exception> failures = null;
            foreach (KeyValuePair<string, Entry> pair in loadedEntries)
            {
                try
                {
                    _provider.Unload(pair.Key, pair.Value.Asset);
                }
                catch (Exception exception)
                {
                    if (failures == null)
                    {
                        failures = new List<Exception>();
                    }

                    failures.Add(exception);
                }
            }

            if (failures != null)
            {
                throw new AggregateException(
                    "One or more assets failed to unload while disposing the store.",
                    failures);
            }
        }

        bool IAssetLeaseOwner<TAsset>.IsLeaseAlive(string key, TAsset asset)
        {
            return !_disposed &&
                   _entries.TryGetValue(key, out Entry entry) &&
                   ReferenceEquals(entry.Asset, asset);
        }

        void IAssetLeaseOwner<TAsset>.Release(string key, TAsset asset)
        {
            if (_disposed)
            {
                return;
            }

            if (!_entries.TryGetValue(key, out Entry entry) ||
                !ReferenceEquals(entry.Asset, asset))
            {
                throw new InvalidOperationException(
                    $"The store no longer owns the asset for key '{key}'.");
            }

            entry.ReferenceCount--;
            if (entry.ReferenceCount > 0)
            {
                return;
            }

            _entries.Remove(key);
            _provider.Unload(key, asset);
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
                throw new ObjectDisposedException(nameof(SharedAssetStore<TAsset>));
            }
        }

        private sealed class Entry
        {
            public Entry(TAsset asset)
            {
                Asset = asset;
                ReferenceCount = 1;
            }

            public TAsset Asset { get; }

            public int ReferenceCount { get; set; }
        }
    }
}
