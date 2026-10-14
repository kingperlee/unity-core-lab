using System;

namespace UnityCoreLab.Diagnostics
{
    /// <summary>
    /// Platform or crash-SDK evidence retained outside the failed process and
    /// made available to managed code during a later healthy launch.
    /// </summary>
    public readonly struct PlatformIncidentRecord
    {
        public PlatformIncidentRecord(
            string incidentId,
            PlatformIncidentKind kind,
            long occurredAtUnixMilliseconds,
            string source,
            string summary,
            string details)
        {
            if (string.IsNullOrWhiteSpace(incidentId))
            {
                throw new ArgumentException(
                    "An incident identifier is required.",
                    nameof(incidentId));
            }

            if (!Enum.IsDefined(typeof(PlatformIncidentKind), kind))
            {
                throw new ArgumentOutOfRangeException(nameof(kind));
            }

            if (occurredAtUnixMilliseconds < 0)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(occurredAtUnixMilliseconds));
            }

            if (string.IsNullOrWhiteSpace(source))
            {
                throw new ArgumentException(
                    "An incident source is required.",
                    nameof(source));
            }

            if (string.IsNullOrWhiteSpace(summary))
            {
                throw new ArgumentException(
                    "An incident summary is required.",
                    nameof(summary));
            }

            IncidentId = incidentId;
            Kind = kind;
            OccurredAtUnixMilliseconds = occurredAtUnixMilliseconds;
            Source = source;
            Summary = summary;
            Details = details ?? string.Empty;
        }

        public string IncidentId { get; }

        public PlatformIncidentKind Kind { get; }

        public long OccurredAtUnixMilliseconds { get; }

        public string Source { get; }

        public string Summary { get; }

        public string Details { get; }
    }
}
