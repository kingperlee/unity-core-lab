using System;
using System.Globalization;

namespace UnityCoreLab.Diagnostics
{
    /// <summary>
    /// Bounded next-launch importer for incidents captured outside managed
    /// Unity execution. Source failures are counted and never crash startup.
    /// </summary>
    public sealed class PlatformIncidentImporter
    {
        private readonly RuntimeDiagnosticBuffer _destination;
        private readonly string[] _knownIncidentIds;
        private readonly int _maximumSummaryCharacters;
        private readonly int _maximumDetailsCharacters;
        private int _knownStartIndex;
        private int _knownCount;
        private long _importedCount;
        private long _duplicateCount;
        private long _sourceReadFailureCount;
        private long _acknowledgementFailureCount;
        private long _truncatedCount;

        public PlatformIncidentImporter(
            RuntimeDiagnosticBuffer destination,
            int rememberedIncidentCapacity,
            int maximumSummaryCharacters,
            int maximumDetailsCharacters)
        {
            _destination = destination ??
                throw new ArgumentNullException(nameof(destination));
            if (rememberedIncidentCapacity <= 0)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(rememberedIncidentCapacity));
            }

            if (maximumSummaryCharacters <= 0)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(maximumSummaryCharacters));
            }

            if (maximumDetailsCharacters < 0)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(maximumDetailsCharacters));
            }

            _knownIncidentIds = new string[rememberedIncidentCapacity];
            _maximumSummaryCharacters = maximumSummaryCharacters;
            _maximumDetailsCharacters = maximumDetailsCharacters;
        }

        public int ImportAvailable(
            IPlatformIncidentSource source,
            double timestampSeconds,
            int maximumRecords)
        {
            if (source == null)
            {
                throw new ArgumentNullException(nameof(source));
            }

            ValidateTimestamp(timestampSeconds);
            if (maximumRecords <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(maximumRecords));
            }

            int importedThisCall = 0;
            for (int index = 0; index < maximumRecords; index++)
            {
                PlatformIncidentRecord incident;
                try
                {
                    if (!source.TryPeek(out incident))
                    {
                        break;
                    }
                }
                catch
                {
                    _sourceReadFailureCount++;
                    break;
                }

                if (IsKnown(incident.IncidentId))
                {
                    _duplicateCount++;
                    if (!TryAcknowledge(source, incident.IncidentId))
                    {
                        break;
                    }

                    continue;
                }

                bool truncated = false;
                string summary = LimitText(
                    incident.Summary,
                    _maximumSummaryCharacters,
                    ref truncated);
                string details = LimitText(
                    incident.Details,
                    _maximumDetailsCharacters,
                    ref truncated);
                string message = BuildMessage(incident, summary, details);
                _destination.Add(
                    timestampSeconds,
                    GetCategory(incident.Kind),
                    DiagnosticSeverity.Critical,
                    message);
                Remember(incident.IncidentId);
                _importedCount++;
                importedThisCall++;
                if (truncated)
                {
                    _truncatedCount++;
                }

                if (!TryAcknowledge(source, incident.IncidentId))
                {
                    break;
                }
            }

            return importedThisCall;
        }

        public PlatformIncidentImportStatistics GetStatistics()
        {
            return new PlatformIncidentImportStatistics(
                _importedCount,
                _duplicateCount,
                _sourceReadFailureCount,
                _acknowledgementFailureCount,
                _truncatedCount);
        }

        public void ResetStatistics()
        {
            _importedCount = 0;
            _duplicateCount = 0;
            _sourceReadFailureCount = 0;
            _acknowledgementFailureCount = 0;
            _truncatedCount = 0;
        }

        private bool TryAcknowledge(
            IPlatformIncidentSource source,
            string incidentId)
        {
            try
            {
                source.Acknowledge(incidentId);
                return true;
            }
            catch
            {
                _acknowledgementFailureCount++;
                return false;
            }
        }

        private bool IsKnown(string incidentId)
        {
            for (int index = 0; index < _knownCount; index++)
            {
                int physicalIndex =
                    (_knownStartIndex + index) % _knownIncidentIds.Length;
                if (string.Equals(
                        _knownIncidentIds[physicalIndex],
                        incidentId,
                        StringComparison.Ordinal))
                {
                    return true;
                }
            }

            return false;
        }

        private void Remember(string incidentId)
        {
            int writeIndex;
            if (_knownCount < _knownIncidentIds.Length)
            {
                writeIndex =
                    (_knownStartIndex + _knownCount) % _knownIncidentIds.Length;
                _knownCount++;
            }
            else
            {
                writeIndex = _knownStartIndex;
                _knownStartIndex =
                    (_knownStartIndex + 1) % _knownIncidentIds.Length;
            }

            _knownIncidentIds[writeIndex] = incidentId;
        }

        private static string GetCategory(PlatformIncidentKind kind)
        {
            return kind == PlatformIncidentKind.NativeCrash
                ? "NativeCrash"
                : "ANR";
        }

        private static string BuildMessage(
            PlatformIncidentRecord incident,
            string summary,
            string details)
        {
            string message =
                $"Previous-session {incident.Kind} from {incident.Source}; " +
                $"incident {incident.IncidentId}; occurred at Unix ms " +
                incident.OccurredAtUnixMilliseconds.ToString(
                    CultureInfo.InvariantCulture) +
                $". {summary}";
            return string.IsNullOrEmpty(details)
                ? message
                : message + "\nDetails: " + details;
        }

        private static string LimitText(
            string value,
            int maximumCharacters,
            ref bool truncated)
        {
            value = value ?? string.Empty;
            if (value.Length <= maximumCharacters)
            {
                return value;
            }

            truncated = true;
            return value.Substring(0, maximumCharacters);
        }

        private static void ValidateTimestamp(double timestampSeconds)
        {
            if (timestampSeconds < 0d ||
                double.IsNaN(timestampSeconds) ||
                double.IsInfinity(timestampSeconds))
            {
                throw new ArgumentOutOfRangeException(
                    nameof(timestampSeconds));
            }
        }
    }
}
