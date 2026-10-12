using System;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.ResourceManagement.AsyncOperations;

namespace UnityCoreLab.Samples.AssetLifecycle
{
    public interface IAddressablesFacade<TAsset>
        where TAsset : UnityEngine.Object
    {
        AsyncOperationHandle<TAsset> LoadAssetAsync(string key);

        void Release(AsyncOperationHandle<TAsset> handle);
    }

    public sealed class UnityAddressablesFacade<TAsset> :
        IAddressablesFacade<TAsset>
        where TAsset : UnityEngine.Object
    {
        public AsyncOperationHandle<TAsset> LoadAssetAsync(string key)
        {
            return Addressables.LoadAssetAsync<TAsset>(key);
        }

        public void Release(AsyncOperationHandle<TAsset> handle)
        {
            Addressables.Release(handle);
        }
    }

    /// <summary>
    /// Retains each successful Addressables handle until the store releases the
    /// final consumer lease. Store references and Addressables handles intentionally
    /// remain separate ownership layers.
    /// </summary>
    public sealed class AddressablesUniTaskAssetProvider<TAsset> :
        IUniTaskAssetProvider<TAsset>
        where TAsset : UnityEngine.Object
    {
        private readonly object _gate = new object();
        private readonly IAddressablesFacade<TAsset> _addressables;
        private readonly PlayerLoopTiming _playerLoopTiming;
        private readonly Dictionary<string, List<HandleEntry>> _handlesByKey =
            new Dictionary<string, List<HandleEntry>>(StringComparer.Ordinal);
        private int _activeHandleCount;

        public AddressablesUniTaskAssetProvider(
            IAddressablesFacade<TAsset> addressables = null,
            PlayerLoopTiming playerLoopTiming = PlayerLoopTiming.Update)
        {
            _addressables = addressables ?? new UnityAddressablesFacade<TAsset>();
            _playerLoopTiming = playerLoopTiming;
        }

        public int LoadRequestCount { get; private set; }

        public int ReleaseCount { get; private set; }

        public int ActiveHandleCount
        {
            get
            {
                lock (_gate)
                {
                    return _activeHandleCount;
                }
            }
        }

        public async UniTask<TAsset> LoadAsync(
            string key,
            CancellationToken cancellationToken)
        {
            if (string.IsNullOrWhiteSpace(key))
            {
                throw new ArgumentException(
                    "An Addressables key cannot be null, empty, or whitespace.",
                    nameof(key));
            }

            cancellationToken.ThrowIfCancellationRequested();
            LoadRequestCount++;
            AsyncOperationHandle<TAsset> handle =
                _addressables.LoadAssetAsync(key);

            try
            {
                TAsset asset = await handle.ToUniTask(
                    timing: _playerLoopTiming,
                    cancellationToken: cancellationToken,
                    autoReleaseWhenCanceled: false);

                if (asset == null)
                {
                    throw new InvalidOperationException(
                        $"Addressables returned null for key '{key}'.");
                }

                cancellationToken.ThrowIfCancellationRequested();
                TrackHandle(key, asset, handle);
                return asset;
            }
            catch (Exception loadFailure)
            {
                try
                {
                    ReleaseHandle(handle);
                }
                catch (Exception releaseFailure)
                {
                    throw new AggregateException(
                        "Addressables load and handle cleanup both failed.",
                        loadFailure,
                        releaseFailure);
                }

                throw;
            }
        }

        public void Unload(string key, TAsset asset)
        {
            AsyncOperationHandle<TAsset> handle;
            lock (_gate)
            {
                if (!_handlesByKey.TryGetValue(key, out List<HandleEntry> entries))
                {
                    throw new InvalidOperationException(
                        $"No Addressables handle is tracked for key '{key}'.");
                }

                int entryIndex = entries.FindIndex(
                    entry => ReferenceEquals(entry.Asset, asset));
                if (entryIndex < 0)
                {
                    throw new InvalidOperationException(
                        $"The Addressables handle for key '{key}' owns another asset.");
                }

                handle = entries[entryIndex].Handle;
                entries.RemoveAt(entryIndex);
                _activeHandleCount--;
                if (entries.Count == 0)
                {
                    _handlesByKey.Remove(key);
                }
            }

            ReleaseHandle(handle);
        }

        private void TrackHandle(
            string key,
            TAsset asset,
            AsyncOperationHandle<TAsset> handle)
        {
            lock (_gate)
            {
                if (!_handlesByKey.TryGetValue(key, out List<HandleEntry> entries))
                {
                    entries = new List<HandleEntry>();
                    _handlesByKey.Add(key, entries);
                }

                entries.Add(new HandleEntry(asset, handle));
                _activeHandleCount++;
            }
        }

        private void ReleaseHandle(AsyncOperationHandle<TAsset> handle)
        {
            if (!handle.IsValid())
            {
                return;
            }

            _addressables.Release(handle);
            ReleaseCount++;
        }

        private sealed class HandleEntry
        {
            public HandleEntry(
                TAsset asset,
                AsyncOperationHandle<TAsset> handle)
            {
                Asset = asset;
                Handle = handle;
            }

            public TAsset Asset { get; }

            public AsyncOperationHandle<TAsset> Handle { get; }
        }
    }
}
