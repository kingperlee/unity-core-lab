namespace UnityCoreLab.AsyncOperations
{
    /// <summary>
    /// Describes the externally observable lifecycle of an asynchronous operation.
    /// </summary>
    public enum OperationState
    {
        Idle,
        Running,
        Completed,
        Cancelled,
        TimedOut,
        Failed,
        Superseded
    }
}
