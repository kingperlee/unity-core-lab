using System;
using System.Collections;
using System.Threading;
using System.Threading.Tasks;
using Cysharp.Threading.Tasks;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using UnityCoreLab.AsyncOperations;
using UnityCoreLab.Samples.AsyncOperations;

namespace UnityCoreLab.Tests.PlayMode
{
    public sealed class BackgroundWorkPlayModeTests
    {
        [UnityTest]
        public IEnumerator Calculation_UsesWorkerAndReturnsToUnityMainThread()
        {
            var gameObject = new GameObject("Background Work Thread Handoff Test");
            var owner = gameObject.AddComponent<BackgroundWorkOwner>();
            int mainThreadId = Thread.CurrentThread.ManagedThreadId;

            Task<BackgroundWorkResult> running = owner
                .CalculateAsync(200000)
                .AsTask();

            yield return Await(running);

            BackgroundWorkResult result = running.Result;
            Assert.That(result.State, Is.EqualTo(OperationState.Completed));
            Assert.That(result.StartedThreadId, Is.EqualTo(mainThreadId));
            Assert.That(result.WorkerThreadId, Is.Not.EqualTo(mainThreadId));
            Assert.That(result.ContinuationThreadId, Is.EqualTo(mainThreadId));
            Assert.That(result.UsedBackgroundThread, Is.True);
            Assert.That(result.ContinuationFrame, Is.GreaterThanOrEqualTo(result.StartedFrame));

            UnityEngine.Object.Destroy(gameObject);
            yield return null;
        }

        [UnityTest]
        public IEnumerator Cancellation_IsObservedCooperativelyByWorkerLoop()
        {
            var gameObject = new GameObject("Background Work Cancellation Test");
            var owner = gameObject.AddComponent<BackgroundWorkOwner>();
            using var cancellation = new CancellationTokenSource();
            int mainThreadId = Thread.CurrentThread.ManagedThreadId;
            Task<BackgroundWorkResult> running = owner
                .CalculateAsync(500000000, cancellation.Token)
                .AsTask();

            double workerDeadline = Time.realtimeSinceStartupAsDouble + 5d;
            while (!owner.WorkerStarted &&
                   Time.realtimeSinceStartupAsDouble < workerDeadline)
            {
                yield return null;
            }

            Assert.That(owner.WorkerStarted, Is.True);
            cancellation.Cancel();
            yield return Await(running);

            BackgroundWorkResult result = running.Result;
            Assert.That(result.State, Is.EqualTo(OperationState.Cancelled));
            Assert.That(result.WorkerThreadId, Is.Not.EqualTo(mainThreadId));
            Assert.That(result.ContinuationThreadId, Is.EqualTo(mainThreadId));

            UnityEngine.Object.Destroy(gameObject);
            yield return null;
        }

        private static IEnumerator Await(Task task)
        {
            const double maximumSeconds = 5d;
            double deadline = Time.realtimeSinceStartupAsDouble + maximumSeconds;

            while (!task.IsCompleted && Time.realtimeSinceStartupAsDouble < deadline)
            {
                yield return null;
            }

            Assert.That(
                task.IsCompleted,
                Is.True,
                "Background work did not finish within 5 real-time seconds.");

            if (task.IsFaulted)
            {
                Exception failure = task.Exception;
                throw failure ?? new InvalidOperationException("Task failed without an exception.");
            }
        }
    }
}
