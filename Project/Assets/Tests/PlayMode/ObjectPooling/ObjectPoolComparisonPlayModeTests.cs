using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using UnityCoreLab.Samples.ObjectPooling;

namespace UnityCoreLab.Tests.PlayMode.ObjectPooling
{
    public sealed class ObjectPoolComparisonPlayModeTests
    {
        [UnityTest]
        public IEnumerator Comparison_CompletesMatchedWorkAndReportsPoolEvidence()
        {
            const int operationsPerSample = 8;
            const int sampleCount = 3;
            var root = new GameObject("Object Pool Comparison Test");
            var runner = root.AddComponent<ObjectPoolComparisonRunner>();

            yield return runner.RunComparison(operationsPerSample, sampleCount);

            Assert.That(runner.IsRunning, Is.False);
            Assert.That(runner.HasResult, Is.True);
            Assert.That(runner.Status, Is.EqualTo("Complete"));

            ObjectPoolComparisonResult result = runner.LastResult;
            AssertSample(result.InstantiateDestroy, operationsPerSample, sampleCount);
            AssertSample(result.RentReturn, operationsPerSample, sampleCount);

            Assert.That(result.PoolSnapshot.CreatedCount,
                Is.EqualTo(operationsPerSample));
            Assert.That(result.PoolSnapshot.ActiveCount, Is.Zero);
            Assert.That(result.PoolSnapshot.AvailableCount,
                Is.EqualTo(operationsPerSample));
            Assert.That(result.PoolSnapshot.RentCount,
                Is.EqualTo(operationsPerSample * (sampleCount + 1)));
            Assert.That(result.PoolSnapshot.ReusedCount,
                Is.EqualTo(result.PoolSnapshot.RentCount));

            Object.Destroy(root);
            yield return null;
        }

        private static void AssertSample(
            ObjectPoolPerformanceSample sample,
            int operationsPerSample,
            int sampleCount)
        {
            Assert.That(sample.OperationsPerSample, Is.EqualTo(operationsPerSample));
            Assert.That(sample.SampleCount, Is.EqualTo(sampleCount));
            Assert.That(sample.MedianElapsedTicks, Is.GreaterThan(0));
            Assert.That(sample.MedianManagedAllocatedBytes, Is.GreaterThanOrEqualTo(0));
            Assert.That(sample.MicrosecondsPerOperation, Is.GreaterThan(0d));
            Assert.That(sample.ManagedBytesPerOperation, Is.GreaterThanOrEqualTo(0d));
        }
    }
}
