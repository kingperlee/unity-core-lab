using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using UnityCoreLab.AssetLifecycle;

namespace UnityCoreLab.Samples.AssetLifecycle
{
    /// <summary>
    /// UniTask entry points for the Task-based shared asset store.
    /// </summary>
    public static class UniTaskAssetStoreExtensions
    {
        public static async UniTask<AssetLease<TAsset>> AcquireUniTaskAsync<TAsset>(
            this AsyncSharedAssetStore<TAsset> store,
            string key,
            CancellationToken cancellationToken = default,
            PlayerLoopTiming continuationTiming = PlayerLoopTiming.Update)
            where TAsset : class
        {
            if (store == null)
            {
                throw new ArgumentNullException(nameof(store));
            }

            AssetLease<TAsset> lease = null;
            try
            {
                // Do not capture Task's synchronization context. Re-entry into the
                // Unity main thread is explicit and visible at this boundary.
                lease = await store
                    .AcquireAsync(key, cancellationToken)
                    .AsUniTask(useCurrentSynchronizationContext: false);

                await UniTask.SwitchToMainThread(
                    continuationTiming,
                    cancellationToken);
                cancellationToken.ThrowIfCancellationRequested();
                return lease;
            }
            catch
            {
                // Cancellation may happen after the Task produced a lease but before
                // the PlayerLoop continuation. Do not leak that ownership claim.
                lease?.Dispose();
                throw;
            }
        }

        public static async UniTask<AssetLease<TAsset>> AcquireForOwnerAsync<TAsset>(
            this AsyncSharedAssetStore<TAsset> store,
            string key,
            AssetLeaseScope owner,
            CancellationToken cancellationToken = default,
            PlayerLoopTiming continuationTiming = PlayerLoopTiming.Update)
            where TAsset : class
        {
            if (owner == null)
            {
                throw new ArgumentNullException(nameof(owner));
            }

            CancellationToken lifetimeToken = owner.destroyCancellationToken;
            CancellationTokenSource linkedCancellation = null;
            CancellationToken operationToken = lifetimeToken;

            if (cancellationToken.CanBeCanceled)
            {
                linkedCancellation = CancellationTokenSource.CreateLinkedTokenSource(
                    lifetimeToken,
                    cancellationToken);
                operationToken = linkedCancellation.Token;
            }

            AssetLease<TAsset> lease = null;
            try
            {
                lease = await store.AcquireUniTaskAsync(
                    key,
                    operationToken,
                    continuationTiming);

                if (!owner.TryTrack(lease))
                {
                    lease.Dispose();
                    lease = null;
                    throw new OperationCanceledException(operationToken);
                }

                return lease;
            }
            catch
            {
                lease?.Dispose();
                throw;
            }
            finally
            {
                linkedCancellation?.Dispose();
            }
        }
    }
}
