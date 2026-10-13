using System;
using NUnit.Framework;
using UnityCoreLab.Samples.RenderingPerformance.FrameTiming;

namespace UnityCoreLab.Tests.PlayMode.RenderingPerformance
{
    public sealed class FrameTimingStatisticsPlayModeTests
    {
        [Test]
        public void Create_ComputesInterpolatedMedianAndP95()
        {
            FrameTimingDistribution distribution =
                FrameTimingStatistics.Create(new[] {4d, 1d, 3d, 2d});

            Assert.That(distribution.SampleCount, Is.EqualTo(4));
            Assert.That(distribution.MedianMilliseconds, Is.EqualTo(2.5d));
            Assert.That(distribution.P95Milliseconds,
                Is.EqualTo(3.85d).Within(0.0001d));
        }

        [Test]
        public void Create_FiltersUnavailableTimingValues()
        {
            FrameTimingDistribution distribution =
                FrameTimingStatistics.Create(new[]
                {
                    0d,
                    -1d,
                    double.NaN,
                    double.PositiveInfinity,
                    5d
                });

            Assert.That(distribution.SampleCount, Is.EqualTo(1));
            Assert.That(distribution.MedianMilliseconds, Is.EqualTo(5d));
            Assert.That(distribution.P95Milliseconds, Is.EqualTo(5d));
        }

        [Test]
        public void Create_RejectsNullSamples()
        {
            Assert.Throws<ArgumentNullException>(() =>
                FrameTimingStatistics.Create(null));
        }
    }
}
