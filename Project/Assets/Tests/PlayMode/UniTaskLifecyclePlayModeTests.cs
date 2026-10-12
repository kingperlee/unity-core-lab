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
    public sealed class UniTaskLifecyclePlayModeTests
    {
        [UnityTest]
        public IEnumerator Delay_CompletesOnUnityPlayerLoopAndMainThread()
        {
            var gameObject = new GameObject("UniTask PlayerLoop Test");
            var owner = gameObject.AddComponent<UniTaskLifecycleOwner>();
            int mainThreadId = Thread.CurrentThread.ManagedThreadId;

            Task<OperationResult> running = owner
                .RunDelayAsync(TimeSpan.FromMilliseconds(30))
                .AsTask();

            yield return Await(running);

            Assert.That(running.Result.State, Is.EqualTo(OperationState.Completed));
            Assert.That(owner.StartedThreadId, Is.EqualTo(mainThreadId));
            Assert.That(owner.ContinuationThreadId, Is.EqualTo(mainThreadId));
            Assert.That(owner.ContinuationFrame, Is.GreaterThan(owner.StartedFrame));

            UnityEngine.Object.Destroy(gameObject);
            yield return null;
        }

        [UnityTest]
        public IEnumerator DestroyingOwner_CancelsLifetimeBoundUniTask()
        {
            var gameObject = new GameObject("UniTask Lifetime Cancellation Test");
            var owner = gameObject.AddComponent<UniTaskLifecycleOwner>();
            Task<OperationResult> running = owner
                .RunDelayWithTimeoutAsync(
                    TimeSpan.FromSeconds(30),
                    TimeSpan.FromSeconds(30))
                .AsTask();

            yield return null;
            UnityEngine.Object.Destroy(gameObject);
            yield return Await(running);

            Assert.That(running.Result.State, Is.EqualTo(OperationState.Cancelled));
        }

        [UnityTest]
        public IEnumerator PlayerLoopTimeout_IsClassifiedSeparatelyFromCancellation()
        {
            var gameObject = new GameObject("UniTask Timeout Test");
            var owner = gameObject.AddComponent<UniTaskLifecycleOwner>();
            Task<OperationResult> running = owner
                .RunDelayWithTimeoutAsync(
                    TimeSpan.FromSeconds(30),
                    TimeSpan.FromMilliseconds(30))
                .AsTask();

            yield return Await(running);

            Assert.That(running.Result.State, Is.EqualTo(OperationState.TimedOut));
            Assert.That(owner.ContinuationFrame, Is.GreaterThan(owner.StartedFrame));

            UnityEngine.Object.Destroy(gameObject);
            yield return null;
        }

        [UnityTest]
        public IEnumerator ForgetHandler_ObservesFireAndForgetException()
        {
            var gameObject = new GameObject("UniTask Forget Exception Test");
            var owner = gameObject.AddComponent<UniTaskLifecycleOwner>();
            Exception observed = null;
            int callbackThreadId = -1;

            owner.RunFaultAndForget(exception =>
            {
                observed = exception;
                callbackThreadId = Thread.CurrentThread.ManagedThreadId;
            });

            double deadline = Time.realtimeSinceStartupAsDouble + 2d;
            while (observed == null && Time.realtimeSinceStartupAsDouble < deadline)
            {
                yield return null;
            }

            Assert.That(observed, Is.TypeOf<InvalidOperationException>());
            Assert.That(observed.Message, Is.EqualTo("Simulated UniTask failure."));
            Assert.That(callbackThreadId, Is.EqualTo(Thread.CurrentThread.ManagedThreadId));

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
                "UniTask did not finish within 5 real-time seconds.");

            if (task.IsFaulted)
            {
                Exception failure = task.Exception;
                throw failure ?? new InvalidOperationException("Task failed without an exception.");
            }
        }
    }
}
