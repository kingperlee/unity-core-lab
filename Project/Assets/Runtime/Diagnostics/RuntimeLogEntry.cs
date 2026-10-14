using System;

namespace UnityCoreLab.Diagnostics
{
    /// <summary>
    /// Log payload retained by the thread-safe inbox before main-thread drain.
    /// </summary>
    public readonly struct RuntimeLogEntry
    {
        public RuntimeLogEntry(
            DiagnosticSeverity severity,
            string message,
            string stackTrace)
        {
            if (!Enum.IsDefined(typeof(DiagnosticSeverity), severity))
            {
                throw new ArgumentOutOfRangeException(nameof(severity));
            }

            Severity = severity;
            Message = message ?? string.Empty;
            StackTrace = stackTrace ?? string.Empty;
        }

        public DiagnosticSeverity Severity { get; }

        public string Message { get; }

        public string StackTrace { get; }

        public string BuildDiagnosticMessage()
        {
            return string.IsNullOrEmpty(StackTrace)
                ? Message
                : Message + "\n" + StackTrace;
        }
    }
}
