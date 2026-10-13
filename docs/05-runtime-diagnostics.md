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

Future stages can add allocation counters, environment snapshots, log capture,
report serialization, upload retry policy, and platform crash/ANR adapters
without changing the bounded event-history contract.
