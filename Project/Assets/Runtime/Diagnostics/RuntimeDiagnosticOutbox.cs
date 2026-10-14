using System;

namespace UnityCoreLab.Diagnostics
{
    /// <summary>
    /// Bounded FIFO delivery state machine. Transport code explicitly begins
    /// and completes one attempt at a time; no networking occurs here.
    /// </summary>
    public sealed class RuntimeDiagnosticOutbox
    {
        private struct PendingReport
        {
            public long ReportId;
            public string Payload;
            public int AttemptCount;
            public double NextAttemptTimestamp;
            public double AttemptStartedTimestamp;
            public bool InFlight;
        }

        private readonly PendingReport[] _reports;
        private readonly DiagnosticUploadPolicy _policy;
        private readonly int _maximumPayloadCharacters;
        private int _startIndex;
        private int _count;
        private long _nextReportId = 1;
        private long _acceptedCount;
        private long _deliveredCount;
        private long _terminalFailureCount;
        private long _rejectedCount;
        private long _retryScheduledCount;

        public RuntimeDiagnosticOutbox(
            int capacity,
            int maximumPayloadCharacters,
            DiagnosticUploadPolicy policy)
        {
            if (capacity <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(capacity));
            }

            if (maximumPayloadCharacters <= 0)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(maximumPayloadCharacters));
            }

            _policy = policy ?? throw new ArgumentNullException(nameof(policy));
            _reports = new PendingReport[capacity];
            _maximumPayloadCharacters = maximumPayloadCharacters;
        }

        public DiagnosticEnqueueResult Enqueue(
            string payload,
            double timestampSeconds,
            out long reportId)
        {
            ValidateTimestamp(timestampSeconds, nameof(timestampSeconds));
            if (string.IsNullOrWhiteSpace(payload))
            {
                throw new ArgumentException(
                    "A diagnostic report payload is required.",
                    nameof(payload));
            }

            reportId = 0;
            if (payload.Length > _maximumPayloadCharacters)
            {
                _rejectedCount++;
                return DiagnosticEnqueueResult.PayloadTooLarge;
            }

            if (_count == _reports.Length)
            {
                _rejectedCount++;
                return DiagnosticEnqueueResult.OutboxFull;
            }

            reportId = _nextReportId++;
            int writeIndex = (_startIndex + _count) % _reports.Length;
            _reports[writeIndex] = new PendingReport
            {
                ReportId = reportId,
                Payload = payload,
                AttemptCount = 0,
                NextAttemptTimestamp = timestampSeconds,
                AttemptStartedTimestamp = 0d,
                InFlight = false
            };
            _count++;
            _acceptedCount++;
            return DiagnosticEnqueueResult.Accepted;
        }

        public bool TryBeginNextAttempt(
            double timestampSeconds,
            out DiagnosticUploadAttempt attempt)
        {
            ValidateTimestamp(timestampSeconds, nameof(timestampSeconds));
            if (_count == 0)
            {
                attempt = default;
                return false;
            }

            PendingReport report = _reports[_startIndex];
            if (report.InFlight || timestampSeconds < report.NextAttemptTimestamp)
            {
                attempt = default;
                return false;
            }

            report.AttemptCount++;
            report.AttemptStartedTimestamp = timestampSeconds;
            report.InFlight = true;
            _reports[_startIndex] = report;
            attempt = new DiagnosticUploadAttempt(
                report.ReportId,
                report.AttemptCount,
                report.Payload);
            return true;
        }

        public void CompleteAttempt(
            long reportId,
            DiagnosticDeliveryOutcome outcome,
            double timestampSeconds)
        {
            ValidateTimestamp(timestampSeconds, nameof(timestampSeconds));
            if (!Enum.IsDefined(typeof(DiagnosticDeliveryOutcome), outcome))
            {
                throw new ArgumentOutOfRangeException(nameof(outcome));
            }

            if (_count == 0)
            {
                throw new InvalidOperationException(
                    "There is no pending diagnostic report.");
            }

            PendingReport report = _reports[_startIndex];
            if (!report.InFlight || report.ReportId != reportId)
            {
                throw new InvalidOperationException(
                    "The completion does not match the in-flight attempt.");
            }

            if (timestampSeconds < report.AttemptStartedTimestamp)
            {
                throw new ArgumentException(
                    "Completion time cannot precede attempt start time.",
                    nameof(timestampSeconds));
            }

            if (outcome == DiagnosticDeliveryOutcome.Succeeded)
            {
                _deliveredCount++;
                RemoveHead();
                return;
            }

            bool attemptsExhausted =
                report.AttemptCount >= _policy.MaximumAttempts;
            if (outcome == DiagnosticDeliveryOutcome.PermanentFailure ||
                attemptsExhausted)
            {
                _terminalFailureCount++;
                RemoveHead();
                return;
            }

            report.InFlight = false;
            report.NextAttemptTimestamp = timestampSeconds +
                _policy.GetRetryDelaySeconds(report.AttemptCount);
            _reports[_startIndex] = report;
            _retryScheduledCount++;
        }

        public bool TryGetDelayUntilNextAttempt(
            double timestampSeconds,
            out double delaySeconds)
        {
            ValidateTimestamp(timestampSeconds, nameof(timestampSeconds));
            if (_count == 0 || _reports[_startIndex].InFlight)
            {
                delaySeconds = 0d;
                return false;
            }

            delaySeconds = Math.Max(
                0d,
                _reports[_startIndex].NextAttemptTimestamp -
                timestampSeconds);
            return true;
        }

        public DiagnosticOutboxStatistics GetStatistics()
        {
            bool hasInFlight =
                _count > 0 && _reports[_startIndex].InFlight;
            return new DiagnosticOutboxStatistics(
                _reports.Length,
                _count,
                hasInFlight,
                _acceptedCount,
                _deliveredCount,
                _terminalFailureCount,
                _rejectedCount,
                _retryScheduledCount);
        }

        public void Clear()
        {
            Array.Clear(_reports, 0, _reports.Length);
            _startIndex = 0;
            _count = 0;
            _acceptedCount = 0;
            _deliveredCount = 0;
            _terminalFailureCount = 0;
            _rejectedCount = 0;
            _retryScheduledCount = 0;
        }

        private void RemoveHead()
        {
            _reports[_startIndex] = default;
            _startIndex = (_startIndex + 1) % _reports.Length;
            _count--;
        }

        private static void ValidateTimestamp(
            double timestampSeconds,
            string parameterName)
        {
            if (timestampSeconds < 0d ||
                double.IsNaN(timestampSeconds) ||
                double.IsInfinity(timestampSeconds))
            {
                throw new ArgumentOutOfRangeException(parameterName);
            }
        }
    }
}
