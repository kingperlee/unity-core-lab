using System;
using System.Threading;
using System.Threading.Tasks;
using Cysharp.Threading.Tasks;
using UnityEngine;
using UnityCoreLab.AssetLifecycle;

namespace UnityCoreLab.Samples.AssetLifecycle
{
    /// <summary>
    /// Adapts a Unity/UniTask provider to the Task-based ownership core.
    /// Unity API calls enter and leave through the configured PlayerLoop phase.
    /// </summary>
    public sealed class UniTaskAssetProviderAdapter<TAsset> :
        IAsyncAssetProvider<TAsset>
        where TAsset : class
    {
        private readonly IUniTaskAssetProvider<TAsset> _provider;
        private readonly PlayerLoopTiming _playerLoopTiming;
        private readonly Action<Exception> _deferredUnloadExceptionHandler;

        public UniTaskAssetProviderAdapter(
            IUniTaskAssetProvider<TAsset> provider,
            PlayerLoopTiming playerLoopTiming = PlayerLoopTiming.Update,
            Action<Exception> deferredUnloadExceptionHandler = null)
        {
            _provider = provider ?? throw new ArgumentNullException(nameof(provider));
            _playerLoopTiming = playerLoopTiming;
            _deferredUnloadExceptionHandler =
                deferredUnloadExceptionHandler ?? Debug.LogException;
        }

        public Task<TAsset> LoadAsync(
            string key,
            CancellationToken cancellationToken)
        {
            return LoadOnUnityPlayerLoopAsync(key, cancellationToken).AsTask();
        }

        public void Unload(string key, TAsset asset)
        {
            if (PlayerLoopHelper.IsMainThread)
            {
                _provider.Unload(key, asset);
                return;
            }

            // The Task-based core permits release from any thread. Unity resource
            // APIs do not, so a worker-thread release is deferred to PlayerLoop.
            UniTask.Post(
                () => UnloadDeferred(key, asset),
                _playerLoopTiming);
        }

        private async UniTask<TAsset> LoadOnUnityPlayerLoopAsync(
            string key,
            CancellationToken cancellationToken)
        {
            await UniTask.SwitchToMainThread(
                _playerLoopTiming,
                cancellationToken);

            TAsset asset = await _provider.LoadAsync(key, cancellationToken);

            // A provider is allowed to perform worker-thread work internally.
            // Re-enter Unity's PlayerLoop before completing the Task boundary.
            await UniTask.SwitchToMainThread(
                _playerLoopTiming,
                cancellationToken);
            return asset;
        }

        private void UnloadDeferred(string key, TAsset asset)
        {
            try
            {
                _provider.Unload(key, asset);
            }
            catch (Exception exception)
            {
                // There is no synchronous caller left to receive an exception from
                // a posted action, so route it through an explicit observer.
                _deferredUnloadExceptionHandler(exception);
            }
        }
    }
}
