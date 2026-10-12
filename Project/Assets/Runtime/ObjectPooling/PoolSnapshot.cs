namespace UnityCoreLab.ObjectPooling
{
    public readonly struct PoolSnapshot
    {
        public PoolSnapshot(
            int availableCount,
            int activeCount,
            int createdCount,
            int rentCount,
            int reusedCount,
            int returnCount,
            int discardedCount,
            int peakActiveCount,
            bool isDisposed)
        {
            AvailableCount = availableCount;
            ActiveCount = activeCount;
            CreatedCount = createdCount;
            RentCount = rentCount;
            ReusedCount = reusedCount;
            ReturnCount = returnCount;
            DiscardedCount = discardedCount;
            PeakActiveCount = peakActiveCount;
            IsDisposed = isDisposed;
        }

        public int AvailableCount { get; }

        public int ActiveCount { get; }

        public int OwnedCount => AvailableCount + ActiveCount;

        public int CreatedCount { get; }

        public int RentCount { get; }

        public int ReusedCount { get; }

        public int ReturnCount { get; }

        public int DiscardedCount { get; }

        public int PeakActiveCount { get; }

        public bool IsDisposed { get; }

        public double ReuseRatio =>
            RentCount == 0 ? 0d : (double)ReusedCount / RentCount;
    }
}
