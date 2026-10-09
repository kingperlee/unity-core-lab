# Async Operation Lifecycle

This module demonstrates a small but complete asynchronous operation lifecycle:

- explicit lifecycle states;
- cooperative cancellation;
- timeout classification;
- exception normalization;
- generation-based stale-result protection;
- observable state transitions;
- EditMode tests for success and failure paths;
- UniTask integration with Unity's PlayerLoop and GameObject lifetime.

## Problem

Starting a second asynchronous request does not stop the first request from completing later. Without an ownership rule, the stale result can overwrite the state produced by the newer request.

## Approach

`AsyncOperationCoordinator` assigns a monotonically increasing version to every execution. Starting a new execution cancels the previous token. A completion may update observable state only when its version is still current.

Cancellation and timeout are deliberately represented as different results. Callers can therefore distinguish user intent from an operational deadline.

## Task baseline versus UniTask integration

The runtime coordinator intentionally uses `Task` and `CancellationToken` only. It remains independent from Unity and from third-party packages, so its state and ownership rules can be tested in EditMode.

The sample layer adds UniTask where Unity-specific behavior matters:

| Topic | Task baseline | UniTask sample |
| --- | --- | --- |
| Delay | `Task.Delay` | `UniTask.Delay` on `PlayerLoopTiming.Update` |
| Lifetime | caller-owned token | Unity `destroyCancellationToken` |
| Timeout | `CancellationTokenSource.CancelAfter` | `CancelAfterSlim` on the PlayerLoop |
| Fire and forget | task must still be observed | `Forget(exceptionHandler)` |
| Continuation | synchronization-context dependent | Unity PlayerLoop by default |
| Return value | reference-type `Task<T>` | struct-based `UniTask<T>` |

On Unity 2022.2 and newer, `MonoBehaviour.destroyCancellationToken` is built in. Older Unity versions can use UniTask's `GetCancellationTokenOnDestroy()` extension for the same ownership model.

UniTask instances should normally be awaited only once. Convert to `Task` only at integration boundaries, as the PlayMode tests do; production sample code awaits `UniTask` directly.

`UniTask.Delay` checks cancellation during the selected PlayerLoop phase by default. `cancelImmediately: true` reacts through a token registration and is more expensive, so it should be reserved for work that genuinely requires immediate cancellation.

For high-frequency cancellation paths, `SuppressCancellationThrow()` can return a cancellation flag instead of throwing `OperationCanceledException`. This optimization belongs at the lowest awaited source; the sample keeps the exception path because it makes timeout and lifetime classification easier to read.

The timeout sample uses `CancelAfterSlim` rather than UniTask's `.Timeout()` wrapper. The cancellation token reaches the underlying delay, so timed-out work actually stops instead of merely having its result ignored.

## Try the sample

1. Open the Unity project under `Project`.
2. Select `Unity Core Lab > Samples > Create Async Operation Demo Scene`.
3. Enter Play Mode in the generated scene.
4. Try normal completion, timeout, cancellation, superseding work, and exception injection.
5. Run the UniTask delay, then destroy its temporary owner to observe lifetime cancellation.
6. Run the UniTask timeout sample to compare `TimedOut` with `Cancelled`.
7. Trigger the UniTask fault to see `Forget(exceptionHandler)` observe the exception.

## Run the tests

Open `Window > General > Test Runner` and run both test assemblies:

- `UnityCoreLab.Tests.EditMode` validates cancellation, timeout, exceptions, and stale-result protection without entering Play Mode.
- `UnityCoreLab.Tests.PlayMode` validates component initialization, Task cancellation, UniTask PlayerLoop continuation, GameObject-lifetime cancellation, and fire-and-forget exception handling.
