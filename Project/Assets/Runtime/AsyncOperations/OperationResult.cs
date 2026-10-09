namespace UnityCoreLab.AsyncOperations
{
    /// <summary>
    /// Immutable result returned by an operation and by the coordinator.
    /// </summary>
    public readonly struct OperationResult
    {
        public OperationResult(OperationState state, string error = null)
        {
            State = state;
            Error = error;
        }

        public OperationState State { get; }

        public string Error { get; }

        public bool IsSuccess => State == OperationState.Completed;

        public static OperationResult Completed() => new OperationResult(OperationState.Completed);

        public static OperationResult Cancelled() => new OperationResult(OperationState.Cancelled);

        public static OperationResult TimedOut() => new OperationResult(OperationState.TimedOut);

        public static OperationResult Failed(string error) => new OperationResult(OperationState.Failed, error);

        public static OperationResult Superseded() => new OperationResult(OperationState.Superseded);
    }
}
