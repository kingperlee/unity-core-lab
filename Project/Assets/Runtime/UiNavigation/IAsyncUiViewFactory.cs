using System.Threading;
using System.Threading.Tasks;

namespace UnityCoreLab.UiNavigation
{
    /// <summary>
    /// Creates one owned view instance. The returned Task must complete on the
    /// synchronization context required by the view implementation.
    /// </summary>
    public interface IAsyncUiViewFactory
    {
        Task<IUiView> CreateAsync(
            UiRoute route,
            CancellationToken cancellationToken);
    }
}
