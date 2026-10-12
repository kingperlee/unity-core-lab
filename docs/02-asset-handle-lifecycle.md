# Asset Handle Lifecycle and Ownership

This module separates an asset object from the right to keep that asset loaded.
Consumers receive an `AssetLease<TAsset>` instead of owning the raw asset directly.

## 02.1 Reference-counted ownership baseline

`SharedAssetStore<TAsset>` applies five rules:

1. the first acquire for a key asks the provider to load the asset;
2. later acquires for the same key share that instance;
3. every acquire returns a distinct lease and increments the reference count;
4. disposing a lease releases exactly one reference;
5. the provider unloads the asset only when its final lease is released.

The store itself is also an owner boundary. Disposing it unloads every remaining
entry and invalidates outstanding leases. This provides a deterministic shutdown
path for scene scopes, feature scopes, or an application-level asset service.

## Why use a lease?

Passing raw assets between systems hides responsibility: it is unclear which caller
must release the resource, whether another caller still uses it, and whether a
double release is possible. A disposable lease makes each ownership claim explicit.

```csharp
using (AssetLease<MyAsset> lease = store.Acquire("hero"))
{
    MyAsset asset = lease.Asset;
    // Use the asset while this ownership claim is alive.
}
```

Access after release throws `ObjectDisposedException`, while repeated `Dispose`
calls are harmless and never decrement the reference count twice.

## Scope of this baseline

The first implementation is synchronous and single-threaded on purpose. It teaches
ownership without mixing in request concurrency. The next increment will add shared
asynchronous loads and distinguish cancellation of one waiter from cancellation of
the underlying shared load.

The provider abstraction also keeps the core independent from `Resources`,
Addressables, AssetBundles, or a proprietary asset framework. A later Unity-facing
adapter can choose one of those mechanisms without changing the lease rules.

## Run the tests

Open `Window > General > Test Runner`, select EditMode, and run
`UnityCoreLab.Tests.EditMode.AssetLifecycle.SharedAssetStoreTests`.

The tests verify shared loads, reference counts, last-owner unload, idempotent lease
release, independent keys, failed loads, and store-wide cleanup.
