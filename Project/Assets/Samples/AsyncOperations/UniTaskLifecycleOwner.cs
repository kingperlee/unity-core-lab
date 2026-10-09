using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;
using UnityCoreLab.AsyncOperations;

namespace UnityCoreLab.Samples.AsyncOperations
{
    /// <summary>
    /// Owns UniTask work whose cancellation lifetime matches this GameObject.
    /// </summary>
    public sealed class UniTaskLifecycleOwner : MonoBehaviour
    {
        public int StartedThreadId { get; private set; }

        public int ContinuationThreadId { get; private set; }

        public int StartedFrame { get; private set; }

        public int ContinuationFrame { get; private set; }

        public UniTask<OperationResult> RunDelayAsync(TimeSpan delay)
        {
            // Unity 2022.2+ provides this token directly. On older Unity versions,
            // UniTask's GetCancellationTokenOnDestroy() serves the same purpose.
            CancellationToken lifetimeCancellationToken = destroyCancellationToken;
            return RunDelayCoreAsync(
                delay,
                lifetimeCancellationToken,
                lifetimeCancellationToken,
                CancellationToken.None);
        }

        public async UniTask<OperationResult> RunDelayWithTimeoutAsync(
            TimeSpan delay,
            TimeSpan timeout)
        {
            CancellationToken lifetimeCancellationToken = destroyCancellationToken;
            using var timeoutCancellation = new CancellationTokenSource();
            using IDisposable timeoutRegistration = timeoutCancellation.CancelAfterSlim(
                timeout,
                DelayType.Realtime,
                PlayerLoopTiming.Update);
            using CancellationTokenSource linkedCancellation =
                CancellationTokenSource.CreateLinkedTokenSource(
                    lifetimeCancellationToken,
                    timeoutCancellation.Token);

            return await RunDelayCoreAsync(
                delay,
                linkedCancellation.Token,
                lifetimeCancellationToken,
                timeoutCancellation.Token);
        }

        private async UniTask<OperationResult> RunDelayCoreAsync(
            TimeSpan delay,
            CancellationToken cancellationToken,
            CancellationToken lifetimeCancellationToken,
            CancellationToken timeoutCancellationToken)
        {
            StartedThreadId = Thread.CurrentThread.ManagedThreadId;
            StartedFrame = Time.frameCount;

            try
            {
                await UniTask.Delay(
                    delay,
                    ignoreTimeScale: true,
                    delayTiming: PlayerLoopTiming.Update,
                    cancellationToken: cancellationToken);

                ContinuationThreadId = Thread.CurrentThread.ManagedThreadId;
                ContinuationFrame = Time.frameCount;
                return OperationResult.Completed();
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                ContinuationThreadId = Thread.CurrentThread.ManagedThreadId;
                ContinuationFrame = Time.frameCount;

                bool timedOut =
                    timeoutCancellationToken.CanBeCanceled &&
                    timeoutCancellationToken.IsCancellationRequested &&
                    !lifetimeCancellationToken.IsCancellationRequested;

                return timedOut
                    ? OperationResult.TimedOut()
                    : OperationResult.Cancelled();
            }
        }

        public void RunFaultAndForget(Action<Exception> exceptionHandler)
        {
            if (exceptionHandler == null)
            {
                throw new ArgumentNullException(nameof(exceptionHandler));
            }

            ThrowAfterYieldAsync(destroyCancellationToken).Forget(exceptionHandler);
        }

        private static async UniTask ThrowAfterYieldAsync(
            CancellationToken cancellationToken)
        {
            await UniTask.Yield(PlayerLoopTiming.Update);
            cancellationToken.ThrowIfCancellationRequested();
            throw new InvalidOperationException("Simulated UniTask failure.");
        }
    }
}
