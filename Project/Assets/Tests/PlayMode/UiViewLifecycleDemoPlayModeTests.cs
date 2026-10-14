using System;
using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using UnityCoreLab.Samples.UiNavigation;
using UnityCoreLab.UiNavigation;

namespace UnityCoreLab.Tests.PlayMode
{
    public sealed class UiViewLifecycleDemoPlayModeTests
    {
        [UnityTest]
        public IEnumerator Demo_CancelsPendingViewAndKeepsHomeVisible()
        {
            var root = new GameObject("UI View Lifecycle Demo Test");
            var demo = root.AddComponent<UiViewLifecycleDemo>();
            yield return WaitUntil(() => demo.Views.ActiveScreenId == "Home");

            demo.OpenSlowThenCancel();
            yield return WaitUntil(() => demo.CancelledCreationCount == 1);

            Assert.That(demo.Views.ActiveScreenId, Is.EqualTo("Home"));
            Assert.That(demo.Navigation.ScreenCount, Is.EqualTo(1));
            Assert.That(demo.Views.TryGetState(
                "SlowDetails",
                UiLayer.Screen,
                out _), Is.False);

            UnityEngine.Object.Destroy(root);
            yield return null;
        }

        [UnityTest]
        public IEnumerator Demo_CreatesRealScreenAndModalLifecycles()
        {
            var root = new GameObject("UI View Lifecycle Demo Test");
            var demo = root.AddComponent<UiViewLifecycleDemo>();
            yield return WaitUntil(() => demo.Views.ActiveScreenId == "Home");

            Assert.That(demo.OpenInventory(), Is.EqualTo(UiNavigationResult.Succeeded));
            yield return WaitUntil(() => demo.Views.ActiveScreenId == "Inventory");
            Assert.That(demo.OpenSettingsModal(),
                Is.EqualTo(UiNavigationResult.Succeeded));
            yield return WaitUntil(() => demo.Views.ActiveModalId == "Settings");

            Assert.That(demo.NavigateBack(), Is.EqualTo(UiNavigationResult.Succeeded));
            Assert.That(demo.Views.ActiveModalId, Is.Null);
            Assert.That(demo.Views.ActiveScreenId, Is.EqualTo("Inventory"));
            Assert.That(demo.NavigateBack(), Is.EqualTo(UiNavigationResult.Succeeded));
            Assert.That(demo.Views.ActiveScreenId, Is.EqualTo("Home"));

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
                "The PlayMode lifecycle condition did not complete in time.");
        }
    }
}
