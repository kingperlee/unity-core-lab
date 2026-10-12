using System.Threading;
using System.Threading.Tasks;

namespace UnityCoreLab.AssetLifecycle
{
    /// <summary>
    /// Loads assets asynchronously. The supplied cancellation token belongs to the
    /// store lifetime, not to any individual waiter. Load completion and Unload may
    /// be observed from any thread; Unity-facing implementations are responsible for
    /// marshalling engine API calls to the Unity main thread.
    /// </summary>
    public interface IAsyncAssetProvider<TAsset>
        where TAsset : class
    {
        Task<TAsset> LoadAsync(string key, CancellationToken cancellationToken);

        void Unload(string key, TAsset asset);
    }
}
