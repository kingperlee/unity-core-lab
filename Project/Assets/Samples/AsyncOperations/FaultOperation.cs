using System;
using System.Threading;
using System.Threading.Tasks;
using UnityCoreLab.AsyncOperations;

namespace UnityCoreLab.Samples.AsyncOperations
{
    public sealed class FaultOperation : IAsyncOperation
    {
        public Task<OperationResult> ExecuteAsync(
            OperationContext context,
            CancellationToken cancellationToken)
        {
            throw new InvalidOperationException("Simulated operation failure.");
        }
    }
}
