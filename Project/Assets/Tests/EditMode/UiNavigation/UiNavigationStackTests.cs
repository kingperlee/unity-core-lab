using System;
using NUnit.Framework;
using UnityCoreLab.UiNavigation;

namespace UnityCoreLab.Tests.EditMode.UiNavigation
{
    public sealed class UiNavigationStackTests
    {
        [Test]
        public void Back_RemovesModalBeforeScreenAndProtectsRoot()
        {
            var navigation = new UiNavigationStack(4, 2);
            navigation.PushScreen("Home");
            navigation.PushScreen("Inventory");
            navigation.PushModal("Settings");

            Assert.That(navigation.Back(out UiRoute removed),
                Is.EqualTo(UiNavigationResult.Succeeded));
            Assert.That(removed.Id, Is.EqualTo("Settings"));
            Assert.That(removed.Layer, Is.EqualTo(UiLayer.Modal));
            Assert.That(navigation.ScreenCount, Is.EqualTo(2));

            Assert.That(navigation.Back(out removed),
                Is.EqualTo(UiNavigationResult.Succeeded));
            Assert.That(removed.Id, Is.EqualTo("Inventory"));
            Assert.That(navigation.Back(out removed),
                Is.EqualTo(UiNavigationResult.RootScreenProtected));
            Assert.That(removed.Id, Is.Null);
            Assert.That(navigation.GetScreen(0).Id, Is.EqualTo("Home"));
        }

        [Test]
        public void Push_RejectsDuplicatesAndBoundedCapacity()
        {
            var navigation = new UiNavigationStack(2, 1);

            Assert.That(navigation.PushScreen("Home"),
                Is.EqualTo(UiNavigationResult.Succeeded));
            Assert.That(navigation.PushScreen("Home"),
                Is.EqualTo(UiNavigationResult.DuplicateRoute));
            Assert.That(navigation.PushScreen("Inventory"),
                Is.EqualTo(UiNavigationResult.Succeeded));
            Assert.That(navigation.PushScreen("Profile"),
                Is.EqualTo(UiNavigationResult.CapacityReached));
            Assert.That(navigation.PushModal("Settings"),
                Is.EqualTo(UiNavigationResult.Succeeded));
            Assert.That(navigation.PushModal("Confirm"),
                Is.EqualTo(UiNavigationResult.CapacityReached));
            Assert.That(navigation.Revision, Is.EqualTo(3));
        }

        [Test]
        public void ReplaceTopScreen_ReportsPreviousAndPreservesDepth()
        {
            var navigation = new UiNavigationStack(3, 1);
            navigation.PushScreen("Home");
            navigation.PushScreen("Inventory");
            UiNavigationChange observed = default;
            navigation.Changed += change => observed = change;

            Assert.That(navigation.ReplaceTopScreen("Shop"),
                Is.EqualTo(UiNavigationResult.Succeeded));
            Assert.That(navigation.ScreenCount, Is.EqualTo(2));
            Assert.That(navigation.GetScreen(1).Id, Is.EqualTo("Shop"));
            Assert.That(observed.Kind,
                Is.EqualTo(UiNavigationChangeKind.Replaced));
            Assert.That(observed.Route.Id, Is.EqualTo("Shop"));
            Assert.That(observed.PreviousRoute.Id, Is.EqualTo("Inventory"));
            Assert.That(navigation.ReplaceTopScreen("Shop"),
                Is.EqualTo(UiNavigationResult.NoChange));
            Assert.That(navigation.ReplaceTopScreen("Home"),
                Is.EqualTo(UiNavigationResult.DuplicateRoute));
        }

        [Test]
        public void ChangeCallback_CannotReenterNavigationMutation()
        {
            var navigation = new UiNavigationStack(3, 1);
            navigation.PushScreen("Home");
            navigation.Changed += _ => navigation.PushScreen("Profile");

            Assert.Throws<InvalidOperationException>(() =>
                navigation.PushScreen("Inventory"));
            Assert.That(navigation.ScreenCount, Is.EqualTo(2));
            Assert.That(navigation.GetScreen(1).Id, Is.EqualTo("Inventory"));
            Assert.That(navigation.Revision, Is.EqualTo(2));
        }

        [Test]
        public void Navigation_ValidatesConfigurationAndIndexes()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() =>
                new UiNavigationStack(0, 1));
            Assert.Throws<ArgumentOutOfRangeException>(() =>
                new UiNavigationStack(1, 0));

            var navigation = new UiNavigationStack(2, 1);
            Assert.Throws<ArgumentException>(() =>
                navigation.PushScreen(string.Empty));
            Assert.Throws<ArgumentException>(() =>
                navigation.Push(default));
            Assert.Throws<ArgumentOutOfRangeException>(() =>
                navigation.GetScreen(0));
        }
    }
}
