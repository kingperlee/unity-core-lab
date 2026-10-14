namespace UnityCoreLab.Diagnostics
{
    /// <summary>
    /// Immutable memory-monitor summary embedded in a diagnostic report.
    /// Availability flags distinguish a real zero from an unsupported counter.
    /// </summary>
    public readonly struct ManagedMemoryStatistics
    {
        public ManagedMemoryStatistics(ManagedMemoryMonitor monitor)
        {
            if (monitor == null)
            {
                ObservedSampleCount = 0;
                UnavailableAllocationSampleCount = 0;
                AllocationSpikeCount = 0;
                SuppressedReportCount = 0;
                MaxGcAllocatedInFrameBytes = 0;
                PeakGcUsedBytes = 0;
                PeakGcReservedBytes = 0;
                PeakSystemUsedBytes = 0;
                HasGcAllocationData = false;
                HasGcUsedMemoryData = false;
                HasGcReservedMemoryData = false;
                HasSystemUsedMemoryData = false;
                return;
            }

            ObservedSampleCount = monitor.ObservedSampleCount;
            UnavailableAllocationSampleCount =
                monitor.UnavailableAllocationSampleCount;
            AllocationSpikeCount = monitor.AllocationSpikeCount;
            SuppressedReportCount = monitor.SuppressedReportCount;
            MaxGcAllocatedInFrameBytes =
                monitor.MaxGcAllocatedInFrameBytes;
            PeakGcUsedBytes = monitor.PeakGcUsedBytes;
            PeakGcReservedBytes = monitor.PeakGcReservedBytes;
            PeakSystemUsedBytes = monitor.PeakSystemUsedBytes;
            HasGcAllocationData = monitor.HasGcAllocationData;
            HasGcUsedMemoryData = monitor.HasGcUsedMemoryData;
            HasGcReservedMemoryData = monitor.HasGcReservedMemoryData;
            HasSystemUsedMemoryData = monitor.HasSystemUsedMemoryData;
        }

        public long ObservedSampleCount { get; }

        public long UnavailableAllocationSampleCount { get; }

        public long AllocationSpikeCount { get; }

        public long SuppressedReportCount { get; }

        public long MaxGcAllocatedInFrameBytes { get; }

        public long PeakGcUsedBytes { get; }

        public long PeakGcReservedBytes { get; }

        public long PeakSystemUsedBytes { get; }

        public bool HasGcAllocationData { get; }

        public bool HasGcUsedMemoryData { get; }

        public bool HasGcReservedMemoryData { get; }

        public bool HasSystemUsedMemoryData { get; }
    }
}
