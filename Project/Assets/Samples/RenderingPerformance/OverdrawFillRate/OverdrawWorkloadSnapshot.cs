using UnityEngine;

namespace UnityCoreLab.Samples.RenderingPerformance.OverdrawFillRate
{
    public readonly struct OverdrawWorkloadSnapshot
    {
        public OverdrawWorkloadSnapshot(
            OverdrawMode mode,
            int layerCount,
            int rendererCount,
            int materialCount,
            float coverageRatio,
            int fragmentIterations,
            Vector2 quadWorldSize)
        {
            Mode = mode;
            LayerCount = layerCount;
            RendererCount = rendererCount;
            MaterialCount = materialCount;
            CoverageRatio = coverageRatio;
            FragmentIterations = fragmentIterations;
            QuadWorldSize = quadWorldSize;
        }

        public OverdrawMode Mode { get; }

        public int LayerCount { get; }

        public int RendererCount { get; }

        public int MaterialCount { get; }

        /// <summary>
        /// Estimated fraction of screen pixels covered by every layer.
        /// </summary>
        public float CoverageRatio { get; }

        /// <summary>
        /// Matched fragment-ALU iterations used by both rendering modes.
        /// </summary>
        public int FragmentIterations { get; }

        public Vector2 QuadWorldSize { get; }

        /// <summary>
        /// Deterministic pressure indicator, not a GPU timing measurement.
        /// </summary>
        public float EquivalentFullscreenLayers => LayerCount * CoverageRatio;

        /// <summary>
        /// Relative fragment-work pressure, not a hardware timing measurement.
        /// </summary>
        public float RelativeFragmentWork =>
            EquivalentFullscreenLayers * FragmentIterations;
    }
}
