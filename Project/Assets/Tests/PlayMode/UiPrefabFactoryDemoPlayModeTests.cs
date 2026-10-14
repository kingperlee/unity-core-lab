using System;
using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using UnityCoreLab.Samples.UiNavigation;
using UnityCoreLab.UiNavigation;

namespace UnityCoreLab.Tests.PlayMode
{
    public sealed class UiPrefabFactoryDemoPlayModeTests
    {
        [UnityTest]
        public IEnumerator Demo_LoadsPrefabAndReleasesItsAssetLeaseOnBack()
        {
            var root = new GameObject("UI Prefab Factory Demo Test");
            var demo = root.AddComponent<UiPrefabFactoryDemo>();
            yield return WaitUntil(() => demo.Views.ActiveScreenId == "Home");

            Assert.That(demo.LoadRequestCount, Is.EqualTo(1));
            Assert.That(demo.TrackedPrefabCount, Is.EqualTo(1));
            Assert.That(demo.LiveInstanceCount, Is.EqualTo(1));
            Assert.That(root.GetComponentInChildren<PrefabUiViewBehaviour>(true),
                Is.Not.Null);

            Assert.That(demo.OpenInventory(), Is.EqualTo(UiNavigationResult.Succeeded));
            yield return WaitUntil(() => demo.Views.ActiveScreenId == "Inventory");
            Assert.That(demo.TrackedPrefabCount, Is.EqualTo(2));
            Assert.That(demo.LiveInstanceCount, Is.EqualTo(2));

            Assert.That(demo.NavigateBack(), Is.EqualTo(UiNavigationResult.Succeeded));
            yield return null;

            Assert.That(demo.Views.ActiveScreenId, Is.EqualTo("Home"));
            Assert.That(demo.LiveInstanceCount, Is.EqualTo(1));
            Assert.That(demo.TrackedPrefabCount, Is.EqualTo(1));
            Assert.That(demo.ReleaseCount, Is.EqualTo(1));

            UnityEngine.Object.Destroy(root);
            yield return null;
        }

        private static IEnumerator WaitUntil(Func<bool> predicate)
        {
            float deadline = Time.realtimeSinceStartup + 3f;
            while (!predicate() && Time.realtimeSinceStartup < deadline)
            {
                yield return null;
            }

            Assert.That(predicate(), Is.True,
                "The prefab factory did not reach the expected state in time.");
        }
    }
}
