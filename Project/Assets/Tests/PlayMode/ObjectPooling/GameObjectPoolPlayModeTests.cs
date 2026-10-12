using System;
using System.Collections;
using System.Threading.Tasks;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using UnityCoreLab.Samples.ObjectPooling;

namespace UnityCoreLab.Tests.PlayMode.ObjectPooling
{
    public sealed class GameObjectPoolPlayModeTests
    {
        [UnityTest]
        public IEnumerator RentAndReturn_ResetHierarchyTransformAndComponentState()
        {
            PoolFixture fixture = CreateFixture(maxRetained: 1);
            fixture.Pool.Prewarm(1);

            GameObject firstRent = fixture.Pool.Rent(new Vector3(2f, 3f, 0f));
            var actor = firstRent.GetComponent<PooledDemoActor>();
            actor.Configure(new Vector3(4f, 5f, 0f));
            firstRent.transform.localRotation = Quaternion.Euler(0f, 0f, 45f);
            firstRent.transform.localScale = Vector3.one * 2f;

            Assert.That(firstRent.activeSelf, Is.True);
            Assert.That(firstRent.transform.parent, Is.SameAs(fixture.ActiveRoot));
            Assert.That(actor.RentCount, Is.EqualTo(1));
            Assert.That(actor.ResetCount, Is.EqualTo(1),
                "Prewarm prepares new instances through the reset contract.");

            fixture.Pool.Return(firstRent);

            Assert.That(firstRent.activeSelf, Is.False);
            Assert.That(firstRent.transform.parent, Is.SameAs(fixture.InactiveRoot));
            Assert.That(firstRent.transform.localPosition, Is.EqualTo(Vector3.zero));
            Assert.That(firstRent.transform.localRotation, Is.EqualTo(Quaternion.identity));
            Assert.That(firstRent.transform.localScale, Is.EqualTo(Vector3.one));
            Assert.That(actor.Velocity, Is.EqualTo(Vector3.zero));
            Assert.That(actor.ResetCount, Is.EqualTo(2));

            GameObject secondRent = fixture.Pool.Rent();

            Assert.That(secondRent, Is.SameAs(firstRent));
            Assert.That(actor.RentCount, Is.EqualTo(2));
            Assert.That(fixture.Pool.Snapshot.ReusedCount, Is.EqualTo(2));

            fixture.Dispose();
            yield return null;
        }

        [UnityTest]
        public IEnumerator ReturnBeyondCapacity_DiscardsOverflowInstances()
        {
            PoolFixture fixture = CreateFixture(maxRetained: 1);
            GameObject first = fixture.Pool.Rent();
            GameObject second = fixture.Pool.Rent();

            fixture.Pool.Return(first);
            fixture.Pool.Return(second);

            Assert.That(fixture.Pool.Snapshot.AvailableCount, Is.EqualTo(1));
            Assert.That(fixture.Pool.Snapshot.DiscardedCount, Is.EqualTo(1));
            Assert.That(fixture.Pool.Snapshot.OwnedCount, Is.EqualTo(1));

            yield return null;
            Assert.That(second == null, Is.True);

            fixture.Dispose();
            yield return null;
        }

        [UnityTest]
        public IEnumerator CallsFromAnotherThread_AreRejectedBeforeUnityApiUse()
        {
            PoolFixture fixture = CreateFixture(maxRetained: 1);
            Task<Exception> workerCall = Task.Run(() =>
            {
                try
                {
                    fixture.Pool.Rent();
                    return null;
                }
                catch (Exception exception)
                {
                    return exception;
                }
            });

            while (!workerCall.IsCompleted)
            {
                yield return null;
            }

            Assert.That(workerCall.Result, Is.TypeOf<InvalidOperationException>());
            Assert.That(workerCall.Result.Message, Does.Contain("Unity thread"));
            Assert.That(fixture.Pool.Snapshot.CreatedCount, Is.Zero);

            fixture.Dispose();
            yield return null;
        }

        [UnityTest]
        public IEnumerator Demo_BurstReturnAndRespawn_ExposesCapacityAndReuse()
        {
            var demoRoot = new GameObject("Object Pooling Demo Test");
            var demo = demoRoot.AddComponent<ObjectPoolingDemo>();

            Assert.That(demo.IsInitialized, Is.True);
            Assert.That(demo.AvailableCount, Is.EqualTo(ObjectPoolingDemo.PrewarmCount));

            demo.SpawnBurst(6);
            Assert.That(demo.ActiveCount, Is.EqualTo(6));
            Assert.That(demo.CreatedCount, Is.EqualTo(6));
            Assert.That(demo.PeakActiveCount, Is.EqualTo(6));

            demo.ReturnAll();
            Assert.That(demo.ActiveCount, Is.Zero);
            Assert.That(demo.AvailableCount, Is.EqualTo(ObjectPoolingDemo.RetainedCapacity));
            Assert.That(demo.DiscardedCount, Is.EqualTo(2));

            int reusedBeforeRespawn = demo.ReusedCount;
            demo.SpawnBurst(2);
            Assert.That(demo.ReusedCount, Is.EqualTo(reusedBeforeRespawn + 2));

            UnityEngine.Object.Destroy(demoRoot);
            yield return null;
        }

        private static PoolFixture CreateFixture(int maxRetained)
        {
            var root = new GameObject("GameObject Pool Test Root");
            Transform activeRoot = CreateChild(root.transform, "Active");
            Transform inactiveRoot = CreateChild(root.transform, "Inactive");
            var template = new GameObject("Template");
            template.transform.SetParent(root.transform, false);
            template.AddComponent<PooledDemoActor>();
            template.SetActive(false);

            var pool = new GameObjectPool(
                template,
                activeRoot,
                inactiveRoot,
                maxRetained);
            return new PoolFixture(root, pool, activeRoot, inactiveRoot);
        }

        private static Transform CreateChild(Transform parent, string name)
        {
            var child = new GameObject(name);
            child.transform.SetParent(parent, false);
            return child.transform;
        }

        private sealed class PoolFixture : IDisposable
        {
            public PoolFixture(
                GameObject root,
                GameObjectPool pool,
                Transform activeRoot,
                Transform inactiveRoot)
            {
                Root = root;
                Pool = pool;
                ActiveRoot = activeRoot;
                InactiveRoot = inactiveRoot;
            }

            public GameObject Root { get; }

            public GameObjectPool Pool { get; }

            public Transform ActiveRoot { get; }

            public Transform InactiveRoot { get; }

            public void Dispose()
            {
                Pool.Dispose();
                UnityEngine.Object.Destroy(Root);
            }
        }
    }
}
