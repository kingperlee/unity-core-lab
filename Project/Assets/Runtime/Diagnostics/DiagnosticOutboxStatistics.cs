namespace UnityCoreLab.Diagnostics
{
    public readonly struct DiagnosticOutboxStatistics
    {
        public DiagnosticOutboxStatistics(
            int capacity,
            int pendingCount,
            bool hasInFlightAttempt,
            long acceptedCount,
            long deliveredCount,
            long terminalFailureCount,
            long rejectedCount,
            long retryScheduledCount)
        {
            Capacity = capacity;
            PendingCount = pendingCount;
            HasInFlightAttempt = hasInFlightAttempt;
            AcceptedCount = acceptedCount;
            DeliveredCount = deliveredCount;
            TerminalFailureCount = terminalFailureCount;
            RejectedCount = rejectedCount;
            RetryScheduledCount = retryScheduledCount;
        }

        public int Capacity { get; }

        public int PendingCount { get; }

        public bool HasInFlightAttempt { get; }

        public long AcceptedCount { get; }

        public long DeliveredCount { get; }

        public long TerminalFailureCount { get; }

        public long RejectedCount { get; }

        public long RetryScheduledCount { get; }
    }
}
