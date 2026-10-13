# Rendering Performance and Submission Analysis

This module builds controlled rendering workloads before making optimization
claims. The goal is to connect scene structure and material ownership to data
from Unity Profiler and Frame Debugger.

## 04.1 Submission and material baseline

The demo renders the same cube grid through four strategies:

1. **Unique material Renderers** create one GameObject, MeshRenderer, and cloned
   material per logical instance. GPU instancing is disabled on those clones.
2. **Shared material Renderers** keep one material and apply no per-Renderer
   overrides. This is the control case for SRP Batcher-compatible sharing.
3. **Shared material + MaterialPropertyBlock** keeps one shared material while
   retaining one Renderer per instance and supplies color overrides without
   calling `renderer.material`.
4. **Explicit GPU instancing** removes per-instance GameObjects and Renderers,
   stores matrices and colors in managed batches, and calls
   `Graphics.DrawMeshInstanced` with a per-batch MaterialPropertyBlock.

`Graphics.DrawMeshInstanced` accepts at most 1023 matrices per call. The
workload splits larger grids into explicit batches and exposes that batch count
rather than hiding it behind a helper.

The Renderer modes use an explicit material asset backed by URP/Unlit so their
SRP Batcher behavior remains visible. Explicit instancing uses the
`UnityCoreLab/Rendering/InstancedColor` shader. That shader intentionally keeps
color in an instancing buffer rather than `UnityPerMaterial`, because its job is
to demonstrate per-instance data instead of the SRP Batcher path. The built-in
cube Mesh remains the common geometry for all four modes.

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
enter Play Mode, and switch among the four modes at 100, 400, and 1000
instances. Allow at least one frame after switching before comparing counters.

Run the `UnityCoreLab.Tests.PlayMode.RenderingPerformance` namespace to verify
material ownership, MaterialPropertyBlock separation, per-instance color data,
cleanup across rebuilds, and the 1023-item explicit instancing boundary. The
test intentionally avoids fixed draw-call or timing thresholds because those
depend on the render pipeline and platform.

For a detailed Chinese-language explanation of Draw Calls, Statistics Batches,
SetPass calls, SRP batches, and the current measurements, see
[`04-rendering-submission-memo.zh-CN.md`](04-rendering-submission-memo.zh-CN.md).

## 04.2 Overdraw and fill-rate baseline

The second scene moves from CPU submission structure to GPU pixel pressure. It
builds a stack of overlapping quads through two matched modes:

1. **Opaque depth write** uses `Blend One Zero`, `ZWrite On`, and the Geometry
   queue. Opaque front-to-back sorting and depth testing can reject covered
   fragments before the full fragment workload runs.
2. **Transparent alpha blend** uses `Blend SrcAlpha OneMinusSrcAlpha`,
   `ZWrite Off`, and the Transparent queue. Every visible overlapping layer
   must be shaded and blended to preserve the result.

Both modes create the same number of quads, Renderer components, material
instances, and potential draw submissions. They use the same vertex/fragment
shader body; only render state, queue, and alpha differ. This keeps the sample
focused on depth rejection and blending instead of presenting fewer draw calls
as a fill-rate optimization.

The controls expose 8, 32, 64, and 128 layers plus estimated 25%, 50%, and 100%
screen coverage per layer. The quad's width and height are each multiplied by
the square root of the requested ratio, so its estimated pixel area follows the
selected percentage. A matched fragment-work control runs 1, 8, or 24 loop
iterations in both modes. It amplifies the cost on desktop GPUs without giving
the transparent path a different shader workload. The overlay reports:

- deterministic Renderer and material counts;
- frame-wide Draw Calls, Batches, and SetPass calls;
- `equivalent fullscreen layers = layer count x coverage ratio`;
- `relative fragment-work pressure = fullscreen layers x iterations`.

Equivalent fullscreen layers and relative fragment-work pressure are workload
descriptors, not hardware timings or counts of fragments that actually ran.
Early-Z behavior, tile-based GPU architecture, resolution, MSAA, render scale,
Editor overhead, and the current URP configuration all affect the measured
result.

Open
`Project/Assets/Samples/RenderingPerformance/OverdrawFillRate/OverdrawFillRateDemo.unity`
and compare modes at the same layer count and coverage. For meaningful timing,
use a Development Build on the target device, disable VSync or other frame-rate
caps, allow the workload to warm up, and compare GPU timings over many frames.
Use Frame Debugger to confirm queue, blend, and depth-write state; use a GPU
capture tool when you need proof of fragment cost or tile behavior.

On a desktop GPU the default workload can be too small to move frame time. Try
64 or 128 layers, 100% coverage, and 24 fragment iterations. Draw Calls should
remain comparable while the transparent mode shades and blends every layer.
If FPS still stays capped, inspect GPU frame time rather than using FPS as the
measurement.

Run `OverdrawWorkloadPlayModeTests` to verify matched geometry ownership,
coverage sizing, material render states, invalid-input handling, and cleanup.
The tests intentionally make no FPS or GPU-time assertion because those values
are device- and pipeline-dependent.

For a Chinese-language review of rasterization, Fragment Shader execution,
Early-Z, transparent overdraw, and the correct Profiler modules, see
[`04-过度绘制与填充率技术回顾.md`](04-过度绘制与填充率技术回顾.md).
