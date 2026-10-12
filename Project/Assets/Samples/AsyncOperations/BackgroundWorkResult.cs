using System;
using UnityCoreLab.AsyncOperations;

namespace UnityCoreLab.Samples.AsyncOperations
{
    public readonly struct BackgroundWorkResult
    {
        public BackgroundWorkResult(
            OperationState state,
            long checksum,
            int startedThreadId,
            int workerThreadId,
            int continuationThreadId,
            int startedFrame,
            int continuationFrame,
            TimeSpan elapsed,
            bool usedBackgroundThread)
        {
            State = state;
            Checksum = checksum;
            StartedThreadId = startedThreadId;
            WorkerThreadId = workerThreadId;
            ContinuationThreadId = continuationThreadId;
            StartedFrame = startedFrame;
            ContinuationFrame = continuationFrame;
            Elapsed = elapsed;
            UsedBackgroundThread = usedBackgroundThread;
        }

        public OperationState State { get; }
        public long Checksum { get; }
        public int StartedThreadId { get; }
        public int WorkerThreadId { get; }
        public int ContinuationThreadId { get; }
        public int StartedFrame { get; }
        public int ContinuationFrame { get; }
        public TimeSpan Elapsed { get; }
        public bool UsedBackgroundThread { get; }
    }
}
