using System;
using System.Collections;
using System.Threading;
using System.Threading.Tasks;
using NUnit.Framework;
using UnityEngine.TestTools;
using UnityCoreLab.AsyncOperations;

namespace UnityCoreLab.Tests.EditMode
{
    public sealed class AsyncOperationCoordinatorTests
    {
        [UnityTest]
        public IEnumerator CompletedOperation_PublishesCompletedState()
        {
            using (var coordinator = new AsyncOperationCoordinator())
            {
                Task<OperationResult> running = coordinator.RunAsync(
                    new ControlledOperation(Task.FromResult(OperationResult.Completed())),
                    TimeSpan.FromSeconds(1));
                yield return Await(running);

                OperationResult result = running.Result;
                Assert.That(result.State, Is.EqualTo(OperationState.Completed));
                Assert.That(coordinator.Current.State, Is.EqualTo(OperationState.Completed));
            }
        }

        [UnityTest]
        public IEnumerator CancelActive_ReturnsCancelled()
        {
            using (var coordinator = new AsyncOperationCoordinator())
            {
                var operation = new WaitingOperation();
                Task<OperationResult> running = coordinator.RunAsync(
                    operation,
                    Timeout.InfiniteTimeSpan);

                yield return Await(operation.Started);
                coordinator.CancelActive();
                yield return Await(running);

                OperationResult result = running.Result;
                Assert.That(result.State, Is.EqualTo(OperationState.Cancelled));
                Assert.That(coordinator.Current.State, Is.EqualTo(OperationState.Cancelled));
            }
        }

        [UnityTest]
        public IEnumerator Timeout_ReturnsTimedOut()
        {
            using (var coordinator = new AsyncOperationCoordinator())
            {
                Task<OperationResult> running = coordinator.RunAsync(
                    new WaitingOperation(),
                    TimeSpan.FromMilliseconds(20));
                yield return Await(running);

                OperationResult result = running.Result;
                Assert.That(result.State, Is.EqualTo(OperationState.TimedOut));
                Assert.That(coordinator.Current.State, Is.EqualTo(OperationState.TimedOut));
            }
        }

        [UnityTest]
        public IEnumerator Exception_ReturnsFailedWithoutEscapingCoordinator()
        {
            using (var coordinator = new AsyncOperationCoordinator())
            {
                Task<OperationResult> running = coordinator.RunAsync(
                    new ThrowingOperation(),
                    TimeSpan.FromSeconds(1));
                yield return Await(running);

                OperationResult result = running.Result;
                Assert.That(result.State, Is.EqualTo(OperationState.Failed));
                Assert.That(result.Error, Does.Contain("expected failure"));
            }
        }

        [UnityTest]
        public IEnumerator NewOperation_SupersedesOldOperation()
        {
            using (var coordinator = new AsyncOperationCoordinator())
            {
                var oldOperation = new WaitingOperation();
                Task<OperationResult> oldTask = coordinator.RunAsync(
                    oldOperation,
                    Timeout.InfiniteTimeSpan);

                yield return Await(oldOperation.Started);

                Task<OperationResult> newTask = coordinator.RunAsync(
                    new ControlledOperation(Task.FromResult(OperationResult.Completed())),
                    TimeSpan.FromSeconds(1));
                yield return Await(newTask);
                yield return Await(oldTask);

                OperationResult newResult = newTask.Result;
                OperationResult oldResult = oldTask.Result;

                Assert.That(newResult.State, Is.EqualTo(OperationState.Completed));
                Assert.That(oldResult.State, Is.EqualTo(OperationState.Superseded));
                Assert.That(coordinator.Current.State, Is.EqualTo(OperationState.Completed));
                Assert.That(coordinator.Current.Version, Is.EqualTo(2));
            }
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

        private sealed class ControlledOperation : IAsyncOperation
        {
            private readonly Task<OperationResult> _result;

            public ControlledOperation(Task<OperationResult> result)
            {
                _result = result;
            }

            public Task<OperationResult> ExecuteAsync(
                OperationContext context,
                CancellationToken cancellationToken)
            {
                return _result;
            }
        }

        private sealed class WaitingOperation : IAsyncOperation
        {
            private readonly TaskCompletionSource<bool> _started =
                new TaskCompletionSource<bool>();

            public Task Started => _started.Task;

            public async Task<OperationResult> ExecuteAsync(
                OperationContext context,
                CancellationToken cancellationToken)
            {
                _started.TrySetResult(true);
                await Task.Delay(Timeout.Infinite, cancellationToken);
                return OperationResult.Completed();
            }
        }

        private sealed class ThrowingOperation : IAsyncOperation
        {
            public Task<OperationResult> ExecuteAsync(
                OperationContext context,
                CancellationToken cancellationToken)
            {
                throw new InvalidOperationException("expected failure");
            }
        }
    }
}
