using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using UnityEngine;
using UnityCoreLab.AsyncOperations;

namespace UnityCoreLab.Samples.AsyncOperations
{
    /// <summary>
    /// Minimal IMGUI sample. Add this component to an empty GameObject in a scene.
    /// </summary>
    public sealed class AsyncOperationDemo : MonoBehaviour
    {
        private readonly List<string> _logs = new List<string>();
        private AsyncOperationCoordinator _coordinator;
        private Renderer _stateRenderer;
        private TextMesh _worldStatus;
        private Material _stateMaterial;
        private GUIStyle _titleStyle;
        private GUIStyle _labelStyle;
        private GUIStyle _buttonStyle;

        public bool IsInitialized =>
            _coordinator != null && _stateRenderer != null && _worldStatus != null;

        public OperationSnapshot Current => _coordinator != null
            ? _coordinator.Current
            : new OperationSnapshot(0, OperationState.Idle, TimeSpan.Zero);

        private void Awake()
        {
            _coordinator = new AsyncOperationCoordinator();
            _coordinator.StateChanged += OnStateChanged;
            CreateWorldVisualization();
            ApplyVisualState(_coordinator.Current);
            Debug.Log("[UnityCoreLab] Async Operation Lifecycle demo initialized.");
        }

        private void Update()
        {
            if (_stateRenderer == null)
            {
                return;
            }

            float pulse = _coordinator.Current.State == OperationState.Running
                ? 1f + Mathf.Sin(Time.time * 6f) * 0.12f
                : 1f;
            _stateRenderer.transform.localScale = Vector3.one * (2f * pulse);
        }

        private void OnDestroy()
        {
            if (_coordinator == null)
            {
                return;
            }

            _coordinator.StateChanged -= OnStateChanged;
            _coordinator.Dispose();

            if (_stateMaterial != null)
            {
                Destroy(_stateMaterial);
            }
        }

        public Task<OperationResult> RunOperationAsync(
            IAsyncOperation operation,
            TimeSpan timeout)
        {
            if (_coordinator == null)
            {
                throw new InvalidOperationException("The demo has not been initialized.");
            }

            return _coordinator.RunAsync(operation, timeout);
        }

        private void OnGUI()
        {
            EnsureGuiStyles();
            GUI.depth = -100;

            const float referencePixelCount = 1080f * 1920f;
            float currentPixelCount = Mathf.Max(1f, Screen.width * Screen.height);
            float scale = Mathf.Clamp(
                Mathf.Sqrt(currentPixelCount / referencePixelCount),
                0.85f,
                1.35f);
            Matrix4x4 previousMatrix = GUI.matrix;
            GUI.matrix = Matrix4x4.Scale(new Vector3(scale, scale, 1f));

            float logicalWidth = Screen.width / scale;
            float logicalHeight = Screen.height / scale;
            float panelWidth = Mathf.Max(360f, Mathf.Min(520f, logicalWidth - 32f));
            float panelHeight = Mathf.Clamp(logicalHeight - 32f, 520f, 900f);
            GUILayout.BeginArea(
                new Rect(16f, 16f, panelWidth, panelHeight),
                GUI.skin.window);
            GUILayout.Label("Unity Core Lab", _titleStyle);
            GUILayout.Label("Async Operation Lifecycle", _labelStyle);
            GUILayout.Space(8);
            GUILayout.Label($"Version: {_coordinator.Current.Version}", _labelStyle);
            GUILayout.Label($"State: {_coordinator.Current.State}", _labelStyle);
            GUILayout.Label(
                $"Elapsed: {_coordinator.Current.Elapsed.TotalMilliseconds:F0} ms",
                _labelStyle);

            if (!string.IsNullOrEmpty(_coordinator.Current.Error))
            {
                GUILayout.Label($"Error: {_coordinator.Current.Error}", _labelStyle);
            }

            GUILayout.Space(12);

            if (GUILayout.Button("Run normal operation (1 second)", _buttonStyle))
            {
                _ = RunOperationAsync(
                    new DelayOperation(TimeSpan.FromSeconds(1)),
                    TimeSpan.FromSeconds(3));
            }

            if (GUILayout.Button("Run slow operation with 1 second timeout", _buttonStyle))
            {
                _ = RunOperationAsync(
                    new DelayOperation(TimeSpan.FromSeconds(5)),
                    TimeSpan.FromSeconds(1));
            }

            if (GUILayout.Button(
                    "Start a new operation and supersede the previous one",
                    _buttonStyle))
            {
                _ = RunOperationAsync(
                    new DelayOperation(TimeSpan.FromSeconds(5)),
                    TimeSpan.FromSeconds(10));
                _ = RunOperationAsync(
                    new DelayOperation(TimeSpan.FromMilliseconds(300)),
                    TimeSpan.FromSeconds(2));
            }

            if (GUILayout.Button("Simulate exception", _buttonStyle))
            {
                _ = RunOperationAsync(new FaultOperation(), TimeSpan.FromSeconds(2));
            }

            if (GUILayout.Button("Cancel active operation", _buttonStyle))
            {
                _coordinator.CancelActive();
            }

            GUILayout.Space(16);
            GUILayout.Label("Recent state changes", _labelStyle);
            foreach (string message in _logs)
            {
                GUILayout.Label(message, _labelStyle);
            }

            GUILayout.EndArea();
            GUI.matrix = previousMatrix;
        }

        private void OnStateChanged(OperationSnapshot snapshot)
        {
            _logs.Insert(
                0,
                $"v{snapshot.Version} | {snapshot.State} | {snapshot.Elapsed.TotalMilliseconds:F0} ms");

            if (_logs.Count > 12)
            {
                _logs.RemoveAt(_logs.Count - 1);
            }

            ApplyVisualState(snapshot);
        }

        private void CreateWorldVisualization()
        {
            Camera camera = Camera.main;
            if (camera != null)
            {
                camera.transform.SetPositionAndRotation(
                    new Vector3(0f, 1.5f, -10f),
                    Quaternion.identity);
                camera.backgroundColor = new Color(0.025f, 0.035f, 0.055f);
            }

            var stateObject = GameObject.CreatePrimitive(PrimitiveType.Cube);
            stateObject.name = "Operation State Indicator";
            stateObject.transform.SetParent(transform, false);
            stateObject.transform.position = new Vector3(2.2f, 0.5f, 0f);
            _stateRenderer = stateObject.GetComponent<Renderer>();

            Shader shader = Shader.Find("Universal Render Pipeline/Lit");
            if (shader != null)
            {
                _stateMaterial = new Material(shader)
                {
                    name = "Runtime Operation State Material"
                };
                _stateRenderer.material = _stateMaterial;
            }

            var statusObject = new GameObject("World Status");
            statusObject.transform.SetParent(transform, false);
            statusObject.transform.position = new Vector3(2.2f, 3f, 0f);
            _worldStatus = statusObject.AddComponent<TextMesh>();
            _worldStatus.anchor = TextAnchor.MiddleCenter;
            _worldStatus.alignment = TextAlignment.Center;
            _worldStatus.fontSize = 64;
            _worldStatus.characterSize = 0.12f;
            _worldStatus.color = Color.white;
        }

        private void ApplyVisualState(OperationSnapshot snapshot)
        {
            if (_worldStatus != null)
            {
                _worldStatus.text =
                    $"ASYNC OPERATION LIFECYCLE\n{snapshot.State}\nVersion {snapshot.Version}";
            }

            if (_stateMaterial != null)
            {
                _stateMaterial.color = GetStateColor(snapshot.State);
            }
        }

        private static Color GetStateColor(OperationState state)
        {
            switch (state)
            {
                case OperationState.Running:
                    return new Color(0.1f, 0.75f, 1f);
                case OperationState.Completed:
                    return new Color(0.2f, 0.9f, 0.35f);
                case OperationState.Cancelled:
                    return new Color(1f, 0.85f, 0.15f);
                case OperationState.TimedOut:
                    return new Color(1f, 0.45f, 0.1f);
                case OperationState.Failed:
                    return new Color(1f, 0.15f, 0.18f);
                case OperationState.Superseded:
                    return new Color(0.75f, 0.25f, 1f);
                default:
                    return new Color(0.45f, 0.5f, 0.6f);
            }
        }

        private void EnsureGuiStyles()
        {
            if (_titleStyle != null)
            {
                return;
            }

            _titleStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 30,
                fontStyle = FontStyle.Bold,
                wordWrap = true,
                normal = { textColor = Color.white }
            };
            _labelStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 18,
                wordWrap = true,
                normal = { textColor = Color.white }
            };
            _buttonStyle = new GUIStyle(GUI.skin.button)
            {
                fontSize = 17,
                fixedHeight = 44f,
                wordWrap = true,
                alignment = TextAnchor.MiddleLeft,
                padding = new RectOffset(14, 14, 6, 6)
            };
        }
    }
}
