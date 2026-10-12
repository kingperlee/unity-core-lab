using System;
using System.Collections.Generic;
using NUnit.Framework;
using UnityCoreLab.AssetLifecycle;

namespace UnityCoreLab.Tests.EditMode.AssetLifecycle
{
    public sealed class SharedAssetStoreTests
    {
        [Test]
        public void SameKey_SharesOneLoadUntilLastLeaseIsReleased()
        {
            var provider = new RecordingAssetProvider();
            using (var store = new SharedAssetStore<FakeAsset>(provider))
            {
                AssetLease<FakeAsset> first = store.Acquire("hero");
                AssetLease<FakeAsset> second = store.Acquire("hero");

                Assert.That(first.Asset, Is.SameAs(second.Asset));
                Assert.That(provider.GetLoadCount("hero"), Is.EqualTo(1));
                Assert.That(store.GetReferenceCount("hero"), Is.EqualTo(2));

                first.Dispose();

                Assert.That(provider.GetUnloadCount("hero"), Is.Zero);
                Assert.That(store.GetReferenceCount("hero"), Is.EqualTo(1));

                second.Dispose();

                Assert.That(provider.GetUnloadCount("hero"), Is.EqualTo(1));
                Assert.That(store.IsLoaded("hero"), Is.False);
            }
        }

        [Test]
        public void LeaseDispose_IsIdempotentAndBlocksFurtherAccess()
        {
            var provider = new RecordingAssetProvider();
            using (var store = new SharedAssetStore<FakeAsset>(provider))
            {
                AssetLease<FakeAsset> lease = store.Acquire("icon");

                lease.Dispose();
                lease.Dispose();

                Assert.That(lease.IsReleased, Is.True);
                Assert.Throws<ObjectDisposedException>(() =>
                {
                    FakeAsset ignored = lease.Asset;
                });
                Assert.That(provider.GetUnloadCount("icon"), Is.EqualTo(1));
            }
        }

        [Test]
        public void DifferentKeys_HaveIndependentOwnership()
        {
            var provider = new RecordingAssetProvider();
            using (var store = new SharedAssetStore<FakeAsset>(provider))
            {
                AssetLease<FakeAsset> hero = store.Acquire("hero");
                AssetLease<FakeAsset> enemy = store.Acquire("enemy");

                Assert.That(store.LoadedAssetCount, Is.EqualTo(2));
                Assert.That(hero.Asset, Is.Not.SameAs(enemy.Asset));

                hero.Dispose();

                Assert.That(store.IsLoaded("hero"), Is.False);
                Assert.That(store.IsLoaded("enemy"), Is.True);
                Assert.That(enemy.Asset.Key, Is.EqualTo("enemy"));

                enemy.Dispose();
            }
        }

        [Test]
        public void StoreDispose_UnloadsEntriesAndInvalidatesOutstandingLeases()
        {
            var provider = new RecordingAssetProvider();
            var store = new SharedAssetStore<FakeAsset>(provider);
            AssetLease<FakeAsset> first = store.Acquire("hero");
            AssetLease<FakeAsset> second = store.Acquire("enemy");

            store.Dispose();

            Assert.That(provider.GetUnloadCount("hero"), Is.EqualTo(1));
            Assert.That(provider.GetUnloadCount("enemy"), Is.EqualTo(1));
            Assert.That(first.IsReleased, Is.True);
            Assert.That(second.IsReleased, Is.True);
            Assert.Throws<ObjectDisposedException>(() =>
            {
                FakeAsset ignored = first.Asset;
            });

            first.Dispose();
            second.Dispose();
            Assert.That(provider.TotalUnloadCount, Is.EqualTo(2));
        }

        [Test]
        public void FailedLoad_DoesNotCreateAnOwnedEntry()
        {
            var provider = new RecordingAssetProvider();
            provider.NullKeys.Add("missing");

            using (var store = new SharedAssetStore<FakeAsset>(provider))
            {
                Assert.Throws<InvalidOperationException>(() => store.Acquire("missing"));
                Assert.That(store.IsLoaded("missing"), Is.False);
                Assert.That(store.LoadedAssetCount, Is.Zero);

                provider.NullKeys.Remove("missing");
                using (AssetLease<FakeAsset> lease = store.Acquire("missing"))
                {
                    Assert.That(lease.Asset.Key, Is.EqualTo("missing"));
                }
            }
        }

        [Test]
        public void AcquireAfterStoreDispose_Throws()
        {
            var provider = new RecordingAssetProvider();
            var store = new SharedAssetStore<FakeAsset>(provider);
            store.Dispose();

            Assert.Throws<ObjectDisposedException>(() => store.Acquire("hero"));
        }

        private sealed class FakeAsset
        {
            public FakeAsset(string key)
            {
                Key = key;
            }

            public string Key { get; }
        }

        private sealed class RecordingAssetProvider : IAssetProvider<FakeAsset>
        {
            private readonly Dictionary<string, int> _loadCounts =
                new Dictionary<string, int>(StringComparer.Ordinal);
            private readonly Dictionary<string, int> _unloadCounts =
                new Dictionary<string, int>(StringComparer.Ordinal);

            public HashSet<string> NullKeys { get; } =
                new HashSet<string>(StringComparer.Ordinal);

            public int TotalUnloadCount { get; private set; }

            public FakeAsset Load(string key)
            {
                Increment(_loadCounts, key);
                return NullKeys.Contains(key) ? null : new FakeAsset(key);
            }

            public void Unload(string key, FakeAsset asset)
            {
                Increment(_unloadCounts, key);
                TotalUnloadCount++;
            }

            public int GetLoadCount(string key)
            {
                return GetCount(_loadCounts, key);
            }

            public int GetUnloadCount(string key)
            {
                return GetCount(_unloadCounts, key);
            }

            private static int GetCount(
                IReadOnlyDictionary<string, int> counts,
                string key)
            {
                return counts.TryGetValue(key, out int count) ? count : 0;
            }

            private static void Increment(Dictionary<string, int> counts, string key)
            {
                counts[key] = GetCount(counts, key) + 1;
            }
        }
    }
}
