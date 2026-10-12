using System;
using System.Collections.Generic;
using System.Threading;
using UnityEngine;
using UnityCoreLab.ObjectPooling;
using Object = UnityEngine.Object;

namespace UnityCoreLab.Samples.ObjectPooling
{
    /// <summary>
    /// Main-thread adapter that applies the reusable pool policy to GameObjects.
    /// </summary>
    public sealed class GameObjectPool : IDisposable
    {
        private readonly Dictionary<GameObject, IPoolableGameObject[]> _callbacks =
            new Dictionary<GameObject, IPoolableGameObject[]>();
        private readonly GameObject _template;
        private readonly Transform _activeRoot;
        private readonly Transform _inactiveRoot;
        private readonly ReusableObjectPool<GameObject> _pool;
        private readonly int _ownerThreadId;

        public GameObjectPool(
            GameObject template,
            Transform activeRoot,
            Transform inactiveRoot,
            int maxRetained = 32)
        {
            _template = template != null
                ? template
                : throw new ArgumentNullException(nameof(template));
            _activeRoot = activeRoot != null
                ? activeRoot
                : throw new ArgumentNullException(nameof(activeRoot));
            _inactiveRoot = inactiveRoot != null
                ? inactiveRoot
                : throw new ArgumentNullException(nameof(inactiveRoot));
            _ownerThreadId = Thread.CurrentThread.ManagedThreadId;

            _pool = new ReusableObjectPool<GameObject>(
                CreateInstance,
                Activate,
                DeactivateAndReset,
                DestroyInstance,
                maxRetained);
        }

        public int MaxRetained => _pool.MaxRetained;

        public PoolSnapshot Snapshot => _pool.Snapshot;

        public void Prewarm(int targetAvailableCount)
        {
            EnsureOwnerThread();
            _pool.Prewarm(targetAvailableCount);
        }

        public GameObject Rent()
        {
            EnsureOwnerThread();
            return _pool.Rent();
        }

        public GameObject Rent(Vector3 localPosition)
        {
            GameObject instance = Rent();
            instance.transform.localPosition = localPosition;
            return instance;
        }

        public void Return(GameObject instance)
        {
            EnsureOwnerThread();
            _pool.Return(instance);
        }

        public void Dispose()
        {
            EnsureOwnerThread();
            _pool.Dispose();
        }

        private GameObject CreateInstance()
        {
            GameObject instance = Object.Instantiate(_template, _inactiveRoot, false);
            instance.name = $"{_template.name} (Pooled)";
            instance.SetActive(false);
            ResetTransform(instance.transform);
            _callbacks.Add(instance, FindCallbacks(instance));
            return instance;
        }

        private void Activate(GameObject instance)
        {
            instance.transform.SetParent(_activeRoot, false);
            ResetTransform(instance.transform);
            instance.SetActive(true);
            InvokeCallbacks(instance, renting: true);
        }

        private void DeactivateAndReset(GameObject instance)
        {
            InvokeCallbacks(instance, renting: false);
            instance.SetActive(false);
            instance.transform.SetParent(_inactiveRoot, false);
            ResetTransform(instance.transform);
        }

        private void DestroyInstance(GameObject instance)
        {
            _callbacks.Remove(instance);
            if (instance == null)
            {
                return;
            }

            if (Application.isPlaying)
            {
                Object.Destroy(instance);
            }
            else
            {
                Object.DestroyImmediate(instance);
            }
        }

        private void InvokeCallbacks(GameObject instance, bool renting)
        {
            if (!_callbacks.TryGetValue(instance, out IPoolableGameObject[] callbacks))
            {
                throw new InvalidOperationException(
                    "The pooled GameObject has no registered reset contract.");
            }

            foreach (IPoolableGameObject callback in callbacks)
            {
                if (renting)
                {
                    callback.OnRentFromPool();
                }
                else
                {
                    callback.OnReturnToPool();
                }
            }
        }

        private void EnsureOwnerThread()
        {
            if (Thread.CurrentThread.ManagedThreadId != _ownerThreadId)
            {
                throw new InvalidOperationException(
                    "GameObjectPool must be used from the Unity thread that created it.");
            }
        }

        private static IPoolableGameObject[] FindCallbacks(GameObject instance)
        {
            MonoBehaviour[] behaviours =
                instance.GetComponentsInChildren<MonoBehaviour>(true);
            var callbacks = new List<IPoolableGameObject>(behaviours.Length);
            foreach (MonoBehaviour behaviour in behaviours)
            {
                if (behaviour is IPoolableGameObject callback)
                {
                    callbacks.Add(callback);
                }
            }

            return callbacks.ToArray();
        }

        private static void ResetTransform(Transform target)
        {
            target.localPosition = Vector3.zero;
            target.localRotation = Quaternion.identity;
            target.localScale = Vector3.one;
        }
    }
}
