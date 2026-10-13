namespace UnityCoreLab.Samples.RenderingPerformance
{
    public readonly struct RenderingWorkloadSnapshot
    {
        public RenderingWorkloadSnapshot(
            RenderingWorkloadMode mode,
            int instanceCount,
            int rendererCount,
            int materialCount,
            int explicitInstancedBatchCount,
            int explicitColorCount)
        {
            Mode = mode;
            InstanceCount = instanceCount;
            RendererCount = rendererCount;
            MaterialCount = materialCount;
            ExplicitInstancedBatchCount = explicitInstancedBatchCount;
            ExplicitColorCount = explicitColorCount;
        }

        public RenderingWorkloadMode Mode { get; }

        public int InstanceCount { get; }

        public int RendererCount { get; }

        public int MaterialCount { get; }

        public int ExplicitInstancedBatchCount { get; }

        public int ExplicitColorCount { get; }
    }
}
