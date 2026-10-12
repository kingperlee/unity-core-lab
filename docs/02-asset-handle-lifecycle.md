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

## 02.2 Async load coalescing and waiter cancellation

`AsyncSharedAssetStore<TAsset>` adds request concurrency without changing the lease
contract. Concurrent acquires for the same key share one provider task, but each
caller waits with its own cancellation token.

This distinction is important:

- cancelling one waiter stops only that caller's wait;
- it does not cancel the provider request while another caller still needs it;
- the provider receives the store-lifetime token rather than a caller token;
- successful waiters each convert into one reference-counted lease;
- failed entries are removed so a later acquire can retry;
- if every waiter cancels, a late provider result is unloaded immediately;
- a late provider failure with no remaining waiter is removed without leaving an
  unobserved faulted task;
- store disposal attempts every unload and aggregates cleanup failures only after
  all entries have been processed.

The store uses standard `Task` rather than UniTask because this ownership layer has
no Unity PlayerLoop dependency. A Unity-facing provider may still use UniTask or a
framework-native handle internally and adapt its completion to this boundary.

## Scope and next integration

The synchronous store remains intentionally single-threaded. The asynchronous store
protects its entry table because provider completions and caller cancellation may
resume on different threads.

The provider abstraction also keeps the core independent from `Resources`,
Addressables, AssetBundles, or a proprietary asset framework. A later Unity-facing
adapter can choose one of those mechanisms without changing the lease rules.
Provider implementations must also marshal Unity API calls to the main thread when
needed because load completion and release may be observed from any thread. The next
increment will add UniTask and Unity-lifecycle integration before the interactive
ownership demo.

## Run the tests

Open `Window > General > Test Runner`, select EditMode, and run
the `UnityCoreLab.Tests.EditMode.AssetLifecycle` namespace so both
`SharedAssetStoreTests` and `AsyncSharedAssetStoreTests` are included.

The tests verify shared loads, reference counts, last-owner unload, idempotent lease
release, independent keys, failed loads, store-wide cleanup, async request
coalescing, independent waiter cancellation, late-result and late-failure cleanup,
retry, API boundary validation, and aggregate disposal failure handling.
