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

## 04.3 Distance LOD and explicit visibility policy

The third scene studies work that can be reduced before expensive geometry and
fragment processing. It creates a fixed field of shared-material objects and
switches among three deterministic policies:

1. **All high detail** keeps the built-in sphere mesh on every Renderer.
2. **Distance LOD** switches objects beyond a configurable distance to the
   built-in cube mesh while keeping every Renderer enabled.
3. **Distance LOD + explicit culling** also disables objects beyond a maximum
   distance or outside the camera frustum.

The sample exposes high-detail, low-detail, and policy-culled counts plus an
estimated visible triangle count. The estimate is calculated from the policy's
selected meshes. The frame-wide Profiler triangle counter can be lower because
Unity can internally frustum-cull an enabled Renderer without changing its
`Renderer.enabled` property.

This distinction is intentional: normal Renderer frustum culling already
prevents off-camera geometry from being submitted for rendering. An explicit
visibility policy is still useful when distance should disable animation,
particles, gameplay updates, streaming, or entire object hierarchies. Manual
frustum tests should not be presented as automatically faster than Unity's
built-in Renderer culling.

The per-frame evaluation:

- compares squared distances to avoid square roots;
- reuses a six-plane buffer when calculating the camera frustum;
- changes `sharedMesh` and `Renderer.enabled` only when state changes;
- uses one shared material for all objects;
- wraps policy evaluation in the
  `UnityCoreLab.DetailCulling.Evaluate` Profiler marker.

The scene also contains a deliberately synthetic per-object CPU workload. It
can update every object or only objects left active by the explicit policy, at
8, 32, or 96 iterations per object. The work is isolated under the
`UnityCoreLab.DetailCulling.SimulatedUpdate` Profiler marker. Its purpose is to
demonstrate a boundary condition: culling has its own CPU cost and only creates
a CPU-side net saving when it prevents more expensive downstream work. The
synthetic loop must not be presented as a production gameplay benchmark.

Object-count presets of 225, 1024, and 4096 make the steady-state CPU trend
visible on desktop hardware. Rebuilding the workload intentionally allocates
and creates GameObjects, so discard the rebuild frame and compare stable frames
afterward. A useful controlled comparison is:

1. select 4096 objects and 96 update iterations;
2. select `Distance LOD + explicit distance/frustum policy`;
3. compare `Update logic on all objects` against
   `Update logic on policy-active objects only`;
4. inspect both custom markers in CPU Profiler Hierarchy or Timeline view.

LOD-only mode should primarily reduce the triangle count. It does not remove
Renderer components or per-object simulated updates, so it should not be
claimed as a general CPU optimization.

Open
`Project/Assets/Samples/RenderingPerformance/LodCulling/DetailCullingDemo.unity`
and compare modes with the same camera preset and threshold pair. Observe both
the deterministic snapshot and the Rendering Profiler counters. Moving the
camera changes which objects fall into the high-detail, low-detail, and culled
bands.

This baseline uses an explicit distance policy so its state is testable and its
triangle estimate is deterministic. Unity `LODGroup`, `CullingGroup`, LOD
cross-fade, hysteresis, and production asset-authoring workflows are separate
follow-up topics rather than hidden inside this first comparison.

Run `DetailCullingWorkloadPlayModeTests` to verify detail selection, distance
and frustum policy, triangle estimates, renderer state, simulated-update
gating, invalid thresholds, and runtime cleanup. Timing and fixed draw-count
thresholds are deliberately excluded from tests because they depend on camera,
pipeline, and platform.

## 04.4 Controlled CPU/GPU frame-timing capture

The LOD/culling scene includes a small measurement harness around Unity's
`FrameTimingManager`. Select one workload configuration and press
`Warm up 60 + capture 120 frames`. The capture records its configuration label,
discards a fixed warm-up window, then reports median and P95 values for:

- total CPU frame time;
- CPU main-thread frame time;
- CPU render-thread frame time;
- GPU frame time, when the current platform supplies it.

Changing the mode, thresholds, object count, simulated-update scope, or camera
preset cancels an in-progress capture. This prevents a result from silently
mixing samples from different workloads. `Frame Timing Stats` is enabled in the
project settings, but GPU timing can still be unavailable in the Editor or on a
particular graphics API. In that case the UI says `unavailable`; it does not
substitute FPS or CPU time and mislabel it as GPU time.

For a fair comparison, keep resolution, camera, quality settings, VSync,
target frame rate, object count, and update workload identical. Prefer a
Development Build running on the target device, repeat each mode several times,
and compare distributions rather than one frame or the Game view FPS display.
Median represents the typical sampled frame. P95 exposes slower tail frames,
so the pair is more informative than a single average.

The capture is deliberately an observation tool rather than a performance test:
automated tests verify the deterministic percentile calculation, but do not
assert that one rendering mode must be faster. GPU and CPU timings vary with
hardware, driver, thermal state, graphics API, pipeline configuration, and
background activity.

The 4096-object experiment illustrates the interpretation rule. If
`AllHighDetail` and `DistanceLod` report similar GPU time even though LOD lowers
triangle count, the workload is probably not vertex-bound at that detail ratio.
`DistanceLodAndExplicitCulling` can still improve GPU time because disabling
Renderers removes complete draw submissions and their vertex, raster, fragment,
and render-target work. Reducing a counter that is not the active bottleneck is
not expected to reduce total frame time.

For a concise Chinese review of the four timing metrics, their frame boundaries,
pipeline overlap, and optimization implications, see
[`04-CPU与GPU帧时序指标技术回顾.md`](04-CPU与GPU帧时序指标技术回顾.md).
