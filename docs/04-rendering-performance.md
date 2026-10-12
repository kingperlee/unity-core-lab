# Rendering Performance and Submission Analysis

This module builds controlled rendering workloads before making optimization
claims. The goal is to connect scene structure and material ownership to data
from Unity Profiler and Frame Debugger.

## 04.1 Submission and material baseline

The demo renders the same cube grid through three strategies:

1. **Unique material Renderers** create one GameObject, MeshRenderer, and cloned
   material per logical instance. GPU instancing is disabled on those clones.
2. **Shared material + MaterialPropertyBlock** keeps one shared material while
   retaining one Renderer per instance and supplies color overrides without
   calling `renderer.material`.
3. **Explicit GPU instancing** removes per-instance GameObjects and Renderers,
   stores matrices in managed batches, and calls `Graphics.DrawMeshInstanced`.

`Graphics.DrawMeshInstanced` accepts at most 1023 matrices per call. The
workload splits larger grids into explicit batches and exposes that batch count
rather than hiding it behind a helper.

## What the counters mean

The overlay reads these frame-wide `ProfilerRecorder` counters when supported:

- `Draw Calls Count`
- `Batches Count`
- `SetPass Calls Count`

They include the camera, UI, and other rendering work in the frame. The logical
Renderer/material counts shown beside them are deterministic workload
structure, while the Profiler counters show what Unity actually submitted.
They should not be treated as interchangeable values.

Material sharing also does not guarantee a particular batch count. Shader
variants, render state, MaterialPropertyBlock data, SRP Batcher compatibility,
GPU instancing support, shadows, and render passes all affect the final result.
Use Frame Debugger to inspect why Unity grouped or separated individual draws.

## Running the sample

Open
`Project/Assets/Samples/RenderingPerformance/RenderingPerformanceDemo.unity`,
enter Play Mode, and switch among the three modes at 100, 400, and 1000
instances. Allow at least one frame after switching before comparing counters.

Run the `UnityCoreLab.Tests.PlayMode.RenderingPerformance` namespace to verify
material ownership, Renderer counts, cleanup across rebuilds, and the 1023-item
explicit instancing boundary. The test intentionally avoids fixed draw-call or
timing thresholds because those depend on the render pipeline and platform.

For a detailed Chinese-language explanation of Draw Calls, Statistics Batches,
SetPass calls, SRP batches, and the current measurements, see
[`04-rendering-submission-memo.zh-CN.md`](04-rendering-submission-memo.zh-CN.md).
