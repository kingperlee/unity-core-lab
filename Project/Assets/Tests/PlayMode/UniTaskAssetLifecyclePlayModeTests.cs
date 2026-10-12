using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using Cysharp.Threading.Tasks;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using UnityCoreLab.AssetLifecycle;
using UnityCoreLab.Samples.AssetLifecycle;

namespace UnityCoreLab.Tests.PlayMode
{
    public sealed class UniTaskAssetLifecyclePlayModeTests
    {
        [UnityTest]
        public IEnumerator AcquireForOwner_TracksLeaseAndReleasesItOnDestroy()
        {
            int mainThreadId = Thread.CurrentThread.ManagedThreadId;
            var provider = new ControlledUniTaskAssetProvider();
            var adapter = new UniTaskAssetProviderAdapter<FakeAsset>(provider);
            using (var store = new AsyncSharedAssetStore<FakeAsset>(adapter))
            {
                var ownerObject = new GameObject("Asset Lease Owner");
                var owner = ownerObject.AddComponent<AssetLeaseScope>();

                Task<AssetLease<FakeAsset>> acquire = store
                    .AcquireForOwnerAsync("hero", owner)
                    .AsTask();

                Assert.That(provider.Requests.Count, Is.EqualTo(1));
                Assert.That(provider.Requests[0].StartedThreadId,
                    Is.EqualTo(mainThreadId));

                provider.Requests[0].Complete();
                yield return Await(acquire);

                Assert.That(owner.TrackedLeaseCount, Is.EqualTo(1));
                Assert.That(store.GetReferenceCount("hero"), Is.EqualTo(1));

                UnityEngine.Object.Destroy(ownerObject);
                yield return WaitUntil(() => provider.GetUnloadCount("hero") == 1);

                Assert.That(provider.LastUnloadThreadId, Is.EqualTo(mainThreadId));
                Assert.That(acquire.Result.IsReleased, Is.True);
            }
        }

        [UnityTest]
        public IEnumerator DestroyingOneOwner_CancelsOnlyItsWaiter()
        {
            var provider = new ControlledUniTaskAssetProvider();
            var adapter = new UniTaskAssetProviderAdapter<FakeAsset>(provider);
            using (var store = new AsyncSharedAssetStore<FakeAsset>(adapter))
            {
                var firstObject = new GameObject("First Asset Owner");
                var firstOwner = firstObject.AddComponent<AssetLeaseScope>();
                var secondObject = new GameObject("Second Asset Owner");
                var secondOwner = secondObject.AddComponent<AssetLeaseScope>();

                Task<(bool IsCanceled, AssetLease<FakeAsset> Result)> cancelled = store
                    .AcquireForOwnerAsync("hero", firstOwner)
                    .SuppressCancellationThrow()
                    .AsTask();
                Task<AssetLease<FakeAsset>> remaining = store
                    .AcquireForOwnerAsync("hero", secondOwner)
                    .AsTask();

                Assert.That(provider.Requests.Count, Is.EqualTo(1));

                UnityEngine.Object.Destroy(firstObject);
                yield return AwaitCompletion(cancelled);

                Assert.That(cancelled.IsCompletedSuccessfully, Is.True);
                Assert.That(cancelled.Result.IsCanceled, Is.True);
                Assert.That(cancelled.Result.Result, Is.Null);
                Assert.That(provider.Requests[0].CancellationToken.IsCancellationRequested,
                    Is.False,
                    "An owner token cancels one waiter, not the shared provider request.");

                provider.Requests[0].Complete();
                yield return Await(remaining);

                Assert.That(remaining.IsCompletedSuccessfully, Is.True);
                Assert.That(store.GetReferenceCount("hero"), Is.EqualTo(1));

                UnityEngine.Object.Destroy(secondObject);
                yield return WaitUntil(() => provider.GetUnloadCount("hero") == 1);
            }
        }

        [UnityTest]
        public IEnumerator WorkerThreadRelease_IsDeferredToUnityPlayerLoop()
        {
            int mainThreadId = Thread.CurrentThread.ManagedThreadId;
            var provider = new ControlledUniTaskAssetProvider();
            var adapter = new UniTaskAssetProviderAdapter<FakeAsset>(provider);
            using (var store = new AsyncSharedAssetStore<FakeAsset>(adapter))
            {
                Task<AssetLease<FakeAsset>> acquire = store
                    .AcquireUniTaskAsync("hero")
                    .AsTask();
                provider.Requests[0].Complete();
                yield return Await(acquire);

                int releasingThreadId = -1;
                Task release = Task.Run(() =>
                {
                    releasingThreadId = Thread.CurrentThread.ManagedThreadId;
                    acquire.Result.Dispose();
                });

                yield return Await(release);
                yield return WaitUntil(() => provider.GetUnloadCount("hero") == 1);

                Assert.That(releasingThreadId, Is.Not.EqualTo(mainThreadId));
                Assert.That(provider.LastUnloadThreadId, Is.EqualTo(mainThreadId));
            }
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

        private static IEnumerator WaitUntil(Func<bool> predicate)
        {
            var stopwatch = Stopwatch.StartNew();
            while (!predicate() && stopwatch.Elapsed < TimeSpan.FromSeconds(5))
            {
                yield return null;
            }

            Assert.That(predicate(), Is.True, "Condition was not met within 5 seconds.");
        }

        private sealed class FakeAsset
        {
            public FakeAsset(string key)
            {
                Key = key;
            }

            public string Key { get; }
        }

        private sealed class ControlledUniTaskAssetProvider :
            IUniTaskAssetProvider<FakeAsset>
        {
            private readonly Dictionary<string, int> _unloadCounts =
                new Dictionary<string, int>(StringComparer.Ordinal);

            public List<Request> Requests { get; } = new List<Request>();

            public int LastUnloadThreadId { get; private set; } = -1;

            public UniTask<FakeAsset> LoadAsync(
                string key,
                CancellationToken cancellationToken)
            {
                var request = new Request(key, cancellationToken);
                Requests.Add(request);
                return request.Task.AsUniTask(useCurrentSynchronizationContext: false);
            }

            public void Unload(string key, FakeAsset asset)
            {
                LastUnloadThreadId = Thread.CurrentThread.ManagedThreadId;
                _unloadCounts[key] = GetUnloadCount(key) + 1;
            }

            public int GetUnloadCount(string key)
            {
                return _unloadCounts.TryGetValue(key, out int count) ? count : 0;
            }

            public sealed class Request
            {
                private readonly TaskCompletionSource<FakeAsset> _completion =
                    new TaskCompletionSource<FakeAsset>(
                        TaskCreationOptions.RunContinuationsAsynchronously);
                private readonly CancellationTokenRegistration _registration;

                public Request(string key, CancellationToken cancellationToken)
                {
                    Key = key;
                    CancellationToken = cancellationToken;
                    StartedThreadId = Thread.CurrentThread.ManagedThreadId;
                    _registration = cancellationToken.Register(
                        () => _completion.TrySetCanceled());
                }

                public string Key { get; }

                public int StartedThreadId { get; }

                public CancellationToken CancellationToken { get; }

                public Task<FakeAsset> Task => _completion.Task;

                public void Complete()
                {
                    _registration.Dispose();
                    _completion.TrySetResult(new FakeAsset(Key));
                }
            }
        }
    }
}
