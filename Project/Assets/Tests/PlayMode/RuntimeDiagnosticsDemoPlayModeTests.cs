using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using UnityCoreLab.Diagnostics;
using UnityCoreLab.Samples.RuntimeDiagnostics;

namespace UnityCoreLab.Tests.PlayMode
{
    public sealed class RuntimeDiagnosticsDemoPlayModeTests
    {
        [UnityTest]
        public IEnumerator Demo_RecordsHitchAndHandledException()
        {
            var root = new GameObject("Runtime Diagnostics Demo Test");
            var demo = root.AddComponent<RuntimeDiagnosticsDemo>();

            Assert.That(demo.IsInitialized, Is.True);
            Assert.That(demo.Events.Capacity,
                Is.EqualTo(RuntimeDiagnosticsDemo.EventCapacity));
            Assert.That(
                demo.ObserveFrameForTesting(40d, 1d),
                Is.True);
            demo.InjectHandledException();

            Assert.That(demo.Events.Count, Is.EqualTo(2));
            Assert.That(demo.Events[0].Category,
                Is.EqualTo(FrameHitchDetector.Category));
            Assert.That(demo.Events[1].Category, Is.EqualTo("Exception"));
            Assert.That(demo.Events[1].Severity,
                Is.EqualTo(DiagnosticSeverity.Error));

            Assert.That(
                demo.ObserveMemoryForTesting(
                    new ManagedMemorySample(
                        512 * 1024,
                        10 * 1024 * 1024,
                        16 * 1024 * 1024,
                        100 * 1024 * 1024),
                    2d),
                Is.True);
            demo.LogInbox.Enqueue(
                DiagnosticSeverity.Warning,
                "Synthetic Unity warning.",
                "Synthetic stack.");
            Assert.That(demo.DrainLogsForTesting(3d, 1), Is.EqualTo(1));
            Assert.That(
                demo.Events[demo.Events.Count - 1].Category,
                Is.EqualTo("UnityLog"));

            Assert.That(
                demo.SimulatePendingPlatformIncident(
                    PlatformIncidentKind.NativeCrash,
                    1700000000000L),
                Is.True);
            Assert.That(
                demo.SimulatePendingPlatformIncident(
                    PlatformIncidentKind.ApplicationNotResponding,
                    1700000001000L),
                Is.True);
            Assert.That(demo.ImportPendingPlatformIncidents(3.5d),
                Is.EqualTo(2));
            Assert.That(demo.Events[demo.Events.Count - 2].Category,
                Is.EqualTo("NativeCrash"));
            Assert.That(demo.Events[demo.Events.Count - 1].Category,
                Is.EqualTo("ANR"));

            RuntimeDiagnosticReport report = demo.CaptureReport();
            string json = demo.BuildReportJson();
            Assert.That(report.EventCount, Is.EqualTo(6));
            Assert.That(report.Context.SessionId, Is.Not.EqualTo("Unknown"));
            Assert.That(report.MemoryStatistics.AllocationSpikeCount,
                Is.EqualTo(1));
            Assert.That(report.LogCaptureStatistics.DrainedCount,
                Is.EqualTo(1));
            Assert.That(report.PlatformIncidentStatistics.ImportedCount,
                Is.EqualTo(2));
            StringAssert.Contains("\"schemaVersion\": 1", json);
            StringAssert.Contains("\"events\":", json);
            StringAssert.Contains("\"memoryStatistics\":", json);
            StringAssert.Contains("\"deliveryStatistics\":", json);
            StringAssert.Contains("\"platformIncidentStatistics\":", json);
            Assert.That(demo.LatestReportJson, Is.EqualTo(json));

            Assert.That(
                demo.QueueCurrentReport(4d, out long reportId),
                Is.EqualTo(DiagnosticEnqueueResult.Accepted));
            Assert.That(reportId, Is.GreaterThan(0));
            Assert.That(
                demo.SimulateNextDelivery(
                    DiagnosticDeliveryOutcome.RetryableFailure,
                    4d),
                Is.True);
            Assert.That(
                demo.SimulateNextDelivery(
                    DiagnosticDeliveryOutcome.Succeeded,
                    4.5d),
                Is.False);
            Assert.That(
                demo.SimulateNextDelivery(
                    DiagnosticDeliveryOutcome.Succeeded,
                    5d),
                Is.True);
            Assert.That(demo.Outbox.GetStatistics().DeliveredCount,
                Is.EqualTo(1));

            demo.ClearDiagnostics();
            Assert.That(demo.Events.Count, Is.Zero);
            Assert.That(demo.HitchDetector.DetectedHitchCount, Is.Zero);
            Assert.That(demo.MemoryMonitor.ObservedSampleCount, Is.Zero);
            Assert.That(demo.LogInbox.GetStatistics().ReceivedCount, Is.Zero);
            Assert.That(demo.Outbox.GetStatistics().AcceptedCount, Is.Zero);
            Assert.That(
                demo.PlatformIncidentImporter.GetStatistics().ImportedCount,
                Is.Zero);
            Assert.That(demo.PlatformIncidentSource.PendingCount, Is.Zero);
            Assert.That(demo.LatestReportJson, Is.Null);

            Object.Destroy(root);
            yield return null;
        }
    }
}
