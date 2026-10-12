using System;

namespace UnityCoreLab.AssetLifecycle
{
    /// <summary>
    /// Represents one ownership claim on a shared asset. Disposing the lease releases
    /// exactly one reference; the asset is unloaded only after the final lease ends.
    /// </summary>
    public sealed class AssetLease<TAsset> : IDisposable
        where TAsset : class
    {
        private IAssetLeaseOwner<TAsset> _owner;
        private readonly TAsset _asset;

        internal AssetLease(
            IAssetLeaseOwner<TAsset> owner,
            string key,
            TAsset asset)
        {
            _owner = owner ?? throw new ArgumentNullException(nameof(owner));
            Key = key ?? throw new ArgumentNullException(nameof(key));
            _asset = asset ?? throw new ArgumentNullException(nameof(asset));
        }

        public string Key { get; }

        public bool IsReleased =>
            _owner == null || !_owner.IsLeaseAlive(Key, _asset);

        public TAsset Asset
        {
            get
            {
                if (IsReleased)
                {
                    throw new ObjectDisposedException(
                        nameof(AssetLease<TAsset>),
                        $"The lease for '{Key}' has already been released.");
                }

                return _asset;
            }
        }

        public void Dispose()
        {
            IAssetLeaseOwner<TAsset> owner = _owner;
            if (owner == null)
            {
                return;
            }

            // Mark this lease as released before calling external unload code. Even if
            // the provider throws, the same ownership claim must never release twice.
            _owner = null;
            owner.Release(Key, _asset);
        }
    }
}
