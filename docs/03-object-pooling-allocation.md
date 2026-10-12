# Object Pooling and Allocation Analysis

This module studies object pooling as an ownership and capacity policy rather
than treating it as a universal performance optimization.

## 03.1 Reusable pool baseline

`ReusableObjectPool<T>` is a pure C# baseline with no Unity dependency. It
separates five operations that are often hidden inside a pool implementation:

1. create a new item when no retained item is available;
2. activate an item when a caller rents it;
3. reset an item when the caller returns it;
4. retain the returned item only while the configured capacity allows it;
5. discard overflow and shutdown items through an explicit callback.

The pool tracks objects by reference identity. Returning the same item twice or
returning an item created by another pool throws instead of silently corrupting
the available stack.

`maxRetained` limits inactive objects, not active rentals. A load spike may
temporarily create more objects than this limit. When those objects return, the
pool retains only the configured number and discards the rest. This prevents a
short peak from permanently increasing retained memory.

## Prewarm and shutdown ownership

`Prewarm` fills the inactive stack up to a target count. It does not count as a
rent, but it does run the return/reset callback so prewarmed objects enter the
same inactive state as ordinary returned objects.

Disposing the pool discards every available item. Active items remain owned by
their callers; if they are returned after disposal, the pool discards them
instead of caching them. New rentals are rejected after disposal.

## Observable statistics

`PoolSnapshot` exposes:

- active, available, and total owned counts;
- created, rented, reused, returned, and discarded counts;
- peak simultaneously active objects;
- reuse ratio across rental operations.

These values make later allocation comparisons explainable. A lower allocation
count is useful only when it can be related to reuse, capacity, and retained
memory.

## 03.2 GameObject adapter and reset lifecycle

`GameObjectPool` keeps Unity-specific behavior outside the reusable C# pool. It
captures the thread that constructs it and rejects operations from other
threads before they can call Unity APIs. A rented object is moved beneath the
active root, reset to a known local transform, activated, and then notified
through `IPoolableGameObject.OnRentFromPool`.

Returning reverses that transition in a deliberate order:

1. components receive `OnReturnToPool` while the object is still active;
2. the object is deactivated and moved beneath the inactive root;
3. local position, rotation, and scale are restored;
4. the core pool either retains it or destroys it when capacity is full.

Reset callbacks are discovered once when an instance is created and cached for
later transitions. The hot rent/return path therefore avoids repeated
`GetComponentsInChildren` calls. The contract still requires each component to
clear its own transient state, such as velocity, timers, targets, subscriptions,
and particle state.

The interactive demo prewarms four cubes. Spawning six makes the difference
between active peak and inactive capacity visible: returning all six retains
four and discards two. A second burst then demonstrates reuse with live pool
statistics.

Open `Project/Assets/Samples/ObjectPooling/ObjectPoolingDemo.unity` to run the
sample. Run the `UnityCoreLab.Tests.PlayMode.ObjectPooling` namespace to verify
hierarchy and state reset, overflow destruction, main-thread enforcement, and
the complete demo cycle.

## 03.3 Matched allocation and timing comparison

`ObjectPoolComparisonRunner` performs the same number of lifecycle operations
with two strategies:

- instantiate every object and schedule every object for destruction;
- rent prewarmed objects and return every object to a capacity-matched pool.

Both paths operate on the same template and reuse a preallocated reference
array. Warm-up work is excluded, each Instantiate/Destroy sample is followed by
a frame so Unity can process deferred destruction, and results use the median
instead of a single frame. Elapsed time comes from `Stopwatch` and managed
allocation comes from `GC.GetAllocatedBytesForCurrentThread` around only the
matched workload.

The runner also emits these custom Profiler markers:

- `UnityCoreLab.ObjectPooling.InstantiateDestroy`
- `UnityCoreLab.ObjectPooling.RentReturn`

The UI deliberately labels Editor results as provisional. Editor extensions,
deep profiling, development instrumentation, target hardware, and the actual
component reset workload can all change the result. The PlayMode test verifies
equal operation counts and observable reuse; it does not assert that one timing
must always be lower than the other.

## Scope and next increment

Both layers are intentionally single-threaded. Unity objects must be created,
activated, reset, and destroyed on the Unity main thread. A later performance
increment can export target-device samples or add automated budget thresholds;
those thresholds should be platform-specific rather than copied from Editor
measurements.

Run the `UnityCoreLab.Tests.EditMode.ObjectPooling` namespace in Test Runner to
verify prewarming, reuse, overflow, duplicate-return protection, callback
failure cleanup, and deterministic disposal.
