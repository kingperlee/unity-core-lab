using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using NUnit.Framework;
using UnityEngine.TestTools;
using UnityCoreLab.AssetLifecycle;

namespace UnityCoreLab.Tests.EditMode.AssetLifecycle
{
    public sealed class AsyncSharedAssetStoreTests
    {
        [UnityTest]
        public IEnumerator InvalidKey_IsRejectedWithoutStartingAProviderRequest()
        {
            var provider = new ControlledAsyncAssetProvider();
            using (var store = new AsyncSharedAssetStore<FakeAsset>(provider))
            {
                Task<AssetLease<FakeAsset>> acquire = store.AcquireAsync("   ");
                yield return WaitForCompletion(acquire);

                Assert.That(acquire.IsFaulted, Is.True);
                Assert.That(acquire.Exception?.GetBaseException(),
                    Is.TypeOf<ArgumentException>());
                Assert.That(provider.Requests, Is.Empty);
            }
        }

        [UnityTest]
        public IEnumerator AcquireAfterStoreDispose_IsRejectedWithoutStartingALoad()
        {
            var provider = new ControlledAsyncAssetProvider();
            var store = new AsyncSharedAssetStore<FakeAsset>(provider);
            store.Dispose();

            Task<AssetLease<FakeAsset>> acquire = store.AcquireAsync("hero");
            yield return WaitForCompletion(acquire);

            Assert.That(acquire.IsFaulted, Is.True);
            Assert.That(acquire.Exception?.GetBaseException(),
                Is.TypeOf<ObjectDisposedException>());
            Assert.That(provider.Requests, Is.Empty);
        }

        [UnityTest]
        public IEnumerator ConcurrentAcquire_CoalescesOneUnderlyingLoad()
        {
            var provider = new ControlledAsyncAssetProvider();
            using (var store = new AsyncSharedAssetStore<FakeAsset>(provider))
            {
                Task<AssetLease<FakeAsset>> first = store.AcquireAsync("hero");
                Task<AssetLease<FakeAsset>> second = store.AcquireAsync("hero");

                Assert.That(provider.Requests.Count, Is.EqualTo(1));
                Assert.That(store.LoadingAssetCount, Is.EqualTo(1));
                Assert.That(store.GetWaiterCount("hero"), Is.EqualTo(2));

                provider.Requests[0].Complete();
                yield return WaitForCompletion(first);
                yield return WaitForCompletion(second);

                Assert.That(first.IsCompletedSuccessfully, Is.True);
                Assert.That(second.IsCompletedSuccessfully, Is.True);
                Assert.That(first.Result.Asset, Is.SameAs(second.Result.Asset));
                Assert.That(store.GetReferenceCount("hero"), Is.EqualTo(2));
                Assert.That(store.LoadedAssetCount, Is.EqualTo(1));

                first.Result.Dispose();
                Assert.That(provider.GetUnloadCount("hero"), Is.Zero);

                second.Result.Dispose();
                Assert.That(provider.GetUnloadCount("hero"), Is.EqualTo(1));
            }
        }

        [UnityTest]
        public IEnumerator CancellingOneWaiter_DoesNotCancelSharedLoad()
        {
            var provider = new ControlledAsyncAssetProvider();
            using (var store = new AsyncSharedAssetStore<FakeAsset>(provider))
            using (var cancellation = new CancellationTokenSource())
            {
                Task<AssetLease<FakeAsset>> cancelled = store.AcquireAsync(
                    "hero",
                    cancellation.Token);
                Task<AssetLease<FakeAsset>> remaining = store.AcquireAsync("hero");

                cancellation.Cancel();
                yield return WaitForCompletion(cancelled);

                Assert.That(cancelled.IsCanceled, Is.True);
                Assert.That(provider.Requests[0].CancellationToken.IsCancellationRequested,
                    Is.False);
                Assert.That(store.GetWaiterCount("hero"), Is.EqualTo(1));

                provider.Requests[0].Complete();
                yield return WaitForCompletion(remaining);

                Assert.That(remaining.IsCompletedSuccessfully, Is.True);
                Assert.That(store.GetReferenceCount("hero"), Is.EqualTo(1));
                remaining.Result.Dispose();
            }
        }

        [UnityTest]
        public IEnumerator AllWaitersCancel_LateResultIsImmediatelyUnloaded()
        {
            var provider = new ControlledAsyncAssetProvider();
            using (var store = new AsyncSharedAssetStore<FakeAsset>(provider))
            using (var cancellation = new CancellationTokenSource())
            {
                Task<AssetLease<FakeAsset>> acquire = store.AcquireAsync(
                    "hero",
                    cancellation.Token);

                cancellation.Cancel();
                yield return WaitForCompletion(acquire);

                Assert.That(acquire.IsCanceled, Is.True);
                Assert.That(provider.Requests[0].CancellationToken.IsCancellationRequested,
                    Is.False,
                    "A caller cancellation must not own the shared provider request.");

                provider.Requests[0].Complete();
                yield return WaitUntil(() => provider.GetUnloadCount("hero") == 1);

                Assert.That(store.LoadedAssetCount, Is.Zero);
                Assert.That(store.GetReferenceCount("hero"), Is.Zero);
                Assert.That(provider.GetUnloadCount("hero"), Is.EqualTo(1));
            }
        }

        [UnityTest]
        public IEnumerator AllWaitersCancel_LateFailureIsRemovedAndCanRetry()
        {
            var provider = new ControlledAsyncAssetProvider();
            using (var store = new AsyncSharedAssetStore<FakeAsset>(provider))
            using (var cancellation = new CancellationTokenSource())
            {
                Task<AssetLease<FakeAsset>> cancelled = store.AcquireAsync(
                    "hero",
                    cancellation.Token);

                cancellation.Cancel();
                yield return WaitForCompletion(cancelled);
                Assert.That(cancelled.IsCanceled, Is.True);

                provider.Requests[0].Fail(new InvalidOperationException("late failure"));
                yield return WaitUntil(() => store.LoadingAssetCount == 0);

                Task<AssetLease<FakeAsset>> retry = store.AcquireAsync("hero");
                Assert.That(provider.Requests.Count, Is.EqualTo(2));

                provider.Requests[1].Complete();
                yield return WaitForCompletion(retry);

                Assert.That(retry.IsCompletedSuccessfully, Is.True);
                retry.Result.Dispose();
            }
        }

        [UnityTest]
        public IEnumerator FailedLoad_IsRemovedSoTheNextAcquireCanRetry()
        {
            var provider = new ControlledAsyncAssetProvider();
            using (var store = new AsyncSharedAssetStore<FakeAsset>(provider))
            {
                Task<AssetLease<FakeAsset>> failed = store.AcquireAsync("hero");
                provider.Requests[0].Fail(new InvalidOperationException("expected failure"));
                yield return WaitForCompletion(failed);

                Assert.That(failed.IsFaulted, Is.True);
                Assert.That(failed.Exception?.GetBaseException().Message,
                    Is.EqualTo("expected failure"));
                yield return WaitUntil(() => store.LoadingAssetCount == 0);

                Task<AssetLease<FakeAsset>> retry = store.AcquireAsync("hero");
                Assert.That(provider.Requests.Count, Is.EqualTo(2));

                provider.Requests[1].Complete();
                yield return WaitForCompletion(retry);

                Assert.That(retry.IsCompletedSuccessfully, Is.True);
                retry.Result.Dispose();
            }
        }

        [UnityTest]
        public IEnumerator StoreDispose_CancelsInFlightLoadAndInvalidatesLeases()
        {
            var provider = new ControlledAsyncAssetProvider();
            var store = new AsyncSharedAssetStore<FakeAsset>(provider);

            Task<AssetLease<FakeAsset>> ready = store.AcquireAsync("hero");
            provider.Requests[0].Complete();
            yield return WaitForCompletion(ready);
            AssetLease<FakeAsset> lease = ready.Result;

            Task<AssetLease<FakeAsset>> loading = store.AcquireAsync("enemy");
            ControlledAsyncAssetProvider.Request loadingRequest = provider.Requests[1];

            store.Dispose();
            yield return WaitForCompletion(loading);

            Assert.That(loadingRequest.CancellationToken.IsCancellationRequested, Is.True);
            Assert.That(loading.IsCanceled, Is.True);
            Assert.That(provider.GetUnloadCount("hero"), Is.EqualTo(1));
            Assert.That(lease.IsReleased, Is.True);
            Assert.Throws<ObjectDisposedException>(() =>
            {
                FakeAsset ignored = lease.Asset;
            });

            lease.Dispose();
            store.Dispose();
            Assert.That(provider.GetUnloadCount("hero"), Is.EqualTo(1));
        }

        [UnityTest]
        public IEnumerator StoreDispose_ContinuesUnloadingAndAggregatesFailures()
        {
            var provider = new ControlledAsyncAssetProvider();
            var store = new AsyncSharedAssetStore<FakeAsset>(provider);

            Task<AssetLease<FakeAsset>> hero = store.AcquireAsync("hero");
            Task<AssetLease<FakeAsset>> enemy = store.AcquireAsync("enemy");
            provider.Requests[0].Complete();
            provider.Requests[1].Complete();
            yield return WaitForCompletion(hero);
            yield return WaitForCompletion(enemy);

            provider.FailUnloadFor("hero");
            provider.FailUnloadFor("enemy");

            AggregateException exception = Assert.Throws<AggregateException>(
                () => store.Dispose());

            Assert.That(exception.InnerExceptions.Count, Is.EqualTo(2));
            Assert.That(provider.GetUnloadCount("hero"), Is.EqualTo(1));
            Assert.That(provider.GetUnloadCount("enemy"), Is.EqualTo(1));
            Assert.That(hero.Result.IsReleased, Is.True);
            Assert.That(enemy.Result.IsReleased, Is.True);

            hero.Result.Dispose();
            enemy.Result.Dispose();
            Assert.DoesNotThrow(() => store.Dispose());
        }

        private static IEnumerator WaitForCompletion(Task task)
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

        private sealed class ControlledAsyncAssetProvider :
            IAsyncAssetProvider<FakeAsset>
        {
            private readonly Dictionary<string, int> _unloadCounts =
                new Dictionary<string, int>(StringComparer.Ordinal);
            private readonly HashSet<string> _unloadFailures =
                new HashSet<string>(StringComparer.Ordinal);

            public List<Request> Requests { get; } = new List<Request>();

            public Task<FakeAsset> LoadAsync(
                string key,
                CancellationToken cancellationToken)
            {
                var request = new Request(key, cancellationToken);
                Requests.Add(request);
                return request.Task;
            }

            public void Unload(string key, FakeAsset asset)
            {
                _unloadCounts[key] = GetUnloadCount(key) + 1;
                if (_unloadFailures.Contains(key))
                {
                    throw new InvalidOperationException(
                        $"Expected unload failure for '{key}'.");
                }
            }

            public void FailUnloadFor(string key)
            {
                _unloadFailures.Add(key);
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
                    _registration = cancellationToken.Register(
                        () => _completion.TrySetCanceled());
                }

                public string Key { get; }

                public CancellationToken CancellationToken { get; }

                public Task<FakeAsset> Task => _completion.Task;

                public void Complete()
                {
                    _registration.Dispose();
                    _completion.TrySetResult(new FakeAsset(Key));
                }

                public void Fail(Exception exception)
                {
                    _registration.Dispose();
                    _completion.TrySetException(exception);
                }
            }
        }
    }
}
