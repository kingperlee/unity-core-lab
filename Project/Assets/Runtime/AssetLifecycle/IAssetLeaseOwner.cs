namespace UnityCoreLab.AssetLifecycle
{
    internal interface IAssetLeaseOwner<TAsset>
        where TAsset : class
    {
        bool IsLeaseAlive(string key, TAsset asset);

        void Release(string key, TAsset asset);
    }
}
