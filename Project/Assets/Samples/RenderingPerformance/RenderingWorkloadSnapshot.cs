namespace UnityCoreLab.Samples.RenderingPerformance
{
    public readonly struct RenderingWorkloadSnapshot
    {
        public RenderingWorkloadSnapshot(
            RenderingWorkloadMode mode,
            int instanceCount,
            int rendererCount,
            int materialCount,
            int explicitInstancedBatchCount)
        {
            Mode = mode;
            InstanceCount = instanceCount;
            RendererCount = rendererCount;
            MaterialCount = materialCount;
            ExplicitInstancedBatchCount = explicitInstancedBatchCount;
        }

        public RenderingWorkloadMode Mode { get; }

        public int InstanceCount { get; }

        public int RendererCount { get; }

        public int MaterialCount { get; }

        public int ExplicitInstancedBatchCount { get; }
    }
}
