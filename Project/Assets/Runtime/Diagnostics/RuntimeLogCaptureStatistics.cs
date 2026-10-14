namespace UnityCoreLab.Diagnostics
{
    public readonly struct RuntimeLogCaptureStatistics
    {
        public RuntimeLogCaptureStatistics(
            int capacity,
            int pendingCount,
            long receivedCount,
            long drainedCount,
            long droppedCount,
            long truncatedCount)
        {
            Capacity = capacity;
            PendingCount = pendingCount;
            ReceivedCount = receivedCount;
            DrainedCount = drainedCount;
            DroppedCount = droppedCount;
            TruncatedCount = truncatedCount;
        }

        public int Capacity { get; }

        public int PendingCount { get; }

        public long ReceivedCount { get; }

        public long DrainedCount { get; }

        public long DroppedCount { get; }

        public long TruncatedCount { get; }
    }
}
