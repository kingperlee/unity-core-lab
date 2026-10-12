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

## 02.3 UniTask and Unity lifetime integration

The UniTask layer lives in `Samples/AssetLifecycle`; it does not duplicate the
reference-counting state machine:

- `UniTaskAssetProviderAdapter<TAsset>` converts a Unity-facing UniTask provider to
  the Task-based core contract;
- provider loads enter and complete through a selected `PlayerLoopTiming`;
- a release triggered from a worker thread is posted back to Unity's PlayerLoop;
- `AcquireUniTaskAsync` exposes a UniTask caller boundary and explicitly switches
  back to the Unity main thread;
- `AcquireForOwnerAsync` links one waiter's cancellation to a GameObject lifetime;
- `AssetLeaseScope` releases successfully acquired leases during `OnDestroy`.

Destroying one owner cancels only that owner's wait. It does not cancel the shared
provider request while another waiter still needs the same key. Once acquisition
succeeds, the scope owns the lease, although callers may still dispose it early
because lease disposal is idempotent.

Task-to-UniTask conversion is intentionally limited to this integration boundary.
The core remains reusable and testable without a Unity PlayerLoop dependency.

The PlayMode cancellation test calls `SuppressCancellationThrow()` before converting
its UniTask to a `Task`. This keeps cancellation as an explicit `(IsCanceled,
Result)` value at the test boundary. Production UniTask code can instead await the
operation directly and catch `OperationCanceledException` when cancellation is part
of its control flow.

## Scope and next integration

The synchronous store remains intentionally single-threaded. The asynchronous store
protects its entry table because provider completions and caller cancellation may
resume on different threads.

The provider abstraction also keeps the core independent from `Resources`,
Addressables, AssetBundles, or a proprietary asset framework. A later Unity-facing
adapter can choose one of those mechanisms without changing the lease rules.
Provider implementations must also marshal Unity API calls to the main thread when
needed because load completion and release may be observed from any thread. The
UniTask adapter demonstrates that policy. A later increment can bind the provider
interface to Resources or Addressables and add an interactive ownership demo.

## Run the tests

Open `Window > General > Test Runner`, select EditMode, and run
the `UnityCoreLab.Tests.EditMode.AssetLifecycle` namespace so both
`SharedAssetStoreTests` and `AsyncSharedAssetStoreTests` are included.

The tests verify shared loads, reference counts, last-owner unload, idempotent lease
release, independent keys, failed loads, store-wide cleanup, async request
coalescing, independent waiter cancellation, late-result and late-failure cleanup,
retry, API boundary validation, and aggregate disposal failure handling.

Select PlayMode and run `UniTaskAssetLifecyclePlayModeTests` to verify that provider
calls execute on the main thread, GameObject destruction cancels only its waiter,
acquired leases are released with their owner, and worker-thread releases are
deferred to Unity's PlayerLoop.

## 02.4 Resources provider and interactive ownership demo

`ResourcesUniTaskAssetProvider<TAsset>` is the first real Unity asset API adapter.
It loads non-GameObject assets with `Resources.LoadAsync<TAsset>` and releases them
with `Resources.UnloadAsset` after the last lease ends.

A `ResourceRequest` cannot actually be cancelled. Cancelling only the UniTask await
would discard the eventual asset and prevent deterministic cleanup. The provider
therefore finishes the Unity request, unloads a late result when the store lifetime
has been cancelled, and only then reports cancellation to the core.

Open `Assets/Samples/AssetLifecycle/AssetLifecycleDemo.unity` and enter Play Mode.
The demo lets two GameObject owners request the same `TextAsset` concurrently. The
screen shows that:

1. two waiters create one underlying Resources load;
2. successful waiters produce two independent leases;
3. releasing or destroying one owner leaves the asset loaded;
4. destroying the final owner triggers exactly one unload.

Run `ResourcesAssetLifecyclePlayModeTests` to exercise the real Resources provider
and the complete interactive-demo ownership flow.

## 02.5 Addressables handles versus consumer leases

Addressables adds another ownership layer: every successful load returns an
`AsyncOperationHandle<TAsset>` that must later be released. The provider keeps that
handle separate from the store's consumer reference count:

- concurrent same-key callers still create one provider request and one handle;
- each successful caller receives its own store lease;
- releasing an intermediate lease does not release the Addressables handle;
- releasing the final lease calls `Addressables.Release(handle)` exactly once;
- failed or cancelled loads release their handle before propagating the result;
- a failed entry is removed, so the next acquire can create a fresh handle.

The adapter releases the handle rather than calling `Addressables.Release(asset)`.
Keeping the exact handle makes ownership explicit and avoids losing the operation
that represents downloaded bundles and dependency references.

`AddressablesAssetProviderPlayModeTests` uses completed ResourceManager operations
to test success, failure, retry, and exact release counts without requiring a remote
catalog or an Addressables content build.
