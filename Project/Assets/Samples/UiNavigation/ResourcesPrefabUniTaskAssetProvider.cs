using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;
using UnityCoreLab.Samples.AssetLifecycle;

namespace UnityCoreLab.Samples.UiNavigation
{
    /// <summary>
    /// Resources can load prefab assets asynchronously, but Unity deliberately
    /// rejects Resources.UnloadAsset for GameObjects. Releasing a lease therefore
    /// removes this provider's logical ownership only. Physical prefab memory is
    /// reclaimed by Resources.UnloadUnusedAssets at a controlled transition point.
    /// </summary>
    public sealed class ResourcesPrefabUniTaskAssetProvider :
        IUniTaskAssetProvider<GameObject>
    {
        private readonly PlayerLoopTiming _playerLoopTiming;

        public ResourcesPrefabUniTaskAssetProvider(
            PlayerLoopTiming playerLoopTiming = PlayerLoopTiming.Update)
        {
            _playerLoopTiming = playerLoopTiming;
        }

        public int LoadRequestCount { get; private set; }

        public int ReleaseCount { get; private set; }

        public async UniTask<GameObject> LoadAsync(
            string key,
            CancellationToken cancellationToken)
        {
            if (string.IsNullOrWhiteSpace(key))
            {
                throw new ArgumentException(
                    "A Resources prefab key is required.",
                    nameof(key));
            }

            cancellationToken.ThrowIfCancellationRequested();
            LoadRequestCount++;

            ResourceRequest request = Resources.LoadAsync<GameObject>(key);
            UnityEngine.Object loaded = await request.ToUniTask(
                timing: _playerLoopTiming,
                cancellationToken: CancellationToken.None);

            var prefab = loaded as GameObject;
            if (prefab == null)
            {
                throw new InvalidOperationException(
                    $"Resources could not load prefab '{key}'.");
            }

            if (cancellationToken.IsCancellationRequested)
            {
                Unload(key, prefab);
                cancellationToken.ThrowIfCancellationRequested();
            }

            return prefab;
        }

        public void Unload(string key, GameObject asset)
        {
            if (asset == null)
            {
                return;
            }

            // Do not call Resources.UnloadAsset here: Unity does not support it
            // for GameObject prefab assets. The shared store still forgets the
            // asset when its final lease is released.
            ReleaseCount++;
        }
    }
}
