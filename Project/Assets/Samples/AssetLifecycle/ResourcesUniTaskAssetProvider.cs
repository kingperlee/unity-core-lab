using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;

namespace UnityCoreLab.Samples.AssetLifecycle
{
    /// <summary>
    /// Loads non-GameObject assets through Resources.LoadAsync. Unity's underlying
    /// ResourceRequest cannot be cancelled, so a cancelled store lifetime waits for
    /// completion and unloads the late asset before reporting cancellation.
    /// </summary>
    public sealed class ResourcesUniTaskAssetProvider<TAsset> :
        IUniTaskAssetProvider<TAsset>
        where TAsset : UnityEngine.Object
    {
        private readonly PlayerLoopTiming _playerLoopTiming;

        public ResourcesUniTaskAssetProvider(
            PlayerLoopTiming playerLoopTiming = PlayerLoopTiming.Update)
        {
            _playerLoopTiming = playerLoopTiming;
        }

        public int LoadRequestCount { get; private set; }

        public int UnloadCount { get; private set; }

        public async UniTask<TAsset> LoadAsync(
            string key,
            CancellationToken cancellationToken)
        {
            if (string.IsNullOrWhiteSpace(key))
            {
                throw new ArgumentException(
                    "A Resources key cannot be null, empty, or whitespace.",
                    nameof(key));
            }

            cancellationToken.ThrowIfCancellationRequested();
            LoadRequestCount++;

            ResourceRequest request = Resources.LoadAsync<TAsset>(key);

            // ResourceRequest has no real cancellation API. Cancelling only its
            // UniTask await would lose the late result and make cleanup impossible.
            UnityEngine.Object loaded = await request.ToUniTask(
                timing: _playerLoopTiming,
                cancellationToken: CancellationToken.None);

            var asset = loaded as TAsset;
            if (asset == null)
            {
                throw new InvalidOperationException(
                    $"Resources could not load '{key}' as {typeof(TAsset).Name}.");
            }

            if (cancellationToken.IsCancellationRequested)
            {
                Unload(key, asset);
                cancellationToken.ThrowIfCancellationRequested();
            }

            return asset;
        }

        public void Unload(string key, TAsset asset)
        {
            if (asset == null)
            {
                return;
            }

            Resources.UnloadAsset(asset);
            UnloadCount++;
        }
    }
}
