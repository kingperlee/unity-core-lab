using System;

namespace UnityCoreLab.AsyncOperations
{
    /// <summary>
    /// Identifies one execution generation. A newer generation supersedes older work.
    /// </summary>
    public readonly struct OperationContext
    {
        public OperationContext(int version, DateTimeOffset startedAt)
        {
            Version = version;
            StartedAt = startedAt;
        }

        public int Version { get; }

        public DateTimeOffset StartedAt { get; }
    }
}
