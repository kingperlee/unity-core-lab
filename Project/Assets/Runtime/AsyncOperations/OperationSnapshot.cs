using System;

namespace UnityCoreLab.AsyncOperations
{
    /// <summary>
    /// Read-only state published whenever the active operation changes lifecycle state.
    /// </summary>
    public readonly struct OperationSnapshot
    {
        public OperationSnapshot(int version, OperationState state, TimeSpan elapsed, string error = null)
        {
            Version = version;
            State = state;
            Elapsed = elapsed;
            Error = error;
        }

        public int Version { get; }

        public OperationState State { get; }

        public TimeSpan Elapsed { get; }

        public string Error { get; }
    }
}
