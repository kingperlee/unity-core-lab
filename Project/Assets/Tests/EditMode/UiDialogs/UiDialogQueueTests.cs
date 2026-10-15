using System;
using System.Collections;
using System.Collections.Generic;
using System.Threading.Tasks;
using NUnit.Framework;
using UnityEngine.TestTools;
using UnityCoreLab.UiDialogs;

namespace UnityCoreLab.Tests.EditMode.UiDialogs
{
    public sealed class UiDialogQueueTests
    {
        [Test]
        public void Notice_BlocksUntilViewAcknowledgesThenCompletes()
        {
            var view = new RecordingView();
            using (var queue = new UiDialogQueue(view))
            {
                Task<UiDialogResult> completion = queue.ShowNoticeAsync(
                    "notice",
                    "Notice",
                    "Message");

                Assert.That(view.VisibleRequest.Id, Is.EqualTo("notice"));
                Assert.That(queue.IsInputBlocked, Is.True);
                Assert.That(completion.IsCompleted, Is.False);

                view.Decide(UiDialogResult.Acknowledged);

                Assert.That(completion.Result,
                    Is.EqualTo(UiDialogResult.Acknowledged));
                Assert.That(queue.ActiveRequest, Is.Null);
                Assert.That(queue.IsInputBlocked, Is.False);
                Assert.That(view.HideCount, Is.EqualTo(1));
            }
        }

        [Test]
        public void DuplicateId_CoalescesToOneEntryAndOneCompletionTask()
        {
            var view = new RecordingView();
            using (var queue = new UiDialogQueue(view))
            {
                Task<UiDialogResult> first = queue.ShowNoticeAsync(
                    "same",
                    "First",
                    "First owner");
                Task<UiDialogResult> duplicate = queue.ShowNoticeAsync(
                    "same",
                    "Second",
                    "Ignored duplicate");

                Assert.That(duplicate, Is.SameAs(first));
                Assert.That(view.ShowCount, Is.EqualTo(1));
                Assert.That(queue.PendingCount, Is.Zero);
                Assert.That(view.VisibleRequest.Title, Is.EqualTo("First"));

                view.Decide(UiDialogResult.Acknowledged);
                Assert.That(duplicate.Result,
                    Is.EqualTo(UiDialogResult.Acknowledged));
            }
        }

        [Test]
        public void PendingQueue_IsPriorityOrderedStableAndDoesNotPreemptActive()
        {
            var view = new RecordingView();
            using (var queue = new UiDialogQueue(view))
            {
                queue.ShowBlockingAsync(
                    "active",
                    "Active",
                    "Not preempted",
                    UiDialogPriority.Normal);
                queue.ShowNoticeAsync(
                    "normal-a", "Normal A", "", UiDialogPriority.Normal);
                queue.ShowNoticeAsync(
                    "critical-a", "Critical A", "", UiDialogPriority.Critical);
                queue.ShowNoticeAsync(
                    "high", "High", "", UiDialogPriority.High);
                queue.ShowNoticeAsync(
                    "critical-b", "Critical B", "", UiDialogPriority.Critical);
                queue.ShowNoticeAsync(
                    "normal-b", "Normal B", "", UiDialogPriority.Normal);

                Assert.That(queue.ActiveRequest.Id, Is.EqualTo("active"));
                Assert.That(
                    Ids(queue.PendingRequests),
                    Is.EqualTo(new[]
                    {
                        "critical-a",
                        "critical-b",
                        "high",
                        "normal-a",
                        "normal-b"
                    }));

                queue.Complete("active", UiDialogResult.Completed);
                Assert.That(queue.ActiveRequest.Id, Is.EqualTo("critical-a"));
                view.Decide(UiDialogResult.Acknowledged);
                Assert.That(queue.ActiveRequest.Id, Is.EqualTo("critical-b"));
            }
        }

        [Test]
        public void BlockingDialog_IgnoresUserDecisionAndRequiresOwnerCompletion()
        {
            var view = new RecordingView();
            using (var queue = new UiDialogQueue(view))
            {
                Task<UiDialogResult> completion = queue.ShowBlockingAsync(
                    "sync",
                    "Syncing",
                    "Please wait");

                view.Decide(UiDialogResult.Cancelled);
                view.Decide(UiDialogResult.Confirmed);

                Assert.That(completion.IsCompleted, Is.False);
                Assert.That(queue.ActiveRequest.Id, Is.EqualTo("sync"));

                Assert.That(
                    queue.Complete("sync", UiDialogResult.Completed),
                    Is.True);
                Assert.That(completion.Result,
                    Is.EqualTo(UiDialogResult.Completed));
            }
        }

        [UnityTest]
        public IEnumerator ConfirmConvenience_ReturnsAwaitableBoolean()
        {
            var view = new RecordingView();
            using (var queue = new UiDialogQueue(view))
            {
                Task<bool> confirmed = queue.ShowConfirmAsync(
                    "confirm",
                    "Confirm",
                    "Question");
                view.Decide(UiDialogResult.Confirmed);
                while (!confirmed.IsCompleted)
                {
                    yield return null;
                }

                Assert.That(confirmed.Result, Is.True);
            }
        }

        [Test]
        public void Dispose_DismissesAllEntriesDetachesViewAndRejectsNewWork()
        {
            var view = new RecordingView();
            var queue = new UiDialogQueue(view);
            Task<UiDialogResult> active = queue.ShowNoticeAsync(
                "active", "Active", "");
            Task<UiDialogResult> pending = queue.ShowNoticeAsync(
                "pending", "Pending", "");

            queue.Dispose();
            view.Decide(UiDialogResult.Acknowledged);

            Assert.That(active.Result, Is.EqualTo(UiDialogResult.Dismissed));
            Assert.That(pending.Result, Is.EqualTo(UiDialogResult.Dismissed));
            Assert.That(view.HideCount, Is.EqualTo(1));
            Assert.That(view.ShowCount, Is.EqualTo(1));
            Assert.Throws<ObjectDisposedException>(() =>
                queue.ShowNoticeAsync("later", "Later", ""));
        }

        private static string[] Ids(
            IReadOnlyList<UiDialogRequest> requests)
        {
            var ids = new string[requests.Count];
            for (int index = 0; index < requests.Count; index++)
            {
                ids[index] = requests[index].Id;
            }

            return ids;
        }

        private sealed class RecordingView : IUiDialogView
        {
            public event Action<UiDialogResult> DecisionRequested;

            public UiDialogRequest VisibleRequest { get; private set; }

            public int ShowCount { get; private set; }

            public int HideCount { get; private set; }

            public void Show(UiDialogRequest request)
            {
                VisibleRequest = request;
                ShowCount++;
            }

            public void Hide()
            {
                VisibleRequest = null;
                HideCount++;
            }

            public void Decide(UiDialogResult result)
            {
                DecisionRequested?.Invoke(result);
            }
        }
    }
}
