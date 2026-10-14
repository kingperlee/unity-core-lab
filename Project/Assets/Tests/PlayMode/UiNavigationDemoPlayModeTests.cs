using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using UnityCoreLab.Samples.UiNavigation;
using UnityCoreLab.UiNavigation;

namespace UnityCoreLab.Tests.PlayMode
{
    public sealed class UiNavigationDemoPlayModeTests
    {
        [UnityTest]
        public IEnumerator Demo_UsesModalFirstBackAndProtectsRootScreen()
        {
            var root = new GameObject("UI Navigation Demo Test");
            var demo = root.AddComponent<UiNavigationDemo>();

            Assert.That(demo.IsInitialized, Is.True);
            Assert.That(demo.Navigation.ScreenCount, Is.EqualTo(1));
            Assert.That(demo.Navigation.GetScreen(0).Id, Is.EqualTo("Home"));
            Assert.That(demo.OpenInventory(),
                Is.EqualTo(UiNavigationResult.Succeeded));
            Assert.That(demo.OpenSettingsModal(),
                Is.EqualTo(UiNavigationResult.Succeeded));

            Assert.That(demo.NavigateBack(),
                Is.EqualTo(UiNavigationResult.Succeeded));
            Assert.That(demo.Navigation.ModalCount, Is.Zero);
            Assert.That(demo.Navigation.ScreenCount, Is.EqualTo(2));
            Assert.That(demo.NavigateBack(),
                Is.EqualTo(UiNavigationResult.Succeeded));
            Assert.That(demo.NavigateBack(),
                Is.EqualTo(UiNavigationResult.RootScreenProtected));
            Assert.That(demo.Navigation.GetScreen(0).Id, Is.EqualTo("Home"));

            Object.Destroy(root);
            yield return null;
        }
    }
}
