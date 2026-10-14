using System;
using NUnit.Framework;
using UnityCoreLab.Diagnostics;

namespace UnityCoreLab.Tests.EditMode.RuntimeDiagnostics
{
    public sealed class PlatformIncidentImporterTests
    {
        [Test]
        public void BufferedSource_PreservesFifoAndAcknowledgesOnlyHead()
        {
            var source = new BufferedPlatformIncidentSource(2);
            PlatformIncidentRecord first = CreateIncident(
                "first",
                PlatformIncidentKind.NativeCrash);
            PlatformIncidentRecord second = CreateIncident(
                "second",
                PlatformIncidentKind.ApplicationNotResponding);

            Assert.That(source.TryEnqueue(first), Is.True);
            Assert.That(source.TryEnqueue(second), Is.True);
            Assert.That(source.TryEnqueue(first), Is.False);
            Assert.That(source.TryPeek(out PlatformIncidentRecord head), Is.True);
            Assert.That(head.IncidentId, Is.EqualTo("first"));
            Assert.Throws<InvalidOperationException>(() =>
                source.Acknowledge("second"));

            source.Acknowledge("first");
            Assert.That(source.TryPeek(out head), Is.True);
            Assert.That(head.IncidentId, Is.EqualTo("second"));
        }

        [Test]
        public void Importer_BoundsWorkAndTruncatesRetainedText()
        {
            var events = new RuntimeDiagnosticBuffer(4);
            var source = new BufferedPlatformIncidentSource(2);
            var importer = new PlatformIncidentImporter(
                events,
                rememberedIncidentCapacity: 4,
                maximumSummaryCharacters: 4,
                maximumDetailsCharacters: 3);
            source.TryEnqueue(new PlatformIncidentRecord(
                "crash-1",
                PlatformIncidentKind.NativeCrash,
                1234L,
                "TestSdk",
                "summary",
                "details"));
            source.TryEnqueue(CreateIncident(
                "anr-1",
                PlatformIncidentKind.ApplicationNotResponding));

            Assert.That(importer.ImportAvailable(source, 2d, 1), Is.EqualTo(1));
            Assert.That(source.PendingCount, Is.EqualTo(1));
            Assert.That(events.Count, Is.EqualTo(1));
            Assert.That(events[0].Category, Is.EqualTo("NativeCrash"));
            Assert.That(events[0].Severity,
                Is.EqualTo(DiagnosticSeverity.Critical));
            StringAssert.Contains("summ", events[0].Message);
            StringAssert.Contains("det", events[0].Message);

            Assert.That(importer.ImportAvailable(source, 3d, 4), Is.EqualTo(1));
            Assert.That(events[1].Category, Is.EqualTo("ANR"));
            PlatformIncidentImportStatistics statistics =
                importer.GetStatistics();
            Assert.That(statistics.ImportedCount, Is.EqualTo(2));
            Assert.That(statistics.TruncatedCount, Is.EqualTo(2));
        }

        [Test]
        public void Importer_DeduplicatesRecordAfterAcknowledgementFailure()
        {
            var events = new RuntimeDiagnosticBuffer(4);
            var importer = new PlatformIncidentImporter(events, 4, 64, 64);
            var source = new FaultingSource(
                CreateIncident("same", PlatformIncidentKind.NativeCrash),
                failFirstAcknowledgement: true,
                failRead: false);

            Assert.That(importer.ImportAvailable(source, 1d, 1), Is.EqualTo(1));
            Assert.That(events.Count, Is.EqualTo(1));
            Assert.That(importer.ImportAvailable(source, 2d, 1), Is.Zero);
            Assert.That(events.Count, Is.EqualTo(1));
            Assert.That(source.HasPending, Is.False);

            PlatformIncidentImportStatistics statistics =
                importer.GetStatistics();
            Assert.That(statistics.ImportedCount, Is.EqualTo(1));
            Assert.That(statistics.DuplicateCount, Is.EqualTo(1));
            Assert.That(statistics.AcknowledgementFailureCount, Is.EqualTo(1));
        }

        [Test]
        public void Importer_ContainsSourceReadFailure()
        {
            var events = new RuntimeDiagnosticBuffer(2);
            var importer = new PlatformIncidentImporter(events, 2, 32, 32);
            var source = new FaultingSource(
                CreateIncident("unused", PlatformIncidentKind.NativeCrash),
                failFirstAcknowledgement: false,
                failRead: true);

            Assert.That(importer.ImportAvailable(source, 1d, 2), Is.Zero);
            Assert.That(events.Count, Is.Zero);
            Assert.That(
                importer.GetStatistics().SourceReadFailureCount,
                Is.EqualTo(1));
        }

        private static PlatformIncidentRecord CreateIncident(
            string incidentId,
            PlatformIncidentKind kind)
        {
            return new PlatformIncidentRecord(
                incidentId,
                kind,
                1000L,
                "TestSdk",
                "Incident summary",
                "Incident details");
        }

        private sealed class FaultingSource : IPlatformIncidentSource
        {
            private readonly PlatformIncidentRecord _incident;
            private readonly bool _failRead;
            private bool _failNextAcknowledgement;

            public FaultingSource(
                PlatformIncidentRecord incident,
                bool failFirstAcknowledgement,
                bool failRead)
            {
                _incident = incident;
                _failNextAcknowledgement = failFirstAcknowledgement;
                _failRead = failRead;
                HasPending = true;
            }

            public bool HasPending { get; private set; }

            public bool TryPeek(out PlatformIncidentRecord incident)
            {
                if (_failRead)
                {
                    throw new InvalidOperationException("Synthetic read failure.");
                }

                incident = _incident;
                return HasPending;
            }

            public void Acknowledge(string incidentId)
            {
                if (_failNextAcknowledgement)
                {
                    _failNextAcknowledgement = false;
                    throw new InvalidOperationException(
                        "Synthetic acknowledgement failure.");
                }

                HasPending = false;
            }
        }
    }
}
