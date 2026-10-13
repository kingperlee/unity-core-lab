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

            demo.ClearDiagnostics();
            Assert.That(demo.Events.Count, Is.Zero);
            Assert.That(demo.HitchDetector.DetectedHitchCount, Is.Zero);

            Object.Destroy(root);
            yield return null;
        }
    }
}
