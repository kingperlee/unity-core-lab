using System;
using System.Collections;
using System.Threading;
using System.Threading.Tasks;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using UnityCoreLab.AsyncOperations;
using UnityCoreLab.Samples.AsyncOperations;

namespace UnityCoreLab.Tests.PlayMode
{
    public sealed class AsyncOperationDemoPlayModeTests
    {
        [UnityTest]
        public IEnumerator Demo_InitializesRuntimeStateAndVisuals()
        {
            var gameObject = new GameObject("Async Operation Demo Test");
            var demo = gameObject.AddComponent<AsyncOperationDemo>();

            yield return null;

            Assert.That(demo.IsInitialized, Is.True);
            Assert.That(demo.Current.State, Is.EqualTo(OperationState.Idle));
            Assert.That(gameObject.transform.childCount, Is.EqualTo(2));

            UnityEngine.Object.Destroy(gameObject);
            yield return null;
        }

        [UnityTest]
        public IEnumerator DestroyingDemo_CancelsActiveWorkAndInvalidatesItsResult()
        {
            var gameObject = new GameObject("Async Operation Demo Cancellation Test");
            var demo = gameObject.AddComponent<AsyncOperationDemo>();
            var operation = new CancellationProbeOperation();

            yield return null;

            Task<OperationResult> running = demo.RunOperationAsync(
                operation,
                Timeout.InfiniteTimeSpan);

            yield return Await(operation.Started);
            UnityEngine.Object.Destroy(gameObject);
            yield return Await(running);

            Assert.That(operation.CancellationWasObserved, Is.True);
            Assert.That(running.Result.State, Is.EqualTo(OperationState.Superseded));
        }

        private static IEnumerator Await(Task task)
        {
            while (!task.IsCompleted)
            {
                yield return null;
            }

            if (task.IsFaulted)
            {
                Exception failure = task.Exception;
                throw failure ?? new InvalidOperationException("Task failed without an exception.");
            }
        }

        private sealed class CancellationProbeOperation : IAsyncOperation
        {
            private readonly TaskCompletionSource<bool> _started = new TaskCompletionSource<bool>();

            public Task Started => _started.Task;

            public bool CancellationWasObserved { get; private set; }

            public async Task<OperationResult> ExecuteAsync(
                OperationContext context,
                CancellationToken cancellationToken)
            {
                _started.TrySetResult(true);
                try
                {
                    await Task.Delay(Timeout.Infinite, cancellationToken);
                }
                catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
                {
                    CancellationWasObserved = true;
                    throw;
                }

                return OperationResult.Completed();
            }
        }
    }
}
