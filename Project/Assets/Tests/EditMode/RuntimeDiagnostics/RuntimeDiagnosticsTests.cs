using System;
using System.Globalization;
using System.Threading;
using NUnit.Framework;
using UnityCoreLab.Diagnostics;

namespace UnityCoreLab.Tests.EditMode.RuntimeDiagnostics
{
    public sealed class RuntimeDiagnosticsTests
    {
        [Test]
        public void Buffer_OverwritesOldestEventAndPreservesChronology()
        {
            var events = new RuntimeDiagnosticBuffer(3);

            events.Add(1d, "Test", DiagnosticSeverity.Info, "One");
            events.Add(2d, "Test", DiagnosticSeverity.Warning, "Two");
            events.Add(3d, "Test", DiagnosticSeverity.Error, "Three");
            events.Add(4d, "Test", DiagnosticSeverity.Critical, "Four");

            Assert.That(events.Count, Is.EqualTo(3));
            Assert.That(events.OverwrittenCount, Is.EqualTo(1));
            Assert.That(events[0].Sequence, Is.EqualTo(2));
            Assert.That(events[0].Message, Is.EqualTo("Two"));
            Assert.That(events[2].Sequence, Is.EqualTo(4));
            Assert.That(events[2].Message, Is.EqualTo("Four"));

            var snapshot = new RuntimeDiagnosticEvent[3];
            Assert.That(events.CopyTo(snapshot), Is.EqualTo(3));
            Assert.That(snapshot[0].Message, Is.EqualTo("Two"));
            Assert.That(snapshot[2].Message, Is.EqualTo("Four"));
        }

        [Test]
        public void Buffer_ClearReleasesHistoryButKeepsSequenceMonotonic()
        {
            var events = new RuntimeDiagnosticBuffer(2);
            events.Add(1d, "Test", DiagnosticSeverity.Info, "Before");

            events.Clear();
            RuntimeDiagnosticEvent after = events.Add(
                2d,
                "Test",
                DiagnosticSeverity.Info,
                "After");

            Assert.That(events.Count, Is.EqualTo(1));
            Assert.That(events.OverwrittenCount, Is.Zero);
            Assert.That(after.Sequence, Is.EqualTo(2));
        }

        [Test]
        public void Buffer_RejectsInvalidInput()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() =>
                new RuntimeDiagnosticBuffer(0));

            var events = new RuntimeDiagnosticBuffer(2);
            Assert.Throws<ArgumentOutOfRangeException>(() =>
                events.Add(
                    double.NaN,
                    "Test",
                    DiagnosticSeverity.Info,
                    "Message"));
            Assert.Throws<ArgumentException>(() =>
                events.Add(0d, string.Empty, DiagnosticSeverity.Info, "Message"));
            Assert.Throws<ArgumentOutOfRangeException>(() =>
                events.Add(
                    0d,
                    "Test",
                    (DiagnosticSeverity)99,
                    "Message"));
            Assert.Throws<ArgumentException>(() =>
                events.Add(0d, "Test", DiagnosticSeverity.Info, string.Empty));
            events.Add(0d, "Test", DiagnosticSeverity.Info, "Valid event");
            Assert.Throws<ArgumentException>(() =>
                events.CopyTo(new RuntimeDiagnosticEvent[0]));
        }

        [Test]
        public void HitchDetector_ClassifiesAndCoalescesReports()
        {
            var events = new RuntimeDiagnosticBuffer(8);
            var detector = new FrameHitchDetector(
                events,
                warningThresholdMilliseconds: 33.3d,
                criticalThresholdMilliseconds: 100d,
                reportCooldownSeconds: 0.5d);

            Assert.That(detector.ObserveFrame(16d, 0d), Is.False);
            Assert.That(detector.ObserveFrame(40d, 1d), Is.True);
            Assert.That(detector.ObserveFrame(50d, 1.2d), Is.False);
            Assert.That(
                detector.ObserveFrame(120d, 1.3d),
                Is.True,
                "Critical severity must bypass a warning cooldown.");

            Assert.That(events.Count, Is.EqualTo(2));
            Assert.That(events[0].Severity,
                Is.EqualTo(DiagnosticSeverity.Warning));
            Assert.That(events[0].OccurrenceCount, Is.EqualTo(1));
            Assert.That(events[1].Severity,
                Is.EqualTo(DiagnosticSeverity.Critical));
            Assert.That(events[1].OccurrenceCount, Is.EqualTo(2));
            Assert.That(detector.DetectedHitchCount, Is.EqualTo(3));
            Assert.That(detector.SuppressedReportCount, Is.EqualTo(1));
            Assert.That(detector.WorstFrameMilliseconds, Is.EqualTo(120d));
        }

        [Test]
        public void HitchDetector_RequiresValidConfigurationAndMonotonicTime()
        {
            var events = new RuntimeDiagnosticBuffer(2);
            Assert.Throws<ArgumentOutOfRangeException>(() =>
                new FrameHitchDetector(events, 0d, 100d, 0.5d));
            Assert.Throws<ArgumentOutOfRangeException>(() =>
                new FrameHitchDetector(events, 50d, 50d, 0.5d));
            Assert.Throws<ArgumentOutOfRangeException>(() =>
                new FrameHitchDetector(events, 50d, 100d, -1d));

            var detector = new FrameHitchDetector(events, 50d, 100d, 0d);
            detector.ObserveFrame(16d, 2d);
            Assert.Throws<ArgumentException>(() =>
                detector.ObserveFrame(16d, 1d));
        }

        [Test]
        public void Report_CapturesStablePointInTimeSnapshot()
        {
            var events = new RuntimeDiagnosticBuffer(2);
            var detector = new FrameHitchDetector(events, 30d, 100d, 0d);
            detector.ObserveFrame(40d, 1d);
            var context = new RuntimeDiagnosticContext(
                "session-1",
                "Unity Core Lab",
                "1.0",
                "Editor",
                "Test Device",
                "Test OS",
                "Test GPU");

            RuntimeDiagnosticReport report = RuntimeDiagnosticReport.Capture(
                events,
                detector,
                context,
                new DateTime(2026, 10, 13, 12, 0, 0, DateTimeKind.Utc),
                2d);
            events.Add(3d, "Test", DiagnosticSeverity.Info, "After capture");

            Assert.That(report.SchemaVersion, Is.EqualTo(1));
            Assert.That(report.EventCount, Is.EqualTo(1));
            Assert.That(report.GetEvent(0).Category,
                Is.EqualTo(FrameHitchDetector.Category));
            Assert.That(report.DetectedHitchCount, Is.EqualTo(1));
            Assert.That(report.WorstFrameMilliseconds, Is.EqualTo(40d));
            Assert.That(report.EventCapacity, Is.EqualTo(2));
            Assert.That(events.Count, Is.EqualTo(2));
        }

        [Test]
        public void JsonSerializer_EscapesTextAndUsesInvariantNumbers()
        {
            var events = new RuntimeDiagnosticBuffer(2);
            events.Add(
                1.25d,
                "Test",
                DiagnosticSeverity.Error,
                "Quote \" slash \\ line\n",
                40.5d,
                2);
            var detector = new FrameHitchDetector(events, 50d, 100d, 0d);
            var context = new RuntimeDiagnosticContext(
                "session-1",
                "Unity Core Lab",
                "1.0",
                "Editor",
                "Test Device",
                "Test OS",
                "Test GPU");
            RuntimeDiagnosticReport report = RuntimeDiagnosticReport.Capture(
                events,
                detector,
                context,
                new DateTime(2026, 10, 13, 12, 0, 0, DateTimeKind.Utc),
                2.5d);

            CultureInfo previousCulture = CultureInfo.CurrentCulture;
            try
            {
                CultureInfo.CurrentCulture = new CultureInfo("fr-FR");
                string json = RuntimeDiagnosticJsonSerializer.Serialize(report);

                StringAssert.Contains(
                    "\"capturedAtSeconds\": 2.5",
                    json);
                StringAssert.Contains(
                    "\"durationMilliseconds\": 40.5",
                    json);
                StringAssert.Contains(
                    "Quote \\\" slash \\\\ line\\n",
                    json);
                StringAssert.Contains("\"severity\": \"Error\"", json);
                StringAssert.Contains(
                    "\"maxGcAllocatedInFrameBytes\": null",
                    json);
            }
            finally
            {
                CultureInfo.CurrentCulture = previousCulture;
            }
        }

        [Test]
        public void MemoryMonitor_TracksPeaksAndCoalescesAllocationSpikes()
        {
            var events = new RuntimeDiagnosticBuffer(8);
            var monitor = new ManagedMemoryMonitor(
                events,
                warningAllocationBytes: 256 * 1024,
                criticalAllocationBytes: 1024 * 1024,
                reportCooldownSeconds: 1d);

            Assert.That(
                monitor.Observe(
                    new ManagedMemorySample(
                        300 * 1024,
                        10 * 1024 * 1024,
                        16 * 1024 * 1024,
                        100 * 1024 * 1024),
                    1d),
                Is.True);
            Assert.That(
                monitor.Observe(
                    new ManagedMemorySample(
                        400 * 1024,
                        11 * 1024 * 1024,
                        16 * 1024 * 1024,
                        101 * 1024 * 1024),
                    1.1d),
                Is.False);
            Assert.That(
                monitor.Observe(
                    new ManagedMemorySample(
                        2 * 1024 * 1024,
                        12 * 1024 * 1024,
                        20 * 1024 * 1024,
                        102 * 1024 * 1024),
                    1.2d),
                Is.True,
                "Critical allocation must bypass a warning cooldown.");

            Assert.That(events.Count, Is.EqualTo(2));
            Assert.That(events[0].Severity,
                Is.EqualTo(DiagnosticSeverity.Warning));
            Assert.That(events[1].Severity,
                Is.EqualTo(DiagnosticSeverity.Critical));
            Assert.That(events[1].OccurrenceCount, Is.EqualTo(2));
            Assert.That(monitor.ObservedSampleCount, Is.EqualTo(3));
            Assert.That(monitor.AllocationSpikeCount, Is.EqualTo(3));
            Assert.That(monitor.SuppressedReportCount, Is.EqualTo(1));
            Assert.That(
                monitor.MaxGcAllocatedInFrameBytes,
                Is.EqualTo(2 * 1024 * 1024));
            Assert.That(
                monitor.PeakGcUsedBytes,
                Is.EqualTo(12 * 1024 * 1024));
        }

        [Test]
        public void MemoryMonitor_PreservesUnavailableCounterStateInReport()
        {
            var events = new RuntimeDiagnosticBuffer(2);
            var hitchDetector = new FrameHitchDetector(
                events,
                30d,
                100d,
                0d);
            var memoryMonitor = new ManagedMemoryMonitor(
                events,
                256 * 1024,
                1024 * 1024,
                0d);
            memoryMonitor.Observe(
                new ManagedMemorySample(
                    ManagedMemorySample.Unavailable,
                    10 * 1024 * 1024,
                    16 * 1024 * 1024,
                    ManagedMemorySample.Unavailable),
                1d);
            var context = new RuntimeDiagnosticContext(
                "session-1",
                "Unity Core Lab",
                "1.0",
                "Editor",
                "Test Device",
                "Test OS",
                "Test GPU");
            RuntimeDiagnosticReport report = RuntimeDiagnosticReport.Capture(
                events,
                hitchDetector,
                context,
                new DateTime(2026, 10, 14, 12, 0, 0, DateTimeKind.Utc),
                2d,
                memoryMonitor);
            string json = RuntimeDiagnosticJsonSerializer.Serialize(report);

            Assert.That(
                report.MemoryStatistics.UnavailableAllocationSampleCount,
                Is.EqualTo(1));
            Assert.That(
                report.MemoryStatistics.HasGcAllocationData,
                Is.False);
            Assert.That(
                report.MemoryStatistics.HasGcUsedMemoryData,
                Is.True);
            StringAssert.Contains(
                "\"maxGcAllocatedInFrameBytes\": null",
                json);
            StringAssert.Contains(
                "\"peakGcUsedBytes\": 10485760",
                json);
        }

        [Test]
        public void MemorySampleAndMonitor_RejectInvalidInput()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() =>
                new ManagedMemorySample(-2, 0, 0, 0));

            var events = new RuntimeDiagnosticBuffer(2);
            Assert.Throws<ArgumentOutOfRangeException>(() =>
                new ManagedMemoryMonitor(events, 0, 1024, 0d));
            Assert.Throws<ArgumentOutOfRangeException>(() =>
                new ManagedMemoryMonitor(events, 1024, 1024, 0d));

            var monitor = new ManagedMemoryMonitor(events, 1024, 2048, 0d);
            monitor.Observe(new ManagedMemorySample(0, 0, 0, 0), 2d);
            Assert.Throws<ArgumentException>(() =>
                monitor.Observe(
                    new ManagedMemorySample(0, 0, 0, 0),
                    1d));
        }

        [Test]
        public void LogInbox_BoundsTextDropsOldestAndTracksDrain()
        {
            var inbox = new RuntimeLogInbox(
                capacity: 3,
                maximumMessageCharacters: 5,
                maximumStackTraceCharacters: 4);
            inbox.Enqueue(DiagnosticSeverity.Info, "One", string.Empty);
            inbox.Enqueue(DiagnosticSeverity.Info, "Two", string.Empty);
            inbox.Enqueue(
                DiagnosticSeverity.Warning,
                "1234567",
                "abcdef");
            inbox.Enqueue(DiagnosticSeverity.Error, "Four", string.Empty);

            RuntimeLogCaptureStatistics before = inbox.GetStatistics();
            Assert.That(before.ReceivedCount, Is.EqualTo(4));
            Assert.That(before.PendingCount, Is.EqualTo(3));
            Assert.That(before.DroppedCount, Is.EqualTo(1));
            Assert.That(before.TruncatedCount, Is.EqualTo(1));

            Assert.That(inbox.TryDequeue(out RuntimeLogEntry first), Is.True);
            Assert.That(first.Message, Is.EqualTo("Two"));
            Assert.That(inbox.TryDequeue(out RuntimeLogEntry second), Is.True);
            Assert.That(second.Message, Is.EqualTo("12345"));
            Assert.That(second.StackTrace, Is.EqualTo("abcd"));
            Assert.That(inbox.TryDequeue(out RuntimeLogEntry third), Is.True);
            Assert.That(third.Message, Is.EqualTo("Four"));
            Assert.That(inbox.TryDequeue(out _), Is.False);

            RuntimeLogCaptureStatistics after = inbox.GetStatistics();
            Assert.That(after.PendingCount, Is.Zero);
            Assert.That(after.DrainedCount, Is.EqualTo(3));
        }

        [Test]
        public void LogInbox_AcceptsParallelProducersWithinFixedCapacity()
        {
            const int ThreadCount = 4;
            const int MessagesPerThread = 100;
            const int Capacity = 32;
            var inbox = new RuntimeLogInbox(Capacity, 64, 0);
            var threads = new Thread[ThreadCount];

            for (int threadIndex = 0;
                 threadIndex < ThreadCount;
                 threadIndex++)
            {
                int capturedIndex = threadIndex;
                threads[threadIndex] = new Thread(() =>
                {
                    for (int messageIndex = 0;
                         messageIndex < MessagesPerThread;
                         messageIndex++)
                    {
                        inbox.Enqueue(
                            DiagnosticSeverity.Info,
                            $"T{capturedIndex}:{messageIndex}",
                            string.Empty);
                    }
                });
                threads[threadIndex].Start();
            }

            foreach (Thread thread in threads)
            {
                thread.Join();
            }

            RuntimeLogCaptureStatistics statistics = inbox.GetStatistics();
            Assert.That(
                statistics.ReceivedCount,
                Is.EqualTo(ThreadCount * MessagesPerThread));
            Assert.That(statistics.PendingCount, Is.EqualTo(Capacity));
            Assert.That(
                statistics.DroppedCount,
                Is.EqualTo(
                    ThreadCount * MessagesPerThread - Capacity));
        }

        [Test]
        public void Report_SerializesLogBackpressureStatistics()
        {
            var events = new RuntimeDiagnosticBuffer(2);
            var hitchDetector = new FrameHitchDetector(
                events,
                30d,
                100d,
                0d);
            var inbox = new RuntimeLogInbox(2, 32, 32);
            inbox.Enqueue(DiagnosticSeverity.Info, "One", string.Empty);
            inbox.Enqueue(DiagnosticSeverity.Warning, "Two", "Stack");
            inbox.Enqueue(DiagnosticSeverity.Error, "Three", string.Empty);
            inbox.TryDequeue(out _);
            var context = new RuntimeDiagnosticContext(
                "session-1",
                "Unity Core Lab",
                "1.0",
                "Editor",
                "Test Device",
                "Test OS",
                "Test GPU");
            RuntimeDiagnosticReport report = RuntimeDiagnosticReport.Capture(
                events,
                hitchDetector,
                context,
                new DateTime(2026, 10, 14, 12, 0, 0, DateTimeKind.Utc),
                2d,
                memoryMonitor: null,
                logInbox: inbox);
            string json = RuntimeDiagnosticJsonSerializer.Serialize(report);

            Assert.That(report.LogCaptureStatistics.ReceivedCount,
                Is.EqualTo(3));
            Assert.That(report.LogCaptureStatistics.DroppedCount,
                Is.EqualTo(1));
            Assert.That(report.LogCaptureStatistics.DrainedCount,
                Is.EqualTo(1));
            Assert.That(report.LogCaptureStatistics.PendingCount,
                Is.EqualTo(1));
            StringAssert.Contains("\"logCaptureStatistics\":", json);
            StringAssert.Contains("\"droppedCount\": 1", json);
        }
    }
}
