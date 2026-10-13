namespace UnityCoreLab.Samples.RenderingPerformance.FrameTiming
{
    public readonly struct FrameTimingDistribution
    {
        public FrameTimingDistribution(
            int sampleCount,
            double medianMilliseconds,
            double p95Milliseconds)
        {
            SampleCount = sampleCount;
            MedianMilliseconds = medianMilliseconds;
            P95Milliseconds = p95Milliseconds;
        }

        public int SampleCount { get; }

        public double MedianMilliseconds { get; }

        public double P95Milliseconds { get; }

        public bool HasSamples => SampleCount > 0;
    }

    public readonly struct FrameTimingSummary
    {
        public FrameTimingSummary(
            string label,
            int capturedFrameCount,
            FrameTimingDistribution cpuFrame,
            FrameTimingDistribution cpuMainThread,
            FrameTimingDistribution cpuRenderThread,
            FrameTimingDistribution gpuFrame)
        {
            Label = label;
            CapturedFrameCount = capturedFrameCount;
            CpuFrame = cpuFrame;
            CpuMainThread = cpuMainThread;
            CpuRenderThread = cpuRenderThread;
            GpuFrame = gpuFrame;
        }

        public string Label { get; }

        public int CapturedFrameCount { get; }

        public FrameTimingDistribution CpuFrame { get; }

        public FrameTimingDistribution CpuMainThread { get; }

        public FrameTimingDistribution CpuRenderThread { get; }

        public FrameTimingDistribution GpuFrame { get; }

        public bool HasResult => CapturedFrameCount > 0;
    }
}
