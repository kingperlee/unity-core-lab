using System;

namespace UnityCoreLab.Diagnostics
{
    /// <summary>
    /// Engine-independent memory counter sample. A value of -1 means that the
    /// source counter is unavailable on the current platform or build type.
    /// </summary>
    public readonly struct ManagedMemorySample
    {
        public const long Unavailable = -1L;

        public ManagedMemorySample(
            long gcAllocatedInFrameBytes,
            long gcUsedBytes,
            long gcReservedBytes,
            long systemUsedBytes)
        {
            ValidateCounter(
                gcAllocatedInFrameBytes,
                nameof(gcAllocatedInFrameBytes));
            ValidateCounter(gcUsedBytes, nameof(gcUsedBytes));
            ValidateCounter(gcReservedBytes, nameof(gcReservedBytes));
            ValidateCounter(systemUsedBytes, nameof(systemUsedBytes));

            GcAllocatedInFrameBytes = gcAllocatedInFrameBytes;
            GcUsedBytes = gcUsedBytes;
            GcReservedBytes = gcReservedBytes;
            SystemUsedBytes = systemUsedBytes;
        }

        public long GcAllocatedInFrameBytes { get; }

        public long GcUsedBytes { get; }

        public long GcReservedBytes { get; }

        public long SystemUsedBytes { get; }

        public bool HasGcAllocatedInFrame =>
            GcAllocatedInFrameBytes != Unavailable;

        public bool HasGcUsedMemory => GcUsedBytes != Unavailable;

        public bool HasGcReservedMemory => GcReservedBytes != Unavailable;

        public bool HasSystemUsedMemory => SystemUsedBytes != Unavailable;

        private static void ValidateCounter(long value, string parameterName)
        {
            if (value < Unavailable)
            {
                throw new ArgumentOutOfRangeException(parameterName);
            }
        }
    }
}
