namespace UnityCoreLab.Diagnostics
{
    public readonly struct PlatformIncidentImportStatistics
    {
        public PlatformIncidentImportStatistics(
            long importedCount,
            long duplicateCount,
            long sourceReadFailureCount,
            long acknowledgementFailureCount,
            long truncatedCount)
        {
            ImportedCount = importedCount;
            DuplicateCount = duplicateCount;
            SourceReadFailureCount = sourceReadFailureCount;
            AcknowledgementFailureCount = acknowledgementFailureCount;
            TruncatedCount = truncatedCount;
        }

        public long ImportedCount { get; }

        public long DuplicateCount { get; }

        public long SourceReadFailureCount { get; }

        public long AcknowledgementFailureCount { get; }

        public long TruncatedCount { get; }
    }
}
