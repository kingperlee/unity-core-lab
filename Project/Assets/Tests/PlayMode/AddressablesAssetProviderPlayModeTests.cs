using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading.Tasks;
using Cysharp.Threading.Tasks;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.ResourceManagement;
using UnityEngine.ResourceManagement.AsyncOperations;
using UnityEngine.TestTools;
using UnityCoreLab.AssetLifecycle;
using UnityCoreLab.Samples.AssetLifecycle;

namespace UnityCoreLab.Tests.PlayMode
{
    public sealed class AddressablesAssetProviderPlayModeTests
    {
        private Action<AsyncOperationHandle, Exception> _previousExceptionHandler;

        [SetUp]
        public void SetUp()
        {
            _previousExceptionHandler = ResourceManager.ExceptionHandler;
            ResourceManager.ExceptionHandler = null;
        }

        [TearDown]
        public void TearDown()
        {
            ResourceManager.ExceptionHandler = _previousExceptionHandler;
        }

        [UnityTest]
        public IEnumerator SharedLeases_RetainOneHandleUntilFinalRelease()
        {
            var asset = new TextAsset("addressables shared asset");
            using (var facade = new ControlledAddressablesFacade())
            {
                facade.EnqueueSuccess(asset);
                var provider = new AddressablesUniTaskAssetProvider<TextAsset>(facade);
                var adapter = new UniTaskAssetProviderAdapter<TextAsset>(provider);
                using (var store = new AsyncSharedAssetStore<TextAsset>(adapter))
                {
                    Task<AssetLease<TextAsset>> first = store
                        .AcquireUniTaskAsync("shared-profile")
                        .AsTask();
                    Task<AssetLease<TextAsset>> second = store
                        .AcquireUniTaskAsync("shared-profile")
                        .AsTask();

                    yield return Await(first);
                    yield return Await(second);

                    Assert.That(facade.LoadCount, Is.EqualTo(1));
                    Assert.That(provider.LoadRequestCount, Is.EqualTo(1));
                    Assert.That(provider.ActiveHandleCount, Is.EqualTo(1));
                    Assert.That(store.GetReferenceCount("shared-profile"), Is.EqualTo(2));

                    first.Result.Dispose();
                    Assert.That(facade.ReleaseCount, Is.Zero);
                    Assert.That(provider.ActiveHandleCount, Is.EqualTo(1));

                    second.Result.Dispose();
                    Assert.That(facade.ReleaseCount, Is.EqualTo(1));
                    Assert.That(provider.ReleaseCount, Is.EqualTo(1));
                    Assert.That(provider.ActiveHandleCount, Is.Zero);
                }
            }

            UnityEngine.Object.Destroy(asset);
            yield return null;
        }

        [UnityTest]
        public IEnumerator FailedHandle_IsReleasedAndTheStoreCanRetry()
        {
            var retryAsset = new TextAsset("retry succeeded");
            using (var facade = new ControlledAddressablesFacade())
            {
                facade.EnqueueFailure("expected addressables failure");
                facade.EnqueueSuccess(retryAsset);
                var provider = new AddressablesUniTaskAssetProvider<TextAsset>(facade);
                var adapter = new UniTaskAssetProviderAdapter<TextAsset>(provider);
                using (var store = new AsyncSharedAssetStore<TextAsset>(adapter))
                {
                    Task<AssetLease<TextAsset>> failed = store
                        .AcquireUniTaskAsync("shared-profile")
                        .AsTask();
                    yield return AwaitCompletion(failed);

                    Assert.That(failed.IsFaulted, Is.True);
                    Assert.That(failed.Exception?.GetBaseException().Message,
                        Does.Contain("expected addressables failure"));
                    Assert.That(facade.ReleaseCount, Is.EqualTo(1));
                    Assert.That(provider.ActiveHandleCount, Is.Zero);

                    Task<AssetLease<TextAsset>> retry = store
                        .AcquireUniTaskAsync("shared-profile")
                        .AsTask();
                    yield return Await(retry);

                    Assert.That(facade.LoadCount, Is.EqualTo(2));
                    Assert.That(provider.ActiveHandleCount, Is.EqualTo(1));

                    retry.Result.Dispose();
                    Assert.That(facade.ReleaseCount, Is.EqualTo(2));
                    Assert.That(provider.ActiveHandleCount, Is.Zero);
                }
            }

            UnityEngine.Object.Destroy(retryAsset);
            yield return null;
        }

        private static IEnumerator Await(Task task)
        {
            yield return AwaitCompletion(task);

            if (task.IsFaulted)
            {
                Exception failure = task.Exception;
                throw failure ?? new InvalidOperationException(
                    "Task failed without an exception.");
            }

            Assert.That(task.IsCanceled, Is.False, "Task was unexpectedly cancelled.");
        }

        private static IEnumerator AwaitCompletion(Task task)
        {
            var stopwatch = Stopwatch.StartNew();
            while (!task.IsCompleted && stopwatch.Elapsed < TimeSpan.FromSeconds(5))
            {
                yield return null;
            }

            Assert.That(task.IsCompleted, Is.True, "Task did not complete within 5 seconds.");
        }

        private sealed class ControlledAddressablesFacade :
            IAddressablesFacade<TextAsset>,
            IDisposable
        {
            private readonly ResourceManager _resourceManager = new ResourceManager();
            private readonly Queue<Func<AsyncOperationHandle<TextAsset>>> _responses =
                new Queue<Func<AsyncOperationHandle<TextAsset>>>();

            public int LoadCount { get; private set; }

            public int ReleaseCount { get; private set; }

            public void EnqueueSuccess(TextAsset asset)
            {
                _responses.Enqueue(
                    () => _resourceManager.CreateCompletedOperation(asset, string.Empty));
            }

            public void EnqueueFailure(string message)
            {
                _responses.Enqueue(
                    () => _resourceManager.CreateCompletedOperation<TextAsset>(
                        null,
                        message));
            }

            public AsyncOperationHandle<TextAsset> LoadAssetAsync(string key)
            {
                LoadCount++;
                if (_responses.Count == 0)
                {
                    throw new InvalidOperationException(
                        "No controlled Addressables response was configured.");
                }

                return _responses.Dequeue().Invoke();
            }

            public void Release(AsyncOperationHandle<TextAsset> handle)
            {
                ReleaseCount++;
                _resourceManager.Release(handle);
            }

            public void Dispose()
            {
                _resourceManager.Dispose();
            }
        }
    }
}
