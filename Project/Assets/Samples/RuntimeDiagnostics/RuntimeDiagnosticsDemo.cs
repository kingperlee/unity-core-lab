using System;
using System.Threading;
using Unity.Profiling;
using UnityEngine;
using UnityCoreLab.Diagnostics;

namespace UnityCoreLab.Samples.RuntimeDiagnostics
{
    /// <summary>
    /// Interactive 05.1 sample for bounded diagnostic recording, slow-frame
    /// detection, report suppression, and deliberately safe fault injection.
    /// </summary>
    public sealed class RuntimeDiagnosticsDemo : MonoBehaviour
    {
        public const int EventCapacity = 32;
        public const double WarningThresholdMilliseconds = 33.3d;
        public const double CriticalThresholdMilliseconds = 100d;
        public const double ReportCooldownSeconds = 0.5d;
        private const int StartupWarmupFrames = 30;

        private static readonly ProfilerMarker InjectHitchMarker =
            new ProfilerMarker("UnityCoreLab.Diagnostics.InjectMainThreadHitch");

        private RuntimeDiagnosticBuffer _events;
        private FrameHitchDetector _hitchDetector;
        private int _warmupFramesRemaining;
        private GUIStyle _titleStyle;
        private GUIStyle _labelStyle;
        private GUIStyle _buttonStyle;
        private Vector2 _scrollPosition;

        public bool IsInitialized => _events != null && _hitchDetector != null;

        public RuntimeDiagnosticBuffer Events => _events;

        public FrameHitchDetector HitchDetector => _hitchDetector;

        private void Awake()
        {
            _events = new RuntimeDiagnosticBuffer(EventCapacity);
            _hitchDetector = new FrameHitchDetector(
                _events,
                WarningThresholdMilliseconds,
                CriticalThresholdMilliseconds,
                ReportCooldownSeconds);
            _warmupFramesRemaining = StartupWarmupFrames;
        }

        private void Update()
        {
            if (_warmupFramesRemaining > 0)
            {
                _warmupFramesRemaining--;
                return;
            }

            ObserveFrameForTesting(
                Time.unscaledDeltaTime * 1000d,
                Time.realtimeSinceStartupAsDouble);
        }

        public bool ObserveFrameForTesting(
            double frameDurationMilliseconds,
            double timestampSeconds)
        {
            return _hitchDetector.ObserveFrame(
                frameDurationMilliseconds,
                timestampSeconds);
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

        public void ClearDiagnostics()
        {
            _events.Clear();
            _hitchDetector.ResetStatistics();
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
            GUILayout.Label("05.1 Runtime diagnostics baseline", _labelStyle);
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

            if (GUILayout.Button("Clear diagnostics", _buttonStyle))
            {
                ClearDiagnostics();
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
                "fixed-capacity buffer prevents diagnostic history from " +
                "growing without limit.",
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
    }
}
