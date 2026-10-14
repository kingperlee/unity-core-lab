using System;
using System.Threading;
using System.Threading.Tasks;
using Cysharp.Threading.Tasks;
using UnityEngine;
using UnityCoreLab.AssetLifecycle;
using UnityCoreLab.Samples.AssetLifecycle;
using UnityCoreLab.UiNavigation;

namespace UnityCoreLab.Samples.UiNavigation
{
    /// <summary>
    /// Loads registered Resources prefabs through the shared asset store. Each
    /// created view owns one asset lease until its instance is disposed.
    /// </summary>
    public sealed class ResourcesPrefabUiViewFactory :
        IAsyncUiViewFactory,
        IDisposable
    {
        private readonly UiRouteViewRegistry _registry;
        private readonly Transform _screenRoot;
        private readonly Transform _modalRoot;
        private readonly ResourcesPrefabUniTaskAssetProvider _provider;
        private readonly AsyncSharedAssetStore<GameObject> _store;
        private bool _disposed;

        public ResourcesPrefabUiViewFactory(
            UiRouteViewRegistry registry,
            Transform screenRoot,
            Transform modalRoot)
        {
            _registry = registry ?? throw new ArgumentNullException(nameof(registry));
            _screenRoot = screenRoot != null
                ? screenRoot
                : throw new ArgumentNullException(nameof(screenRoot));
            _modalRoot = modalRoot != null
                ? modalRoot
                : throw new ArgumentNullException(nameof(modalRoot));
            _provider = new ResourcesPrefabUniTaskAssetProvider();
            var adapter = new UniTaskAssetProviderAdapter<GameObject>(_provider);
            _store = new AsyncSharedAssetStore<GameObject>(adapter);
        }

        public int LoadRequestCount => _provider.LoadRequestCount;

        public int ReleaseCount => _provider.ReleaseCount;

        public int TrackedPrefabCount => _store.LoadedAssetCount;

        public int LiveInstanceCount { get; private set; }

        public async Task<IUiView> CreateAsync(
            UiRoute route,
            CancellationToken cancellationToken)
        {
            if (_disposed)
            {
                throw new ObjectDisposedException(nameof(ResourcesPrefabUiViewFactory));
            }

            string assetKey = _registry.GetAssetKey(route);
            AssetLease<GameObject> lease = null;
            GameObject instance = null;
            try
            {
                lease = await _store.AcquireAsync(assetKey, cancellationToken);
                await UniTask.SwitchToMainThread(
                    PlayerLoopTiming.Update,
                    cancellationToken);

                Transform parent = route.Layer == UiLayer.Screen
                    ? _screenRoot
                    : _modalRoot;
                instance = UnityEngine.Object.Instantiate(lease.Asset, parent, false);
                instance.name = $"{route.Id} {route.Layer} Prefab Instance";
                PrefabUiViewBehaviour behaviour =
                    instance.GetComponent<PrefabUiViewBehaviour>();
                if (behaviour == null)
                {
                    throw new InvalidOperationException(
                        $"UI prefab '{assetKey}' has no {nameof(PrefabUiViewBehaviour)}.");
                }

                behaviour.Initialize(route);
                LiveInstanceCount++;
                return new LeasedPrefabView(
                    route,
                    instance,
                    behaviour,
                    lease,
                    HandleInstanceDisposed);
            }
            catch
            {
                if (instance != null)
                {
                    UnityEngine.Object.Destroy(instance);
                }

                lease?.Dispose();
                throw;
            }
        }

        public void Dispose()
        {
            if (_disposed)
            {
                return;
            }

            _disposed = true;
            _store.Dispose();
        }

        private void HandleInstanceDisposed()
        {
            LiveInstanceCount--;
        }

        private sealed class LeasedPrefabView : IUiView
        {
            private readonly GameObject _instance;
            private readonly PrefabUiViewBehaviour _behaviour;
            private readonly AssetLease<GameObject> _lease;
            private readonly Action _onDisposed;
            private bool _disposed;

            public LeasedPrefabView(
                UiRoute route,
                GameObject instance,
                PrefabUiViewBehaviour behaviour,
                AssetLease<GameObject> lease,
                Action onDisposed)
            {
                Route = route;
                _instance = instance;
                _behaviour = behaviour;
                _lease = lease;
                _onDisposed = onDisposed;
            }

            public UiRoute Route { get; }

            public void Enter()
            {
                _behaviour.Enter();
            }

            public void Cover()
            {
                _behaviour.Cover();
            }

            public void Resume()
            {
                _behaviour.Resume();
            }

            public void Exit()
            {
                _behaviour.Exit();
            }

            public void Dispose()
            {
                if (_disposed)
                {
                    return;
                }

                _disposed = true;
                if (_instance != null)
                {
                    UnityEngine.Object.Destroy(_instance);
                }

                _lease.Dispose();
                _onDisposed?.Invoke();
            }
        }
    }
}
