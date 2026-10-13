using System;
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
    }
}
