namespace UnityCoreLab.Samples.RenderingPerformance.LodCulling
{
    public readonly struct DetailCullingSnapshot
    {
        public DetailCullingSnapshot(
            DetailCullingMode mode,
            int objectCount,
            int highDetailCount,
            int lowDetailCount,
            int policyCulledCount,
            int estimatedVisibleTriangleCount,
            float highDetailDistance,
            float cullDistance)
        {
            Mode = mode;
            ObjectCount = objectCount;
            HighDetailCount = highDetailCount;
            LowDetailCount = lowDetailCount;
            PolicyCulledCount = policyCulledCount;
            EstimatedVisibleTriangleCount = estimatedVisibleTriangleCount;
            HighDetailDistance = highDetailDistance;
            CullDistance = cullDistance;
        }

        public DetailCullingMode Mode { get; }

        public int ObjectCount { get; }

        public int HighDetailCount { get; }

        public int LowDetailCount { get; }

        /// <summary>
        /// Objects disabled by this sample's explicit policy. Unity can still
        /// internally frustum-cull enabled Renderers.
        /// </summary>
        public int PolicyCulledCount { get; }

        public int EstimatedVisibleTriangleCount { get; }

        public float HighDetailDistance { get; }

        public float CullDistance { get; }
    }
}
