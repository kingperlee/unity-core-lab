using System.Threading;
using Cysharp.Threading.Tasks;

namespace UnityCoreLab.Samples.AssetLifecycle
{
    /// <summary>
    /// Unity-facing load boundary. Implementations may await Resources,
    /// Addressables, AssetBundles, or another Unity-native async handle.
    /// </summary>
    public interface IUniTaskAssetProvider<TAsset>
        where TAsset : class
    {
        UniTask<TAsset> LoadAsync(
            string key,
            CancellationToken cancellationToken);

        void Unload(string key, TAsset asset);
    }
}
