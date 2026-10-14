using System;
using NUnit.Framework;
using UnityCoreLab.UiNavigation;

namespace UnityCoreLab.Tests.EditMode.UiNavigation
{
    public sealed class UiRouteViewRegistryTests
    {
        [Test]
        public void Registry_UsesLayerAsPartOfTheRouteKey()
        {
            var registry = new UiRouteViewRegistry(new[]
            {
                new UiViewRegistration(
                    new UiRoute("Settings", UiLayer.Screen),
                    "Ui/SettingsScreen"),
                new UiViewRegistration(
                    new UiRoute("Settings", UiLayer.Modal),
                    "Ui/SettingsModal")
            });

            Assert.That(
                registry.GetAssetKey(new UiRoute("Settings", UiLayer.Screen)),
                Is.EqualTo("Ui/SettingsScreen"));
            Assert.That(
                registry.GetAssetKey(new UiRoute("Settings", UiLayer.Modal)),
                Is.EqualTo("Ui/SettingsModal"));
        }

        [Test]
        public void Registry_RejectsDuplicateRoutesAndMissingLookups()
        {
            UiViewRegistration first = new UiViewRegistration(
                new UiRoute("Home", UiLayer.Screen),
                "Ui/HomeA");
            UiViewRegistration duplicate = new UiViewRegistration(
                new UiRoute("Home", UiLayer.Screen),
                "Ui/HomeB");

            Assert.Throws<ArgumentException>(
                () => new UiRouteViewRegistry(new[] { first, duplicate }));

            var registry = new UiRouteViewRegistry(new[] { first });
            Assert.That(
                registry.TryGetAssetKey(
                    new UiRoute("Inventory", UiLayer.Screen),
                    out _),
                Is.False);
            Assert.Throws<System.Collections.Generic.KeyNotFoundException>(
                () => registry.GetAssetKey(
                    new UiRoute("Inventory", UiLayer.Screen)));
        }
    }
}
