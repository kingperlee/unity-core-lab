using System.Threading;
using System.Threading.Tasks;

namespace UnityCoreLab.AsyncOperations
{
    /// <summary>
    /// Represents cancellable asynchronous work with an explicit result.
    /// Implementations should throw OperationCanceledException when cancellation is observed.
    /// </summary>
    public interface IAsyncOperation
    {
        Task<OperationResult> ExecuteAsync(
            OperationContext context,
            CancellationToken cancellationToken);
    }
}
