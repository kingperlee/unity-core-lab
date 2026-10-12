using System.Diagnostics;
using UnityCoreLab.ObjectPooling;

namespace UnityCoreLab.Samples.ObjectPooling
{
    public readonly struct ObjectPoolPerformanceSample
    {
        public ObjectPoolPerformanceSample(
            string scenario,
            int operationsPerSample,
            int sampleCount,
            long medianElapsedTicks,
            long medianManagedAllocatedBytes)
        {
            Scenario = scenario;
            OperationsPerSample = operationsPerSample;
            SampleCount = sampleCount;
            MedianElapsedTicks = medianElapsedTicks;
            MedianManagedAllocatedBytes = medianManagedAllocatedBytes;
        }

        public string Scenario { get; }

        public int OperationsPerSample { get; }

        public int SampleCount { get; }

        public long MedianElapsedTicks { get; }

        public long MedianManagedAllocatedBytes { get; }

        public double MicrosecondsPerOperation =>
            OperationsPerSample == 0
                ? 0d
                : MedianElapsedTicks * 1_000_000d /
                  Stopwatch.Frequency /
                  OperationsPerSample;

        public double ManagedBytesPerOperation =>
            OperationsPerSample == 0
                ? 0d
                : (double)MedianManagedAllocatedBytes / OperationsPerSample;
    }

    public readonly struct ObjectPoolComparisonResult
    {
        public ObjectPoolComparisonResult(
            ObjectPoolPerformanceSample instantiateDestroy,
            ObjectPoolPerformanceSample rentReturn,
            PoolSnapshot poolSnapshot,
            bool measuredInEditor)
        {
            InstantiateDestroy = instantiateDestroy;
            RentReturn = rentReturn;
            PoolSnapshot = poolSnapshot;
            MeasuredInEditor = measuredInEditor;
        }

        public ObjectPoolPerformanceSample InstantiateDestroy { get; }

        public ObjectPoolPerformanceSample RentReturn { get; }

        public PoolSnapshot PoolSnapshot { get; }

        public bool MeasuredInEditor { get; }
    }
}
