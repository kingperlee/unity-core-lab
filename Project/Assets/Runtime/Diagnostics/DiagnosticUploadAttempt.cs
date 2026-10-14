namespace UnityCoreLab.Diagnostics
{
    public readonly struct DiagnosticUploadAttempt
    {
        public DiagnosticUploadAttempt(
            long reportId,
            int attemptNumber,
            string payload)
        {
            ReportId = reportId;
            AttemptNumber = attemptNumber;
            Payload = payload;
        }

        public long ReportId { get; }

        public int AttemptNumber { get; }

        public string Payload { get; }
    }
}
