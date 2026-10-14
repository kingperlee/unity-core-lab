using System;

namespace UnityCoreLab.Diagnostics
{
    /// <summary>
    /// Point-in-time copy of bounded diagnostic state. Snapshot allocation is
    /// explicit and is never performed by the per-frame observation path.
    /// </summary>
    public sealed class RuntimeDiagnosticReport
    {
        public const int CurrentSchemaVersion = 1;

        private readonly RuntimeDiagnosticEvent[] _events;

        private RuntimeDiagnosticReport(
            DateTime generatedAtUtc,
            double capturedAtSeconds,
            RuntimeDiagnosticContext context,
            long detectedHitchCount,
            long suppressedReportCount,
            double worstFrameMilliseconds,
            ManagedMemoryStatistics memoryStatistics,
            RuntimeLogCaptureStatistics logCaptureStatistics,
            int eventCapacity,
            long overwrittenEventCount,
            RuntimeDiagnosticEvent[] events)
        {
            GeneratedAtUtc = generatedAtUtc;
            CapturedAtSeconds = capturedAtSeconds;
            Context = context;
            DetectedHitchCount = detectedHitchCount;
            SuppressedReportCount = suppressedReportCount;
            WorstFrameMilliseconds = worstFrameMilliseconds;
            MemoryStatistics = memoryStatistics;
            LogCaptureStatistics = logCaptureStatistics;
            EventCapacity = eventCapacity;
            OverwrittenEventCount = overwrittenEventCount;
            _events = events;
        }

        public int SchemaVersion => CurrentSchemaVersion;

        public DateTime GeneratedAtUtc { get; }

        public double CapturedAtSeconds { get; }

        public RuntimeDiagnosticContext Context { get; }

        public long DetectedHitchCount { get; }

        public long SuppressedReportCount { get; }

        public double WorstFrameMilliseconds { get; }

        public ManagedMemoryStatistics MemoryStatistics { get; }

        public RuntimeLogCaptureStatistics LogCaptureStatistics { get; }

        public int EventCapacity { get; }

        public long OverwrittenEventCount { get; }

        public int EventCount => _events.Length;

        public RuntimeDiagnosticEvent GetEvent(int chronologicalIndex)
        {
            if (chronologicalIndex < 0 || chronologicalIndex >= _events.Length)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(chronologicalIndex));
            }

            return _events[chronologicalIndex];
        }

        public static RuntimeDiagnosticReport Capture(
            RuntimeDiagnosticBuffer events,
            FrameHitchDetector hitchDetector,
            RuntimeDiagnosticContext context,
            DateTime generatedAtUtc,
            double capturedAtSeconds,
            ManagedMemoryMonitor memoryMonitor = null,
            RuntimeLogInbox logInbox = null)
        {
            if (events == null)
            {
                throw new ArgumentNullException(nameof(events));
            }

            if (hitchDetector == null)
            {
                throw new ArgumentNullException(nameof(hitchDetector));
            }

            if (context == null)
            {
                throw new ArgumentNullException(nameof(context));
            }

            if (capturedAtSeconds < 0d ||
                double.IsNaN(capturedAtSeconds) ||
                double.IsInfinity(capturedAtSeconds))
            {
                throw new ArgumentOutOfRangeException(
                    nameof(capturedAtSeconds));
            }

            DateTime utc = generatedAtUtc.Kind == DateTimeKind.Utc
                ? generatedAtUtc
                : generatedAtUtc.ToUniversalTime();
            var eventSnapshot = new RuntimeDiagnosticEvent[events.Count];
            events.CopyTo(eventSnapshot);

            return new RuntimeDiagnosticReport(
                utc,
                capturedAtSeconds,
                context,
                hitchDetector.DetectedHitchCount,
                hitchDetector.SuppressedReportCount,
                hitchDetector.WorstFrameMilliseconds,
                new ManagedMemoryStatistics(memoryMonitor),
                logInbox != null
                    ? logInbox.GetStatistics()
                    : default,
                events.Capacity,
                events.OverwrittenCount,
                eventSnapshot);
        }
    }
}
