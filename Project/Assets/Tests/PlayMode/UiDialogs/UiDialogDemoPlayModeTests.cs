using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using UnityCoreLab.Samples.UiDialogs;
using UnityCoreLab.UiDialogs;

namespace UnityCoreLab.Tests.PlayMode.UiDialogs
{
    public sealed class UiDialogDemoPlayModeTests
    {
        [UnityTest]
        public IEnumerator Demo_CoalescesBlocksAwaitsAndAdvancesPriorityQueue()
        {
            var root = new GameObject("UI Dialog Demo Test");
            var demo = root.AddComponent<UiDialogDemo>();
            yield return null;

            Assert.That(demo.View.IsVisible, Is.False);
            Assert.That(demo.View.BlocksInput, Is.False);

            demo.ShowConfirmation();
            Assert.That(demo.View.IsVisible, Is.True);
            Assert.That(demo.View.BlocksInput, Is.True);
            Assert.That(demo.View.CurrentKind,
                Is.EqualTo(UiDialogKind.Confirmation));
            demo.PressSecondary();
            yield return null;
            Assert.That(demo.Status,
                Does.Contain("confirmation result: false"));
            Assert.That(demo.View.IsVisible, Is.False);

            int showCount = demo.View.ShowCount;
            demo.ShowDuplicateNoticeTwice();
            Assert.That(demo.LastDuplicateCoalesced, Is.True);
            Assert.That(demo.View.ShowCount, Is.EqualTo(showCount + 1));
            Assert.That(demo.Queue.PendingCount, Is.Zero);
            demo.PressPrimary();
            yield return null;

            demo.ShowBlocking();
            demo.PressPrimary();
            demo.PressSecondary();
            Assert.That(demo.Queue.ActiveRequest.Id,
                Is.EqualTo("blocking-operation"));
            Assert.That(demo.View.IsVisible, Is.True);
            demo.CompleteBlocking();
            yield return null;
            Assert.That(demo.Queue.ActiveRequest, Is.Null);

            demo.RunPriorityQueueScenario();
            Assert.That(demo.Queue.ActiveRequest.Id,
                Is.EqualTo("priority-gate"));
            Assert.That(demo.Queue.PendingCount, Is.EqualTo(3));
            Assert.That(demo.Queue.PendingRequests[0].Id,
                Is.EqualTo("priority-critical"));
            Assert.That(demo.Queue.PendingRequests[1].Id,
                Is.EqualTo("priority-high"));
            Assert.That(demo.Queue.PendingRequests[2].Id,
                Is.EqualTo("priority-normal"));

            demo.AdvancePriorityQueueScenario();
            Assert.That(demo.Queue.ActiveRequest.Id,
                Is.EqualTo("priority-critical"));
            demo.AdvancePriorityQueueScenario();
            Assert.That(demo.Queue.ActiveRequest.Id,
                Is.EqualTo("priority-high"));
            demo.AdvancePriorityQueueScenario();
            Assert.That(demo.Queue.ActiveRequest.Id,
                Is.EqualTo("priority-normal"));
            demo.AdvancePriorityQueueScenario();
            Assert.That(demo.Queue.ActiveRequest, Is.Null);
            Assert.That(demo.View.IsVisible, Is.False);
            Assert.That(demo.View.BlocksInput, Is.False);

            Object.Destroy(root);
            yield return null;
        }
    }
}
