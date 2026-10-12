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

## Scope and next increment

This baseline is intentionally single-threaded. Unity objects must be created,
activated, reset, and destroyed on the Unity main thread. The next increment
will adapt this policy to `GameObject`, define a reusable component reset
contract, and add a PlayMode demonstration before measuring allocations.

Run the `UnityCoreLab.Tests.EditMode.ObjectPooling` namespace in Test Runner to
verify prewarming, reuse, overflow, duplicate-return protection, callback
failure cleanup, and deterministic disposal.
