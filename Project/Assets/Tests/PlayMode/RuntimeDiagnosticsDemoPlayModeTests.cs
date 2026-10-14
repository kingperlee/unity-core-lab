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

            RuntimeDiagnosticReport report = demo.CaptureReport();
            string json = demo.BuildReportJson();
            Assert.That(report.EventCount, Is.EqualTo(3));
            Assert.That(report.Context.SessionId, Is.Not.EqualTo("Unknown"));
            Assert.That(report.MemoryStatistics.AllocationSpikeCount,
                Is.EqualTo(1));
            StringAssert.Contains("\"schemaVersion\": 1", json);
            StringAssert.Contains("\"events\":", json);
            StringAssert.Contains("\"memoryStatistics\":", json);
            Assert.That(demo.LatestReportJson, Is.EqualTo(json));

            demo.ClearDiagnostics();
            Assert.That(demo.Events.Count, Is.Zero);
            Assert.That(demo.HitchDetector.DetectedHitchCount, Is.Zero);
            Assert.That(demo.MemoryMonitor.ObservedSampleCount, Is.Zero);
            Assert.That(demo.LatestReportJson, Is.Null);

            Object.Destroy(root);
            yield return null;
        }
    }
}
