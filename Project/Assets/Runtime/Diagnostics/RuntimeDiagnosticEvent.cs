namespace UnityCoreLab.Diagnostics
{
    public readonly struct RuntimeDiagnosticEvent
    {
        public RuntimeDiagnosticEvent(
            long sequence,
            double timestampSeconds,
            string category,
            DiagnosticSeverity severity,
            string message,
            double durationMilliseconds,
            int occurrenceCount)
        {
            Sequence = sequence;
            TimestampSeconds = timestampSeconds;
            Category = category;
            Severity = severity;
            Message = message;
            DurationMilliseconds = durationMilliseconds;
            OccurrenceCount = occurrenceCount;
        }

        public long Sequence { get; }

        public double TimestampSeconds { get; }

        public string Category { get; }

        public DiagnosticSeverity Severity { get; }

        public string Message { get; }

        public double DurationMilliseconds { get; }

        public int OccurrenceCount { get; }
    }
}
