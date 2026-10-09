# Async Operation Lifecycle

This module demonstrates a small but complete asynchronous operation lifecycle:

- explicit lifecycle states;
- cooperative cancellation;
- timeout classification;
- exception normalization;
- generation-based stale-result protection;
- observable state transitions;
- EditMode tests for success and failure paths.

## Problem

Starting a second asynchronous request does not stop the first request from completing later. Without an ownership rule, the stale result can overwrite the state produced by the newer request.

## Approach

`AsyncOperationCoordinator` assigns a monotonically increasing version to every execution. Starting a new execution cancels the previous token. A completion may update observable state only when its version is still current.

Cancellation and timeout are deliberately represented as different results. Callers can therefore distinguish user intent from an operational deadline.

## Try the sample

1. Open the Unity project under `Project`.
2. Select `Unity Core Lab > Samples > Create Async Operation Demo Scene`.
3. Enter Play Mode in the generated scene.
4. Try normal completion, timeout, cancellation, superseding work, and exception injection.

## Run the tests

Open `Window > General > Test Runner` and run both test assemblies:

- `UnityCoreLab.Tests.EditMode` validates cancellation, timeout, exceptions, and stale-result protection without entering Play Mode.
- `UnityCoreLab.Tests.PlayMode` validates component initialization and GameObject-destruction cancellation in Unity's runtime lifecycle.
