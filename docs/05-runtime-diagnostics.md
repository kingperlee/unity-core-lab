# Runtime Diagnostics and Fault Injection

## 05.1 Bounded hitch diagnostics baseline

The first runtime-diagnostics sample separates three responsibilities:

1. `FrameHitchDetector` classifies externally supplied frame durations;
2. `RuntimeDiagnosticBuffer` retains a bounded chronological history;
3. `RuntimeDiagnosticsDemo` adapts Unity time and safe fault-injection controls
   to those engine-independent components.

The buffer allocates its backing array once. When capacity is reached, the
oldest event is overwritten and `OverwrittenCount` increases. Sequence numbers
remain monotonic after clearing the visible history, which makes event ordering
unambiguous when a report is copied or correlated with other logs.

The hitch detector uses a warning threshold of `33.3 ms`, a critical threshold
of `100 ms`, and a `0.5 s` report cooldown in the sample. Every slow frame still
contributes to `DetectedHitchCount` and the worst-frame statistic. Repeated
events inside the cooldown are coalesced rather than filling the diagnostic
buffer; the next emitted event carries an `OccurrenceCount` that includes the
suppressed observations. A transition from warning to critical bypasses the
cooldown so a severity escalation cannot disappear from the event history.

Open
`Project/Assets/Samples/RuntimeDiagnostics/RuntimeDiagnosticsDemo.unity` and
enter Play Mode. After a 30-frame startup warm-up, use the controls to inject:

- an `80 ms` main-thread stall, expected to produce a warning hitch;
- a `150 ms` main-thread stall, expected to produce a critical hitch;
- an intentionally thrown and handled exception, recorded as an error event.

The injected stalls are wrapped in the
`UnityCoreLab.Diagnostics.InjectMainThreadHitch` Profiler marker. They use a
strict `250 ms` upper bound. This baseline deliberately does not cause an OOM,
terminate the process, upload a report, or pretend that a handled exception is
a native crash.

The sample demonstrates several production-oriented constraints:

- diagnostic storage must itself have a memory budget;
- repeated symptoms need suppression or aggregation;
- a monotonic clock is required for time-window policies;
- startup frames should not be classified as steady-state hitches;
- fault injection must be explicit, bounded, and safe by default;
- event capture and report upload are different responsibilities.

Run `RuntimeDiagnosticsTests` in EditMode to verify bounded overwrite order,
monotonic event IDs, input validation, hitch classification, and cooldown
aggregation. Run `RuntimeDiagnosticsDemoPlayModeTests` to verify the Unity
adapter and handled-exception path. Tests inject measured values directly and
do not make scheduler-dependent assertions about `Thread.Sleep` duration.

Future stages can add allocation counters, log capture, upload retry policy,
and platform crash/ANR adapters without changing the bounded event-history
contract.

## 05.2 Point-in-time JSON report

The second sample turns the bounded in-memory state into evidence that can be
attached to a bug report. Report generation is explicit rather than part of the
per-frame path:

1. `RuntimeDiagnosticReport.Capture` copies the current chronological events;
2. the snapshot records hitch statistics, overwrite count, session identity,
   application version, platform, device, operating system, and GPU;
3. `RuntimeDiagnosticJsonSerializer` writes deterministic JSON with invariant
   numbers and correct string escaping;
4. the demo can preview the JSON or save it below
   `Application.persistentDataPath/Diagnostics`.

A captured report does not change if the live ring buffer is subsequently
overwritten or cleared. The report includes `schemaVersion` so a future server
or offline analysis tool can distinguish incompatible formats.

The serializer is deliberately engine-independent and reflection-free. Its
property order is stable, but consumers must identify fields by name rather
than depend on their order. JSON construction and disk I/O allocate memory, so
they occur only after an explicit button press. They must not be moved into
`Update` or another per-frame callback.

This stage still performs no upload. Separating capture, serialization, local
persistence, and transport keeps retry policy and privacy decisions out of the
low-overhead event detector.

## 05.3 Managed-memory counters and allocation spikes

The third stage samples four Unity `ProfilerRecorder` counters with capacity
one:

- `GC Allocated In Frame` for managed bytes allocated during a frame;
- `GC Used Memory` and `GC Reserved Memory` for managed-heap usage;
- `System Used Memory` for the process memory reported by the operating system.

`RuntimeDiagnosticsDemo` is the Unity adapter. It converts a valid recorder
value into `ManagedMemorySample` and uses `-1` only at that boundary to express
an unavailable counter. The engine-independent `ManagedMemoryMonitor` then
tracks availability flags, sample counts, peak values, allocation spikes, and
cooldown suppression without allocating on an ordinary sample.

The sample reports a warning at `256 KB` allocated in one frame and a critical
event at `1 MB`. Repeated events have a one-second cooldown, while a transition
from warning to critical bypasses the cooldown. The two injection buttons make
bounded `512 KB` and `2 MB` allocations and retain them briefly so the counter
can observe them. The demo never forces a collection because `GC.Collect`
would introduce a different stall and distort the experiment.

Counter availability is part of the result. `GC Allocated In Frame` is not
available in every Release Player configuration, so an unavailable value is
serialized as JSON `null`, not zero. Used, reserved, and system memory are
reported independently because they answer different questions. A rising
`GC Used Memory` value alone does not prove a leak; warm-up, caches, and pools
can retain memory intentionally. Confirm suspected leaks by comparing Memory
Profiler snapshots on the target device.

Editor values include Editor and profiling overhead. Use this scene to learn
the signal and validate the event/report pipeline, then use a development
build on the target platform for performance conclusions.

References:

- Unity 2022.3 Memory Profiler module:
  https://docs.unity3d.com/2022.3/Documentation/Manual/ProfilerMemory.html
- Unity 2022.3 `ProfilerRecorder` API:
  https://docs.unity3d.com/2022.3/Documentation/ScriptReference/Unity.Profiling.ProfilerRecorder.html
