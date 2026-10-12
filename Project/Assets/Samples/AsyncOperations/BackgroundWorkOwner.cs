using System;
using System.Diagnostics;
using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;
using UnityCoreLab.AsyncOperations;

namespace UnityCoreLab.Samples.AsyncOperations
{
    /// <summary>
    /// Moves pure CPU work away from Unity's main thread and returns to the PlayerLoop
    /// before publishing the result. No UnityEngine API is used on the worker thread.
    /// </summary>
    public sealed class BackgroundWorkOwner : MonoBehaviour
    {
        private int _workerStarted;
        private int _workerThreadId = -1;

        public bool WorkerStarted => Volatile.Read(ref _workerStarted) != 0;

        public int WorkerThreadId => Volatile.Read(ref _workerThreadId);

        public async UniTask<BackgroundWorkResult> CalculateAsync(
            int iterations,
            CancellationToken cancellationToken = default)
        {
            if (iterations <= 0)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(iterations),
                    "Iteration count must be positive.");
            }

            int startedThreadId = Thread.CurrentThread.ManagedThreadId;
            int startedFrame = Time.frameCount;
            int workerThreadId = startedThreadId;
            long checksum = 17;
            var stopwatch = Stopwatch.StartNew();

            using CancellationTokenSource linkedCancellation =
                CancellationTokenSource.CreateLinkedTokenSource(
                    destroyCancellationToken,
                    cancellationToken);
            CancellationToken linkedToken = linkedCancellation.Token;

            Volatile.Write(ref _workerStarted, 0);
            Volatile.Write(ref _workerThreadId, -1);

            try
            {
#if UNITY_WEBGL && !UNITY_EDITOR
                Volatile.Write(ref _workerStarted, 1);
                Volatile.Write(ref _workerThreadId, startedThreadId);
                checksum = await CalculateAcrossFramesAsync(
                    iterations,
                    checksum,
                    linkedToken);
                bool usedBackgroundThread = false;
#else
                await UniTask.SwitchToThreadPool();
                workerThreadId = Thread.CurrentThread.ManagedThreadId;
                Volatile.Write(ref _workerThreadId, workerThreadId);
                Volatile.Write(ref _workerStarted, 1);

                checksum = CalculateChecksum(iterations, checksum, linkedToken);

                await UniTask.SwitchToMainThread(
                    PlayerLoopTiming.Update,
                    linkedToken);
                bool usedBackgroundThread = workerThreadId != startedThreadId;
#endif

                stopwatch.Stop();
                return new BackgroundWorkResult(
                    OperationState.Completed,
                    checksum,
                    startedThreadId,
                    workerThreadId,
                    Thread.CurrentThread.ManagedThreadId,
                    startedFrame,
                    Time.frameCount,
                    stopwatch.Elapsed,
                    usedBackgroundThread);
            }
            catch (OperationCanceledException) when (linkedToken.IsCancellationRequested)
            {
                // Do not pass the already-cancelled token: reporting still needs to return
                // to Unity's main thread.
                await UniTask.SwitchToMainThread(PlayerLoopTiming.Update);
                stopwatch.Stop();

                return new BackgroundWorkResult(
                    OperationState.Cancelled,
                    checksum,
                    startedThreadId,
                    workerThreadId,
                    Thread.CurrentThread.ManagedThreadId,
                    startedFrame,
                    Time.frameCount,
                    stopwatch.Elapsed,
                    workerThreadId != startedThreadId);
            }
            finally
            {
                Volatile.Write(ref _workerStarted, 0);
            }
        }

        private static long CalculateChecksum(
            int iterations,
            long seed,
            CancellationToken cancellationToken)
        {
            long checksum = seed;
            unchecked
            {
                for (int index = 0; index < iterations; index++)
                {
                    checksum = ((checksum << 5) + checksum) ^ index;

                    if ((index & 0x3FFF) == 0)
                    {
                        cancellationToken.ThrowIfCancellationRequested();
                    }
                }
            }

            return checksum;
        }

#if UNITY_WEBGL && !UNITY_EDITOR
        private static async UniTask<long> CalculateAcrossFramesAsync(
            int iterations,
            long seed,
            CancellationToken cancellationToken)
        {
            const int iterationsPerFrame = 20000;
            long checksum = seed;

            unchecked
            {
                for (int index = 0; index < iterations; index++)
                {
                    checksum = ((checksum << 5) + checksum) ^ index;

                    if (index > 0 && index % iterationsPerFrame == 0)
                    {
                        await UniTask.Yield(
                            PlayerLoopTiming.Update,
                            cancellationToken);
                    }
                }
            }

            cancellationToken.ThrowIfCancellationRequested();
            return checksum;
        }
#endif
    }
}
