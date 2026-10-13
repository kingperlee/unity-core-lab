# Unity Core Lab

Practical Unity samples covering asynchronous workflows, state machines, asset lifecycle, performance optimization, and runtime diagnostics.

This repository is a personal Unity engineering portfolio for technical demonstration and discussion. It focuses on reusable engineering concepts rather than production-ready frameworks.

## Environment

- Unity 2022.3.62f1
- Universal Render Pipeline 14.0.12
- Unity Test Framework 1.1.33
- UniTask 2.5.11
- Addressables 1.21.12

The Unity project is located in the `Project` directory.

## Labs

### 01 Async Operation Lifecycle

Demonstrates cancellation, timeout classification, exception normalization, observable lifecycle state, generation-based protection against stale asynchronous completions, Task/UniTask lifecycle differences, and safe worker-thread-to-main-thread handoff.

- Runtime code: `Project/Assets/Runtime/AsyncOperations`
- Interactive sample: `Project/Assets/Samples/AsyncOperations`
- EditMode tests: `Project/Assets/Tests/EditMode`
- PlayMode tests: `Project/Assets/Tests/PlayMode`
- Design note: `docs/01-async-operation-lifecycle.md`

### 02 Asset Handle Lifecycle and Ownership

Demonstrates explicit asset ownership through disposable leases, synchronous and asynchronous load deduplication by key, independent waiter cancellation, reference counting, deterministic last-owner unload, failure recovery, UniTask/PlayerLoop adaptation, GameObject-lifetime ownership, and Resources/Addressables provider integration.

- Runtime code: `Project/Assets/Runtime/AssetLifecycle`
- UniTask integration: `Project/Assets/Samples/AssetLifecycle`
- Interactive scene: `Project/Assets/Samples/AssetLifecycle/AssetLifecycleDemo.unity`
- EditMode tests: `Project/Assets/Tests/EditMode/AssetLifecycle`
- PlayMode tests: `Project/Assets/Tests/PlayMode/UniTaskAssetLifecyclePlayModeTests.cs`
- Design note: `docs/02-asset-handle-lifecycle.md`

### 03 Object Pooling and Allocation Analysis

Studies pooling as an explicit ownership and capacity policy. The pure C#
baseline implements prewarming, reference-identity return validation, bounded
inactive retention, overflow discard, failure cleanup, and observable reuse
statistics. The Unity adapter adds main-thread enforcement, hierarchy and
transform reset, component lifecycle hooks, and a visible GameObject demo. A
matched-workload runner compares Instantiate/Destroy with Rent/Return using
median elapsed time, current-thread managed allocation, and custom Profiler
markers without encoding a fragile "pooling must win" test assertion.

- Runtime code: `Project/Assets/Runtime/ObjectPooling`
- Unity integration: `Project/Assets/Samples/ObjectPooling`
- Interactive scene: `Project/Assets/Samples/ObjectPooling/ObjectPoolingDemo.unity`
- EditMode tests: `Project/Assets/Tests/EditMode/ObjectPooling`
- PlayMode tests: `Project/Assets/Tests/PlayMode/ObjectPooling`
- Design note: `docs/03-object-pooling-allocation.md`

### 04 Rendering Performance and Submission Analysis

Builds controlled rendering workloads with unique materials, a shared material
without overrides, shared material plus MaterialPropertyBlock, and explicit GPU
instancing with per-instance colors. The demo separates deterministic
scene-structure counts from frame-wide Profiler counters and exposes Unity's
1023-matrix instanced draw boundary for Frame Debugger analysis.

- Rendering sample: `Project/Assets/Samples/RenderingPerformance`
- Interactive scene: `Project/Assets/Samples/RenderingPerformance/RenderingPerformanceDemo.unity`
- PlayMode tests: `Project/Assets/Tests/PlayMode/RenderingPerformance`
- Design note: `docs/04-rendering-performance.md`

## Roadmap

- Frame Debugger case studies and overdraw comparison
- Runtime diagnostics and fault injection

Copyright © 2026 Jiangbo Li. All rights reserved.
