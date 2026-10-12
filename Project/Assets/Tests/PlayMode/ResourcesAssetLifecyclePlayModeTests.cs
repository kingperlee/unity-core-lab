using System;
using System.Collections;
using System.Diagnostics;
using System.Threading.Tasks;
using Cysharp.Threading.Tasks;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using UnityCoreLab.AssetLifecycle;
using UnityCoreLab.Samples.AssetLifecycle;

namespace UnityCoreLab.Tests.PlayMode
{
    public sealed class ResourcesAssetLifecyclePlayModeTests
    {
        [UnityTest]
        public IEnumerator ResourcesProvider_LoadsAndUnloadsRealTextAsset()
        {
            var provider = new ResourcesUniTaskAssetProvider<TextAsset>();
            var adapter = new UniTaskAssetProviderAdapter<TextAsset>(provider);
            using (var store = new AsyncSharedAssetStore<TextAsset>(adapter))
            {
                Task<AssetLease<TextAsset>> acquire = store
                    .AcquireUniTaskAsync(AssetLifecycleDemo.ResourceKey)
                    .AsTask();

                yield return Await(acquire);

                Assert.That(acquire.Result.Asset.text,
                    Does.Contain("Unity Core Lab Shared Resource"));
                Assert.That(provider.LoadRequestCount, Is.EqualTo(1));
                Assert.That(store.GetReferenceCount(AssetLifecycleDemo.ResourceKey),
                    Is.EqualTo(1));

                acquire.Result.Dispose();

                Assert.That(provider.UnloadCount, Is.EqualTo(1));
                Assert.That(store.GetReferenceCount(AssetLifecycleDemo.ResourceKey),
                    Is.Zero);
            }
        }

        [UnityTest]
        public IEnumerator Demo_TwoOwnersShareOneLoadAndLastOwnerUnloads()
        {
            var demoObject = new GameObject("Asset Lifecycle Demo Test");
            var demo = demoObject.AddComponent<AssetLifecycleDemo>();

            Task acquire = demo.AcquireBothAsync().AsTask();
            yield return Await(acquire);

            Assert.That(demo.IsInitialized, Is.True);
            Assert.That(demo.LoadRequestCount, Is.EqualTo(1));
            Assert.That(demo.ReferenceCount, Is.EqualTo(2));
            Assert.That(demo.UnloadCount, Is.Zero);
            Assert.That(demo.AssetPreview,
                Does.Contain("Unity Core Lab Shared Resource"));

            demo.DestroyFirstOwner();
            yield return null;

            Assert.That(demo.ReferenceCount, Is.EqualTo(1));
            Assert.That(demo.UnloadCount, Is.Zero);

            demo.DestroySecondOwner();
            yield return null;

            Assert.That(demo.ReferenceCount, Is.Zero);
            Assert.That(demo.UnloadCount, Is.EqualTo(1));

            UnityEngine.Object.Destroy(demoObject);
            yield return null;
        }

        private static IEnumerator Await(Task task)
        {
            var stopwatch = Stopwatch.StartNew();
            while (!task.IsCompleted && stopwatch.Elapsed < TimeSpan.FromSeconds(5))
            {
                yield return null;
            }

            Assert.That(task.IsCompleted, Is.True, "Task did not complete within 5 seconds.");

            if (task.IsFaulted)
            {
                Exception failure = task.Exception;
                throw failure ?? new InvalidOperationException(
                    "Task failed without an exception.");
            }

            Assert.That(task.IsCanceled, Is.False, "Task was unexpectedly cancelled.");
        }
    }
}
