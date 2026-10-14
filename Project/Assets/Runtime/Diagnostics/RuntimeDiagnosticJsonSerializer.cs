using System;
using System.Globalization;
using System.Text;

namespace UnityCoreLab.Diagnostics
{
    /// <summary>
    /// Engine-independent JSON writer with deterministic property order and
    /// invariant numeric formatting. No reflection is used.
    /// </summary>
    public static class RuntimeDiagnosticJsonSerializer
    {
        public static string Serialize(
            RuntimeDiagnosticReport report,
            bool indented = true)
        {
            if (report == null)
            {
                throw new ArgumentNullException(nameof(report));
            }

            var builder = new StringBuilder(2048);
            AppendObjectStart(builder);
            AppendNumberProperty(
                builder,
                "schemaVersion",
                report.SchemaVersion,
                1,
                indented,
                first: true);
            AppendStringProperty(
                builder,
                "generatedAtUtc",
                report.GeneratedAtUtc.ToString(
                    "O",
                    CultureInfo.InvariantCulture),
                1,
                indented);
            AppendNumberProperty(
                builder,
                "capturedAtSeconds",
                report.CapturedAtSeconds,
                1,
                indented);

            AppendPropertyPrefix(builder, "environment", 1, indented);
            AppendObjectStart(builder);
            AppendStringProperty(
                builder,
                "sessionId",
                report.Context.SessionId,
                2,
                indented,
                first: true);
            AppendStringProperty(
                builder,
                "applicationName",
                report.Context.ApplicationName,
                2,
                indented);
            AppendStringProperty(
                builder,
                "applicationVersion",
                report.Context.ApplicationVersion,
                2,
                indented);
            AppendStringProperty(
                builder,
                "platform",
                report.Context.Platform,
                2,
                indented);
            AppendStringProperty(
                builder,
                "deviceModel",
                report.Context.DeviceModel,
                2,
                indented);
            AppendStringProperty(
                builder,
                "operatingSystem",
                report.Context.OperatingSystem,
                2,
                indented);
            AppendStringProperty(
                builder,
                "graphicsDeviceName",
                report.Context.GraphicsDeviceName,
                2,
                indented);
            AppendObjectEnd(builder, 1, indented);

            AppendPropertyPrefix(builder, "hitchStatistics", 1, indented);
            AppendObjectStart(builder);
            AppendNumberProperty(
                builder,
                "detectedCount",
                report.DetectedHitchCount,
                2,
                indented,
                first: true);
            AppendNumberProperty(
                builder,
                "suppressedReportCount",
                report.SuppressedReportCount,
                2,
                indented);
            AppendNumberProperty(
                builder,
                "worstFrameMilliseconds",
                report.WorstFrameMilliseconds,
                2,
                indented);
            AppendObjectEnd(builder, 1, indented);

            ManagedMemoryStatistics memory = report.MemoryStatistics;
            AppendPropertyPrefix(builder, "memoryStatistics", 1, indented);
            AppendObjectStart(builder);
            AppendNumberProperty(
                builder,
                "observedSampleCount",
                memory.ObservedSampleCount,
                2,
                indented,
                first: true);
            AppendNumberProperty(
                builder,
                "unavailableAllocationSampleCount",
                memory.UnavailableAllocationSampleCount,
                2,
                indented);
            AppendNumberProperty(
                builder,
                "allocationSpikeCount",
                memory.AllocationSpikeCount,
                2,
                indented);
            AppendNumberProperty(
                builder,
                "suppressedReportCount",
                memory.SuppressedReportCount,
                2,
                indented);
            AppendNullableNumberProperty(
                builder,
                "maxGcAllocatedInFrameBytes",
                memory.MaxGcAllocatedInFrameBytes,
                memory.HasGcAllocationData,
                2,
                indented);
            AppendNullableNumberProperty(
                builder,
                "peakGcUsedBytes",
                memory.PeakGcUsedBytes,
                memory.HasGcUsedMemoryData,
                2,
                indented);
            AppendNullableNumberProperty(
                builder,
                "peakGcReservedBytes",
                memory.PeakGcReservedBytes,
                memory.HasGcReservedMemoryData,
                2,
                indented);
            AppendNullableNumberProperty(
                builder,
                "peakSystemUsedBytes",
                memory.PeakSystemUsedBytes,
                memory.HasSystemUsedMemoryData,
                2,
                indented);
            AppendObjectEnd(builder, 1, indented);

            RuntimeLogCaptureStatistics logCapture =
                report.LogCaptureStatistics;
            AppendPropertyPrefix(
                builder,
                "logCaptureStatistics",
                1,
                indented);
            AppendObjectStart(builder);
            AppendNumberProperty(
                builder,
                "capacity",
                logCapture.Capacity,
                2,
                indented,
                first: true);
            AppendNumberProperty(
                builder,
                "pendingCount",
                logCapture.PendingCount,
                2,
                indented);
            AppendNumberProperty(
                builder,
                "receivedCount",
                logCapture.ReceivedCount,
                2,
                indented);
            AppendNumberProperty(
                builder,
                "drainedCount",
                logCapture.DrainedCount,
                2,
                indented);
            AppendNumberProperty(
                builder,
                "droppedCount",
                logCapture.DroppedCount,
                2,
                indented);
            AppendNumberProperty(
                builder,
                "truncatedCount",
                logCapture.TruncatedCount,
                2,
                indented);
            AppendObjectEnd(builder, 1, indented);

            DiagnosticOutboxStatistics outbox = report.OutboxStatistics;
            AppendPropertyPrefix(
                builder,
                "deliveryStatistics",
                1,
                indented);
            AppendObjectStart(builder);
            AppendNumberProperty(
                builder,
                "capacity",
                outbox.Capacity,
                2,
                indented,
                first: true);
            AppendNumberProperty(
                builder,
                "pendingCount",
                outbox.PendingCount,
                2,
                indented);
            AppendBooleanProperty(
                builder,
                "hasInFlightAttempt",
                outbox.HasInFlightAttempt,
                2,
                indented);
            AppendNumberProperty(
                builder,
                "acceptedCount",
                outbox.AcceptedCount,
                2,
                indented);
            AppendNumberProperty(
                builder,
                "deliveredCount",
                outbox.DeliveredCount,
                2,
                indented);
            AppendNumberProperty(
                builder,
                "terminalFailureCount",
                outbox.TerminalFailureCount,
                2,
                indented);
            AppendNumberProperty(
                builder,
                "rejectedCount",
                outbox.RejectedCount,
                2,
                indented);
            AppendNumberProperty(
                builder,
                "retryScheduledCount",
                outbox.RetryScheduledCount,
                2,
                indented);
            AppendObjectEnd(builder, 1, indented);

            AppendPropertyPrefix(builder, "eventStorage", 1, indented);
            AppendObjectStart(builder);
            AppendNumberProperty(
                builder,
                "capacity",
                report.EventCapacity,
                2,
                indented,
                first: true);
            AppendNumberProperty(
                builder,
                "count",
                report.EventCount,
                2,
                indented);
            AppendNumberProperty(
                builder,
                "overwrittenCount",
                report.OverwrittenEventCount,
                2,
                indented);
            AppendObjectEnd(builder, 1, indented);

            AppendPropertyPrefix(builder, "events", 1, indented);
            builder.Append('[');
            for (int index = 0; index < report.EventCount; index++)
            {
                if (index > 0)
                {
                    builder.Append(',');
                }

                AppendNewLineAndIndent(builder, 2, indented);
                AppendEvent(builder, report.GetEvent(index), indented);
            }

            if (report.EventCount > 0)
            {
                AppendNewLineAndIndent(builder, 1, indented);
            }

            builder.Append(']');
            AppendNewLineAndIndent(builder, 0, indented);
            builder.Append('}');
            return builder.ToString();
        }

        private static void AppendEvent(
            StringBuilder builder,
            RuntimeDiagnosticEvent diagnosticEvent,
            bool indented)
        {
            AppendObjectStart(builder);
            AppendNumberProperty(
                builder,
                "sequence",
                diagnosticEvent.Sequence,
                3,
                indented,
                first: true);
            AppendNumberProperty(
                builder,
                "timestampSeconds",
                diagnosticEvent.TimestampSeconds,
                3,
                indented);
            AppendStringProperty(
                builder,
                "category",
                diagnosticEvent.Category,
                3,
                indented);
            AppendStringProperty(
                builder,
                "severity",
                diagnosticEvent.Severity.ToString(),
                3,
                indented);
            AppendStringProperty(
                builder,
                "message",
                diagnosticEvent.Message,
                3,
                indented);
            AppendNumberProperty(
                builder,
                "durationMilliseconds",
                diagnosticEvent.DurationMilliseconds,
                3,
                indented);
            AppendNumberProperty(
                builder,
                "occurrenceCount",
                diagnosticEvent.OccurrenceCount,
                3,
                indented);
            AppendObjectEnd(builder, 2, indented);
        }

        private static void AppendStringProperty(
            StringBuilder builder,
            string name,
            string value,
            int indent,
            bool indented,
            bool first = false)
        {
            AppendPropertySeparator(builder, indent, indented, first);
            AppendQuoted(builder, name);
            builder.Append(indented ? ": " : ":");
            AppendQuoted(builder, value);
        }

        private static void AppendNumberProperty(
            StringBuilder builder,
            string name,
            long value,
            int indent,
            bool indented,
            bool first = false)
        {
            AppendPropertySeparator(builder, indent, indented, first);
            AppendQuoted(builder, name);
            builder.Append(indented ? ": " : ":");
            builder.Append(value.ToString(CultureInfo.InvariantCulture));
        }

        private static void AppendNumberProperty(
            StringBuilder builder,
            string name,
            double value,
            int indent,
            bool indented,
            bool first = false)
        {
            AppendPropertySeparator(builder, indent, indented, first);
            AppendQuoted(builder, name);
            builder.Append(indented ? ": " : ":");
            builder.Append(value.ToString("R", CultureInfo.InvariantCulture));
        }

        private static void AppendNullableNumberProperty(
            StringBuilder builder,
            string name,
            long value,
            bool available,
            int indent,
            bool indented)
        {
            AppendPropertySeparator(builder, indent, indented, first: false);
            AppendQuoted(builder, name);
            builder.Append(indented ? ": " : ":");
            if (available)
            {
                builder.Append(value.ToString(CultureInfo.InvariantCulture));
            }
            else
            {
                builder.Append("null");
            }
        }

        private static void AppendBooleanProperty(
            StringBuilder builder,
            string name,
            bool value,
            int indent,
            bool indented)
        {
            AppendPropertySeparator(builder, indent, indented, first: false);
            AppendQuoted(builder, name);
            builder.Append(indented ? ": " : ":");
            builder.Append(value ? "true" : "false");
        }

        private static void AppendPropertyPrefix(
            StringBuilder builder,
            string name,
            int indent,
            bool indented)
        {
            AppendPropertySeparator(builder, indent, indented, first: false);
            AppendQuoted(builder, name);
            builder.Append(indented ? ": " : ":");
        }

        private static void AppendPropertySeparator(
            StringBuilder builder,
            int indent,
            bool indented,
            bool first)
        {
            if (!first)
            {
                builder.Append(',');
            }

            AppendNewLineAndIndent(builder, indent, indented);
        }

        private static void AppendObjectStart(StringBuilder builder)
        {
            builder.Append('{');
        }

        private static void AppendObjectEnd(
            StringBuilder builder,
            int indent,
            bool indented)
        {
            AppendNewLineAndIndent(builder, indent, indented);
            builder.Append('}');
        }

        private static void AppendNewLineAndIndent(
            StringBuilder builder,
            int indent,
            bool indented)
        {
            if (!indented)
            {
                return;
            }

            builder.Append('\n');
            builder.Append(' ', indent * 2);
        }

        private static void AppendQuoted(StringBuilder builder, string value)
        {
            builder.Append('"');
            foreach (char character in value)
            {
                switch (character)
                {
                    case '"':
                        builder.Append("\\\"");
                        break;
                    case '\\':
                        builder.Append("\\\\");
                        break;
                    case '\b':
                        builder.Append("\\b");
                        break;
                    case '\f':
                        builder.Append("\\f");
                        break;
                    case '\n':
                        builder.Append("\\n");
                        break;
                    case '\r':
                        builder.Append("\\r");
                        break;
                    case '\t':
                        builder.Append("\\t");
                        break;
                    default:
                        if (character < 0x20)
                        {
                            builder.Append("\\u");
                            builder.Append(((int)character).ToString("x4"));
                        }
                        else
                        {
                            builder.Append(character);
                        }

                        break;
                }
            }

            builder.Append('"');
        }
    }
}
