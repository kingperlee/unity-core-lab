namespace UnityCoreLab.AssetLifecycle
{
    /// <summary>
    /// Supplies and unloads assets for a SharedAssetStore. The synchronous baseline
    /// keeps ownership rules separate from any particular Unity loading framework.
    /// </summary>
    public interface IAssetProvider<TAsset>
        where TAsset : class
    {
        TAsset Load(string key);

        void Unload(string key, TAsset asset);
    }
}
