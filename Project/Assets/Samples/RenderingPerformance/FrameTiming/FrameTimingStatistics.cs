using System;
using System.Collections.Generic;

namespace UnityCoreLab.Samples.RenderingPerformance.FrameTiming
{
    public static class FrameTimingStatistics
    {
        public static FrameTimingDistribution Create(
            IReadOnlyList<double> samples)
        {
            if (samples == null)
            {
                throw new ArgumentNullException(nameof(samples));
            }

            var validSamples = new List<double>(samples.Count);
            for (int index = 0; index < samples.Count; index++)
            {
                double sample = samples[index];
                if (sample > 0d &&
                    !double.IsNaN(sample) &&
                    !double.IsInfinity(sample))
                {
                    validSamples.Add(sample);
                }
            }

            if (validSamples.Count == 0)
            {
                return default(FrameTimingDistribution);
            }

            validSamples.Sort();
            return new FrameTimingDistribution(
                validSamples.Count,
                Percentile(validSamples, 0.5d),
                Percentile(validSamples, 0.95d));
        }

        private static double Percentile(
            IReadOnlyList<double> sortedSamples,
            double percentile)
        {
            double position = (sortedSamples.Count - 1) * percentile;
            int lowerIndex = (int)Math.Floor(position);
            int upperIndex = (int)Math.Ceiling(position);
            if (lowerIndex == upperIndex)
            {
                return sortedSamples[lowerIndex];
            }

            double fraction = position - lowerIndex;
            return sortedSamples[lowerIndex] +
                   (sortedSamples[upperIndex] - sortedSamples[lowerIndex]) *
                   fraction;
        }
    }
}
