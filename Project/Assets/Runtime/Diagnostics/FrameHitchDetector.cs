using System;

namespace UnityCoreLab.Diagnostics
{
    /// <summary>
    /// Detects slow frames from an externally supplied monotonic clock and
    /// coalesces repeated reports during a cooldown window.
    /// </summary>
    public sealed class FrameHitchDetector
    {
        public const string Category = "FrameTiming";
        public const string WarningMessage = "Frame time exceeded warning threshold.";
        public const string CriticalMessage = "Frame time exceeded critical threshold.";

        private readonly RuntimeDiagnosticBuffer _events;
        private readonly double _warningThresholdMilliseconds;
        private readonly double _criticalThresholdMilliseconds;
        private readonly double _reportCooldownSeconds;
        private bool _hasObservedTimestamp;
        private double _lastObservedTimestamp;
        private double _nextReportTimestamp;
        private int _pendingSuppressedCount;
        private DiagnosticSeverity _lastReportedSeverity;

        public FrameHitchDetector(
            RuntimeDiagnosticBuffer events,
            double warningThresholdMilliseconds,
            double criticalThresholdMilliseconds,
            double reportCooldownSeconds)
        {
            _events = events ?? throw new ArgumentNullException(nameof(events));
            ValidatePositiveFinite(
                warningThresholdMilliseconds,
                nameof(warningThresholdMilliseconds));
            ValidatePositiveFinite(
                criticalThresholdMilliseconds,
                nameof(criticalThresholdMilliseconds));
            if (criticalThresholdMilliseconds <= warningThresholdMilliseconds)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(criticalThresholdMilliseconds));
            }

            if (reportCooldownSeconds < 0d ||
                double.IsNaN(reportCooldownSeconds) ||
                double.IsInfinity(reportCooldownSeconds))
            {
                throw new ArgumentOutOfRangeException(
                    nameof(reportCooldownSeconds));
            }

            _warningThresholdMilliseconds = warningThresholdMilliseconds;
            _criticalThresholdMilliseconds = criticalThresholdMilliseconds;
            _reportCooldownSeconds = reportCooldownSeconds;
        }

        public long DetectedHitchCount { get; private set; }

        public long SuppressedReportCount { get; private set; }

        public double WorstFrameMilliseconds { get; private set; }

        public bool ObserveFrame(
            double frameDurationMilliseconds,
            double timestampSeconds)
        {
            ValidateNonNegativeFinite(
                frameDurationMilliseconds,
                nameof(frameDurationMilliseconds));
            ValidateNonNegativeFinite(timestampSeconds, nameof(timestampSeconds));
            if (_hasObservedTimestamp && timestampSeconds < _lastObservedTimestamp)
            {
                throw new ArgumentException(
                    "The observation timestamp must be monotonic.",
                    nameof(timestampSeconds));
            }

            _hasObservedTimestamp = true;
            _lastObservedTimestamp = timestampSeconds;

            if (frameDurationMilliseconds < _warningThresholdMilliseconds)
            {
                return false;
            }

            DetectedHitchCount++;
            WorstFrameMilliseconds = Math.Max(
                WorstFrameMilliseconds,
                frameDurationMilliseconds);

            bool critical =
                frameDurationMilliseconds >= _criticalThresholdMilliseconds;
            DiagnosticSeverity severity = critical
                ? DiagnosticSeverity.Critical
                : DiagnosticSeverity.Warning;
            bool severityEscalated = severity > _lastReportedSeverity;
            if (timestampSeconds < _nextReportTimestamp && !severityEscalated)
            {
                _pendingSuppressedCount++;
                SuppressedReportCount++;
                return false;
            }

            _events.Add(
                timestampSeconds,
                Category,
                severity,
                critical ? CriticalMessage : WarningMessage,
                frameDurationMilliseconds,
                _pendingSuppressedCount + 1);
            _pendingSuppressedCount = 0;
            _nextReportTimestamp = timestampSeconds + _reportCooldownSeconds;
            _lastReportedSeverity = severity;
            return true;
        }

        public void ResetStatistics()
        {
            DetectedHitchCount = 0;
            SuppressedReportCount = 0;
            WorstFrameMilliseconds = 0d;
            _pendingSuppressedCount = 0;
            _nextReportTimestamp = 0d;
            _lastObservedTimestamp = 0d;
            _hasObservedTimestamp = false;
            _lastReportedSeverity = DiagnosticSeverity.Info;
        }

        private static void ValidatePositiveFinite(
            double value,
            string parameterName)
        {
            if (value <= 0d || double.IsNaN(value) || double.IsInfinity(value))
            {
                throw new ArgumentOutOfRangeException(parameterName);
            }
        }

        private static void ValidateNonNegativeFinite(
            double value,
            string parameterName)
        {
            if (value < 0d || double.IsNaN(value) || double.IsInfinity(value))
            {
                throw new ArgumentOutOfRangeException(parameterName);
            }
        }
    }
}
