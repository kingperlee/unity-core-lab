using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace UnityCoreLab.UiPresentation
{
    /// <summary>
    /// Supplies presentation-ready items without knowing how they are rendered.
    /// </summary>
    public interface IUiContentSource<TItem>
    {
        Task<IReadOnlyList<TItem>> LoadAsync(
            CancellationToken cancellationToken);
    }
}
