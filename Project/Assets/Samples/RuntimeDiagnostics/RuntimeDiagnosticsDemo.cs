using System;
using System.Globalization;
using System.IO;
using System.Text;
using System.Threading;
using Unity.Profiling;
using UnityEngine;
using UnityCoreLab.Diagnostics;

namespace UnityCoreLab.Samples.RuntimeDiagnostics
{
    /// <summary>
    /// Interactive diagnostics sample for bounded event capture, safe fault
    /// injection, and explicitly requested JSON report snapshots.
    /// </summary>
    public sealed class RuntimeDiagnosticsDemo : MonoBehaviour
    {
        public const int EventCapacity = 32;
        public const double WarningThresholdMilliseconds = 33.3d;
        public const double CriticalThresholdMilliseconds = 100d;
        public const double ReportCooldownSeconds = 0.5d;
        public const long WarningAllocationBytes = 256L * 1024L;
        public const long CriticalAllocationBytes = 1024L * 1024L;
        public const double MemoryReportCooldownSeconds = 1d;
        private const int StartupWarmupFrames = 30;
        private const int ReportPreviewCharacterLimit = 1800;
        private const int MaximumInjectedAllocationKilobytes = 4096;

        private static readonly ProfilerMarker InjectHitchMarker =
            new ProfilerMarker("UnityCoreLab.Diagnostics.InjectMainThreadHitch");

        private RuntimeDiagnosticBuffer _events;
        private FrameHitchDetector _hitchDetector;
        private ManagedMemoryMonitor _memoryMonitor;
        private ProfilerRecorder _gcAllocatedInFrameRecorder;
        private ProfilerRecorder _gcUsedMemoryRecorder;
        private ProfilerRecorder _gcReservedMemoryRecorder;
        private ProfilerRecorder _systemUsedMemoryRecorder;
        private int _warmupFramesRemaining;
        private GUIStyle _titleStyle;
        private GUIStyle _labelStyle;
        private GUIStyle _buttonStyle;
        private Vector2 _scrollPosition;
        private string _sessionId;
        private string _latestReportJson;
        private string _latestReportPreview;
        private string _latestReportPath;
        private string _reportStatus;
        private byte[] _managedAllocationProbe;
        private int _releaseManagedProbeFrame;

        public bool IsInitialized =>
            _events != null &&
            _hitchDetector != null &&
            _memoryMonitor != null;

        public RuntimeDiagnosticBuffer Events => _events;

        public FrameHitchDetector HitchDetector => _hitchDetector;

        public ManagedMemoryMonitor MemoryMonitor => _memoryMonitor;

        public string LatestReportJson => _latestReportJson;

        private void Awake()
        {
            _events = new RuntimeDiagnosticBuffer(EventCapacity);
            _hitchDetector = new FrameHitchDetector(
                _events,
                WarningThresholdMilliseconds,
                CriticalThresholdMilliseconds,
                ReportCooldownSeconds);
            _memoryMonitor = new ManagedMemoryMonitor(
                _events,
                WarningAllocationBytes,
                CriticalAllocationBytes,
                MemoryReportCooldownSeconds);
            _sessionId = Guid.NewGuid().ToString("N");
            _warmupFramesRemaining = StartupWarmupFrames;
            StartMemoryCounters();
        }

        private void Update()
        {
            if (_managedAllocationProbe != null &&
                Time.frameCount >= _releaseManagedProbeFrame)
            {
                _managedAllocationProbe = null;
            }

            if (_warmupFramesRemaining > 0)
            {
                _warmupFramesRemaining--;
                return;
            }

            double timestampSeconds = Time.realtimeSinceStartupAsDouble;
            ObserveFrameForTesting(
                Time.unscaledDeltaTime * 1000d,
                timestampSeconds);
            ObserveMemoryForTesting(
                ReadManagedMemorySample(),
                timestampSeconds);
        }

        private void OnDestroy()
        {
            _gcAllocatedInFrameRecorder.Dispose();
            _gcUsedMemoryRecorder.Dispose();
            _gcReservedMemoryRecorder.Dispose();
            _systemUsedMemoryRecorder.Dispose();
            _managedAllocationProbe = null;
        }

        public bool ObserveFrameForTesting(
            double frameDurationMilliseconds,
            double timestampSeconds)
        {
            return _hitchDetector.ObserveFrame(
                frameDurationMilliseconds,
                timestampSeconds);
        }

        public bool ObserveMemoryForTesting(
            ManagedMemorySample sample,
            double timestampSeconds)
        {
            return _memoryMonitor.Observe(sample, timestampSeconds);
        }

        public void InjectMainThreadStall(int milliseconds)
        {
            if (milliseconds <= 0 || milliseconds > 250)
            {
                throw new ArgumentOutOfRangeException(nameof(milliseconds));
            }

            _events.Add(
                Time.realtimeSinceStartupAsDouble,
                "FaultInjection",
                DiagnosticSeverity.Info,
                "Requested a bounded main-thread stall.",
                milliseconds);

            using (InjectHitchMarker.Auto())
            {
                Thread.Sleep(milliseconds);
            }
        }

        public void InjectHandledException()
        {
            try
            {
                throw new InvalidOperationException(
                    "Intentional handled exception from the diagnostics lab.");
            }
            catch (Exception exception)
            {
                _events.Add(
                    Time.realtimeSinceStartupAsDouble,
                    "Exception",
                    DiagnosticSeverity.Error,
                    $"Handled {exception.GetType().Name}: {exception.Message}");
            }
        }

        public void InjectManagedAllocation(int kilobytes)
        {
            if (kilobytes <= 0 ||
                kilobytes > MaximumInjectedAllocationKilobytes)
            {
                throw new ArgumentOutOfRangeException(nameof(kilobytes));
            }

            _managedAllocationProbe = new byte[kilobytes * 1024];
            _managedAllocationProbe[0] = 1;
            _managedAllocationProbe[_managedAllocationProbe.Length - 1] = 1;
            _releaseManagedProbeFrame = Time.frameCount + 2;
            _events.Add(
                Time.realtimeSinceStartupAsDouble,
                "FaultInjection",
                DiagnosticSeverity.Info,
                $"Allocated a bounded {kilobytes} KB managed probe.");
        }

        public void ClearDiagnostics()
        {
            _events.Clear();
            _hitchDetector.ResetStatistics();
            _memoryMonitor.ResetStatistics();
            _latestReportJson = null;
            _latestReportPreview = null;
            _latestReportPath = null;
            _reportStatus = null;
        }

        public RuntimeDiagnosticReport CaptureReport()
        {
            return RuntimeDiagnosticReport.Capture(
                _events,
                _hitchDetector,
                CreateRuntimeContext(),
                DateTime.UtcNow,
                Time.realtimeSinceStartupAsDouble,
                _memoryMonitor);
        }

        public string BuildReportJson()
        {
            _latestReportJson = RuntimeDiagnosticJsonSerializer.Serialize(
                CaptureReport(),
                indented: true);
            int previewLength = Math.Min(
                _latestReportJson.Length,
                ReportPreviewCharacterLimit);
            _latestReportPreview = _latestReportJson.Substring(0, previewLength);
            if (previewLength < _latestReportJson.Length)
            {
                _latestReportPreview += "\n... preview truncated ...";
            }

            _reportStatus =
                $"Built {_latestReportJson.Length:N0}-character JSON snapshot.";
            return _latestReportJson;
        }

        public bool TryExportReport(out string path, out string error)
        {
            path = null;
            error = null;
            string json = BuildReportJson();
            try
            {
                string directory = Path.Combine(
                    Application.persistentDataPath,
                    "Diagnostics");
                Directory.CreateDirectory(directory);
                string fileName =
                    "runtime-diagnostics-" +
                    DateTime.UtcNow.ToString(
                        "yyyyMMdd-HHmmss-fff",
                        CultureInfo.InvariantCulture) +
                    ".json";
                path = Path.Combine(directory, fileName);
                File.WriteAllText(path, json, new UTF8Encoding(false));
                _latestReportPath = path;
                _reportStatus = "Exported report successfully.";
                return true;
            }
            catch (Exception exception) when (
                exception is IOException ||
                exception is UnauthorizedAccessException ||
                exception is NotSupportedException ||
                exception is System.Security.SecurityException)
            {
                error = $"{exception.GetType().Name}: {exception.Message}";
                _reportStatus = $"Export failed: {error}";
                _events.Add(
                    Time.realtimeSinceStartupAsDouble,
                    "ReportExport",
                    DiagnosticSeverity.Error,
                    _reportStatus);
                return false;
            }
        }

        private void OnGUI()
        {
            EnsureGuiStyles();
            GUI.depth = -100;

            float scale = Mathf.Clamp(Screen.height / 1080f, 0.85f, 1.15f);
            Matrix4x4 previousMatrix = GUI.matrix;
            GUI.matrix = Matrix4x4.Scale(new Vector3(scale, scale, 1f));

            float logicalHeight = Screen.height / scale;
            GUILayout.BeginArea(
                new Rect(16f, 16f, 600f, Mathf.Max(700f, logicalHeight - 32f)),
                GUI.skin.window);
            _scrollPosition = GUILayout.BeginScrollView(_scrollPosition);

            GUILayout.Label("Unity Core Lab", _titleStyle);
            GUILayout.Label(
                "05.3 Managed-memory counters and allocation spikes",
                _labelStyle);
            GUILayout.Space(8f);
            GUILayout.Label(
                $"Hitch thresholds: warning " +
                $"{WarningThresholdMilliseconds:F1} ms, critical " +
                $"{CriticalThresholdMilliseconds:F0} ms",
                _labelStyle);
            GUILayout.Label(
                $"Detected hitches: {_hitchDetector.DetectedHitchCount}",
                _labelStyle);
            GUILayout.Label(
                $"Suppressed duplicate reports: " +
                $"{_hitchDetector.SuppressedReportCount}",
                _labelStyle);
            GUILayout.Label(
                $"Worst observed frame: " +
                $"{_hitchDetector.WorstFrameMilliseconds:F2} ms",
                _labelStyle);
            GUILayout.Label(
                $"Buffered events: {_events.Count}/{_events.Capacity}, " +
                $"overwritten: {_events.OverwrittenCount}",
                _labelStyle);

            GUILayout.Space(8f);
            GUILayout.Label(
                "ProfilerRecorder counters (last completed frame)",
                _labelStyle);
            GUILayout.Label(
                "GC allocated in frame: " +
                FormatCounterBytes(_gcAllocatedInFrameRecorder),
                _labelStyle);
            GUILayout.Label(
                "GC heap used / reserved: " +
                $"{FormatCounterBytes(_gcUsedMemoryRecorder)} / " +
                FormatCounterBytes(_gcReservedMemoryRecorder),
                _labelStyle);
            GUILayout.Label(
                "System used memory: " +
                FormatCounterBytes(_systemUsedMemoryRecorder),
                _labelStyle);
            string maximumAllocation = FormatBytes(
                _memoryMonitor.MaxGcAllocatedInFrameBytes,
                _memoryMonitor.HasGcAllocationData);
            GUILayout.Label(
                $"Allocation spikes: {_memoryMonitor.AllocationSpikeCount}, " +
                $"max: {maximumAllocation}",
                _labelStyle);

            GUILayout.Space(12f);
            if (GUILayout.Button(
                    "Inject 80 ms main-thread stall",
                    _buttonStyle))
            {
                InjectMainThreadStall(80);
            }

            if (GUILayout.Button(
                    "Inject 150 ms main-thread stall",
                    _buttonStyle))
            {
                InjectMainThreadStall(150);
            }

            if (GUILayout.Button(
                    "Inject handled exception",
                    _buttonStyle))
            {
                InjectHandledException();
            }

            if (GUILayout.Button(
                    "Allocate 512 KB managed probe",
                    _buttonStyle))
            {
                InjectManagedAllocation(512);
            }

            if (GUILayout.Button(
                    "Allocate 2 MB managed probe",
                    _buttonStyle))
            {
                InjectManagedAllocation(2048);
            }

            if (GUILayout.Button("Clear diagnostics", _buttonStyle))
            {
                ClearDiagnostics();
            }

            GUILayout.Space(12f);
            if (GUILayout.Button("Build JSON report preview", _buttonStyle))
            {
                BuildReportJson();
            }

            if (GUILayout.Button(
                    "Export JSON to persistentDataPath",
                    _buttonStyle))
            {
                TryExportReport(out _, out _);
            }

            if (!string.IsNullOrEmpty(_reportStatus))
            {
                GUILayout.Label(_reportStatus, _labelStyle);
            }

            if (!string.IsNullOrEmpty(_latestReportPath))
            {
                GUILayout.Label(
                    $"Latest path: {_latestReportPath}",
                    _labelStyle);
            }

            if (!string.IsNullOrEmpty(_latestReportPreview))
            {
                GUILayout.Label(_latestReportPreview, GUI.skin.textArea);
            }

            GUILayout.Space(12f);
            GUILayout.Label("Recent events (newest first)", _labelStyle);
            for (int index = _events.Count - 1; index >= 0; index--)
            {
                RuntimeDiagnosticEvent diagnosticEvent = _events[index];
                string duration = diagnosticEvent.DurationMilliseconds > 0d
                    ? $" {diagnosticEvent.DurationMilliseconds:F2} ms"
                    : string.Empty;
                string occurrences = diagnosticEvent.OccurrenceCount > 1
                    ? $" x{diagnosticEvent.OccurrenceCount}"
                    : string.Empty;
                GUILayout.Label(
                    $"#{diagnosticEvent.Sequence} " +
                    $"[{diagnosticEvent.Severity}] " +
                    $"{diagnosticEvent.Category}:{duration}{occurrences}\n" +
                    diagnosticEvent.Message,
                    _labelStyle);
                GUILayout.Space(5f);
            }

            GUILayout.Space(12f);
            GUILayout.Label(
                "Fault injection is intentionally bounded: it demonstrates " +
                "hitch detection and handled exception capture without " +
                "triggering an OOM, process crash, or external upload. The " +
                "per-frame path remains bounded. JSON allocation and disk " +
                "I/O happen only when a report is explicitly requested. " +
                "Memory probes are capped at 4 MB and are released after " +
                "measurement; the sample never forces a garbage collection.",
                _labelStyle);

            GUILayout.EndScrollView();
            GUILayout.EndArea();
            GUI.matrix = previousMatrix;
        }

        private void EnsureGuiStyles()
        {
            if (_titleStyle != null)
            {
                return;
            }

            _titleStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 28,
                fontStyle = FontStyle.Bold,
                wordWrap = true
            };
            _labelStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 17,
                wordWrap = true
            };
            _buttonStyle = new GUIStyle(GUI.skin.button)
            {
                fontSize = 16,
                wordWrap = true,
                fixedHeight = 44f
            };
        }

        private RuntimeDiagnosticContext CreateRuntimeContext()
        {
            return new RuntimeDiagnosticContext(
                _sessionId,
                Application.productName,
                Application.version,
                Application.platform.ToString(),
                SystemInfo.deviceModel,
                SystemInfo.operatingSystem,
                SystemInfo.graphicsDeviceName);
        }

        private void StartMemoryCounters()
        {
            _gcAllocatedInFrameRecorder = ProfilerRecorder.StartNew(
                ProfilerCategory.Memory,
                "GC Allocated In Frame",
                1);
            _gcUsedMemoryRecorder = ProfilerRecorder.StartNew(
                ProfilerCategory.Memory,
                "GC Used Memory",
                1);
            _gcReservedMemoryRecorder = ProfilerRecorder.StartNew(
                ProfilerCategory.Memory,
                "GC Reserved Memory",
                1);
            _systemUsedMemoryRecorder = ProfilerRecorder.StartNew(
                ProfilerCategory.Memory,
                "System Used Memory",
                1);
        }

        private ManagedMemorySample ReadManagedMemorySample()
        {
            return new ManagedMemorySample(
                ReadCounter(_gcAllocatedInFrameRecorder),
                ReadCounter(_gcUsedMemoryRecorder),
                ReadCounter(_gcReservedMemoryRecorder),
                ReadCounter(_systemUsedMemoryRecorder));
        }

        private static long ReadCounter(ProfilerRecorder recorder)
        {
            return recorder.Valid
                ? Math.Max(0L, recorder.LastValue)
                : ManagedMemorySample.Unavailable;
        }

        private static string FormatCounterBytes(ProfilerRecorder recorder)
        {
            return FormatBytes(ReadCounter(recorder), recorder.Valid);
        }

        private static string FormatBytes(long bytes, bool available)
        {
            if (!available)
            {
                return "Unavailable";
            }

            const double Kilobyte = 1024d;
            const double Megabyte = Kilobyte * 1024d;
            if (bytes >= Megabyte)
            {
                return $"{bytes / Megabyte:F2} MB";
            }

            return $"{bytes / Kilobyte:F1} KB";
        }
    }
}
