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

Builds controlled rendering workloads for both CPU submission and GPU pixel
pressure. The submission sample compares unique materials, shared material
renderers, MaterialPropertyBlock overrides, and explicit GPU instancing. The
overdraw sample keeps geometry and layer counts matched while switching between
opaque depth writes and transparent alpha blending at controlled screen
coverage. The LOD/culling sample separates mesh-detail reduction from an
explicit distance and camera-frustum visibility policy.
The same LOD/culling scene also includes a controlled frame-timing capture that
uses a fixed warm-up and sampling window instead of comparing instantaneous FPS.

- Rendering sample: `Project/Assets/Samples/RenderingPerformance`
- Submission scene: `Project/Assets/Samples/RenderingPerformance/RenderingPerformanceDemo.unity`
- Overdraw scene: `Project/Assets/Samples/RenderingPerformance/OverdrawFillRate/OverdrawFillRateDemo.unity`
- LOD/culling scene: `Project/Assets/Samples/RenderingPerformance/LodCulling/DetailCullingDemo.unity`
- Frame-timing capture: `Project/Assets/Samples/RenderingPerformance/FrameTiming`
- PlayMode tests: `Project/Assets/Tests/PlayMode/RenderingPerformance`
- Design note: `docs/04-rendering-performance.md`
- Overdraw review: `docs/04-过度绘制与填充率技术回顾.md`
- Frame-timing review: `docs/04-CPU与GPU帧时序指标技术回顾.md`

### 05 Runtime Diagnostics and Fault Injection

Builds a bounded runtime diagnostic pipeline instead of an unbounded debug log.
The sample detects warning and critical frame hitches from a monotonic clock,
coalesces repeated reports during a cooldown, retains chronological events in
a fixed-capacity ring buffer, and exposes safe main-thread stall and
handled-exception injection controls. An explicitly requested point-in-time
snapshot combines those events with hitch statistics and runtime environment
metadata, then previews or locally exports deterministic JSON. The third stage
adapts Unity memory counters into an engine-independent monitor, records
bounded managed-allocation spikes, and preserves unavailable counters as
`null` rather than reporting a misleading zero.

- Runtime code: `Project/Assets/Runtime/Diagnostics`
- Interactive scene: `Project/Assets/Samples/RuntimeDiagnostics/RuntimeDiagnosticsDemo.unity`
- EditMode tests: `Project/Assets/Tests/EditMode/RuntimeDiagnostics`
- PlayMode test: `Project/Assets/Tests/PlayMode/RuntimeDiagnosticsDemoPlayModeTests.cs`
- Design note: `docs/05-runtime-diagnostics.md`

## Roadmap

- Mobile-device rendering case studies
- Log capture, upload policy, and platform crash/ANR adapters

Copyright © 2026 Jiangbo Li. All rights reserved.
