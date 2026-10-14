using System;
using System.Globalization;

namespace UnityCoreLab.Diagnostics
{
    /// <summary>
    /// Tracks bounded memory statistics and reports large managed allocation
    /// spikes without allocating on ordinary samples.
    /// </summary>
    public sealed class ManagedMemoryMonitor
    {
        public const string Category = "ManagedMemory";

        private readonly RuntimeDiagnosticBuffer _events;
        private readonly long _warningAllocationBytes;
        private readonly long _criticalAllocationBytes;
        private readonly double _reportCooldownSeconds;
        private bool _hasObservedTimestamp;
        private double _lastObservedTimestamp;
        private double _nextReportTimestamp;
        private int _pendingSuppressedCount;
        private DiagnosticSeverity _lastReportedSeverity;
        private long _maxGcAllocatedInFrameBytes;
        private long _peakGcUsedBytes;
        private long _peakGcReservedBytes;
        private long _peakSystemUsedBytes;
        private bool _hasGcAllocationData;
        private bool _hasGcUsedMemoryData;
        private bool _hasGcReservedMemoryData;
        private bool _hasSystemUsedMemoryData;

        public ManagedMemoryMonitor(
            RuntimeDiagnosticBuffer events,
            long warningAllocationBytes,
            long criticalAllocationBytes,
            double reportCooldownSeconds)
        {
            _events = events ?? throw new ArgumentNullException(nameof(events));
            if (warningAllocationBytes <= 0)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(warningAllocationBytes));
            }

            if (criticalAllocationBytes <= warningAllocationBytes)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(criticalAllocationBytes));
            }

            if (reportCooldownSeconds < 0d ||
                double.IsNaN(reportCooldownSeconds) ||
                double.IsInfinity(reportCooldownSeconds))
            {
                throw new ArgumentOutOfRangeException(
                    nameof(reportCooldownSeconds));
            }

            _warningAllocationBytes = warningAllocationBytes;
            _criticalAllocationBytes = criticalAllocationBytes;
            _reportCooldownSeconds = reportCooldownSeconds;
        }

        public long ObservedSampleCount { get; private set; }

        public long UnavailableAllocationSampleCount { get; private set; }

        public long AllocationSpikeCount { get; private set; }

        public long SuppressedReportCount { get; private set; }

        public long MaxGcAllocatedInFrameBytes =>
            _maxGcAllocatedInFrameBytes;

        public long PeakGcUsedBytes => _peakGcUsedBytes;

        public long PeakGcReservedBytes => _peakGcReservedBytes;

        public long PeakSystemUsedBytes => _peakSystemUsedBytes;

        public bool HasGcAllocationData => _hasGcAllocationData;

        public bool HasGcUsedMemoryData => _hasGcUsedMemoryData;

        public bool HasGcReservedMemoryData => _hasGcReservedMemoryData;

        public bool HasSystemUsedMemoryData => _hasSystemUsedMemoryData;

        public bool Observe(
            ManagedMemorySample sample,
            double timestampSeconds)
        {
            ValidateTimestamp(timestampSeconds);
            ObservedSampleCount++;

            UpdatePeak(
                sample.GcAllocatedInFrameBytes,
                ref _maxGcAllocatedInFrameBytes,
                ref _hasGcAllocationData);
            UpdatePeak(
                sample.GcUsedBytes,
                ref _peakGcUsedBytes,
                ref _hasGcUsedMemoryData);
            UpdatePeak(
                sample.GcReservedBytes,
                ref _peakGcReservedBytes,
                ref _hasGcReservedMemoryData);
            UpdatePeak(
                sample.SystemUsedBytes,
                ref _peakSystemUsedBytes,
                ref _hasSystemUsedMemoryData);

            if (!sample.HasGcAllocatedInFrame)
            {
                UnavailableAllocationSampleCount++;
                return false;
            }

            long allocatedBytes = sample.GcAllocatedInFrameBytes;
            if (allocatedBytes < _warningAllocationBytes)
            {
                return false;
            }

            AllocationSpikeCount++;
            DiagnosticSeverity severity =
                allocatedBytes >= _criticalAllocationBytes
                    ? DiagnosticSeverity.Critical
                    : DiagnosticSeverity.Warning;
            bool severityEscalated = severity > _lastReportedSeverity;
            if (timestampSeconds < _nextReportTimestamp && !severityEscalated)
            {
                _pendingSuppressedCount++;
                SuppressedReportCount++;
                return false;
            }

            string message = string.Format(
                CultureInfo.InvariantCulture,
                "GC allocated {0} bytes in one frame.",
                allocatedBytes);
            _events.Add(
                timestampSeconds,
                Category,
                severity,
                message,
                occurrenceCount: _pendingSuppressedCount + 1);
            _pendingSuppressedCount = 0;
            _nextReportTimestamp = timestampSeconds + _reportCooldownSeconds;
            _lastReportedSeverity = severity;
            return true;
        }

        public void ResetStatistics()
        {
            ObservedSampleCount = 0;
            UnavailableAllocationSampleCount = 0;
            AllocationSpikeCount = 0;
            SuppressedReportCount = 0;
            _maxGcAllocatedInFrameBytes = 0;
            _peakGcUsedBytes = 0;
            _peakGcReservedBytes = 0;
            _peakSystemUsedBytes = 0;
            _hasGcAllocationData = false;
            _hasGcUsedMemoryData = false;
            _hasGcReservedMemoryData = false;
            _hasSystemUsedMemoryData = false;
            _hasObservedTimestamp = false;
            _lastObservedTimestamp = 0d;
            _nextReportTimestamp = 0d;
            _pendingSuppressedCount = 0;
            _lastReportedSeverity = DiagnosticSeverity.Info;
        }

        private void ValidateTimestamp(double timestampSeconds)
        {
            if (timestampSeconds < 0d ||
                double.IsNaN(timestampSeconds) ||
                double.IsInfinity(timestampSeconds))
            {
                throw new ArgumentOutOfRangeException(
                    nameof(timestampSeconds));
            }

            if (_hasObservedTimestamp && timestampSeconds < _lastObservedTimestamp)
            {
                throw new ArgumentException(
                    "The observation timestamp must be monotonic.",
                    nameof(timestampSeconds));
            }

            _hasObservedTimestamp = true;
            _lastObservedTimestamp = timestampSeconds;
        }

        private static void UpdatePeak(
            long value,
            ref long peak,
            ref bool hasData)
        {
            if (value == ManagedMemorySample.Unavailable)
            {
                return;
            }

            hasData = true;
            if (value > peak)
            {
                peak = value;
            }
        }
    }
}
