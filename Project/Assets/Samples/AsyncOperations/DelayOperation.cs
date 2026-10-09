using System;
using System.Threading;
using System.Threading.Tasks;
using UnityCoreLab.AsyncOperations;

namespace UnityCoreLab.Samples.AsyncOperations
{
    public sealed class DelayOperation : IAsyncOperation
    {
        private readonly TimeSpan _delay;

        public DelayOperation(TimeSpan delay)
        {
            _delay = delay;
        }

        public async Task<OperationResult> ExecuteAsync(
            OperationContext context,
            CancellationToken cancellationToken)
        {
            await Task.Delay(_delay, cancellationToken);
            return OperationResult.Completed();
        }
    }
}
