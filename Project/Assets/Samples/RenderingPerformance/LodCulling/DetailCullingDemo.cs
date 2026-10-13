using Unity.Profiling;
using UnityEngine;
using Object = UnityEngine.Object;

namespace UnityCoreLab.Samples.RenderingPerformance.LodCulling
{
    /// <summary>
    /// Interactive 04.3 workload for separating geometric detail reduction
    /// from explicit visibility policy and Unity's internal Renderer culling.
    /// </summary>
    public sealed class DetailCullingDemo : MonoBehaviour
    {
        public const int DefaultObjectCount = 225;
        public const float DefaultHighDetailDistance = 18f;
        public const float DefaultCullDistance = 42f;

        [SerializeField] private Material _materialTemplate;

        private DetailCullingWorkload _workload;
        private Material _ownedMaterial;
        private GameObject _highMeshSource;
        private GameObject _lowMeshSource;
        private GameObject _ownedCameraObject;
        private Camera _targetCamera;
        private DetailCullingMode _mode = DetailCullingMode.AllHighDetail;
        private float _highDetailDistance = DefaultHighDetailDistance;
        private float _cullDistance = DefaultCullDistance;
        private bool _updatePolicyActiveOnly;
        private int _simulatedUpdateIterations = 32;
        private ProfilerRecorder _drawCallsRecorder;
        private ProfilerRecorder _batchesRecorder;
        private ProfilerRecorder _setPassRecorder;
        private ProfilerRecorder _trianglesRecorder;
        private GUIStyle _titleStyle;
        private GUIStyle _labelStyle;
        private GUIStyle _buttonStyle;
        private Vector2 _scrollPosition;

        public bool IsInitialized => _workload != null;

        public DetailCullingSnapshot Snapshot =>
            _workload?.Snapshot ?? default(DetailCullingSnapshot);

        private void Awake()
        {
            _highMeshSource = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            _highMeshSource.name = "LOD High Mesh Source";
            _highMeshSource.SetActive(false);
            Mesh highMesh =
                _highMeshSource.GetComponent<MeshFilter>().sharedMesh;

            _lowMeshSource = GameObject.CreatePrimitive(PrimitiveType.Cube);
            _lowMeshSource.name = "LOD Low Mesh Source";
            _lowMeshSource.SetActive(false);
            Mesh lowMesh = _lowMeshSource.GetComponent<MeshFilter>().sharedMesh;

            var workloadRoot = new GameObject("LOD Culling Workload");
            workloadRoot.transform.SetParent(transform, false);

            EnsureCamera();
            _workload = new DetailCullingWorkload(
                highMesh,
                lowMesh,
                ResolveMaterial(),
                workloadRoot.transform);
            _workload.Rebuild(BuildGridPositions(DefaultObjectCount, 15));
            StartCounters();
            EvaluateNow();
            RunSimulatedUpdate();
        }

        private void Update()
        {
            EvaluateNow();
            RunSimulatedUpdate();
        }

        public void SetMode(DetailCullingMode mode)
        {
            _mode = mode;
            EvaluateNow();
        }

        public void SetDistances(
            float highDetailDistance,
            float cullDistance)
        {
            _highDetailDistance = highDetailDistance;
            _cullDistance = cullDistance;
            EvaluateNow();
        }

        public void RebuildObjects(int objectCount)
        {
            if (objectCount <= 0)
            {
                throw new System.ArgumentOutOfRangeException(
                    nameof(objectCount));
            }

            int columns = Mathf.CeilToInt(Mathf.Sqrt(objectCount));
            _workload.Rebuild(BuildGridPositions(objectCount, columns));
            EvaluateNow();
            RunSimulatedUpdate();
        }

        public void SetSimulatedUpdatePolicy(
            bool policyActiveOnly,
            int iterations)
        {
            _updatePolicyActiveOnly = policyActiveOnly;
            _simulatedUpdateIterations = iterations;
            RunSimulatedUpdate();
        }

        public void SetCameraPreset(int preset)
        {
            switch (preset)
            {
                case 0:
                    PositionCamera(
                        new Vector3(0f, 10f, -14f),
                        new Vector3(0f, 0f, 17f));
                    break;

                case 1:
                    PositionCamera(
                        new Vector3(0f, 9f, 6f),
                        new Vector3(0f, 0f, 22f));
                    break;

                case 2:
                    PositionCamera(
                        new Vector3(0f, 9f, 25f),
                        new Vector3(0f, 0f, 28f));
                    break;

                default:
                    throw new System.ArgumentOutOfRangeException(
                        nameof(preset),
                        preset,
                        null);
            }

            EvaluateNow();
        }

        private void OnDestroy()
        {
            _drawCallsRecorder.Dispose();
            _batchesRecorder.Dispose();
            _setPassRecorder.Dispose();
            _trianglesRecorder.Dispose();
            _workload?.Dispose();
            _workload = null;
            DestroyOwned(_ownedMaterial);
            DestroyOwned(_highMeshSource);
            DestroyOwned(_lowMeshSource);
            DestroyOwned(_ownedCameraObject);
        }

        private void EvaluateNow()
        {
            if (_workload == null || _targetCamera == null)
            {
                return;
            }

            _workload.Evaluate(
                _targetCamera,
                _mode,
                _highDetailDistance,
                _cullDistance);
        }

        private void RunSimulatedUpdate()
        {
            _workload?.RunSimulatedUpdate(
                _updatePolicyActiveOnly,
                _simulatedUpdateIterations,
                Time.unscaledDeltaTime);
        }

        private static Vector3[] BuildGridPositions(
            int objectCount,
            int columns)
        {
            const float horizontalSpacing = 2.2f;
            const float depthSpacing = 2.7f;
            var positions = new Vector3[objectCount];
            float xOffset = (columns - 1) * horizontalSpacing * 0.5f;

            for (int index = 0; index < objectCount; index++)
            {
                int column = index % columns;
                int row = index / columns;
                positions[index] = new Vector3(
                    column * horizontalSpacing - xOffset,
                    0f,
                    row * depthSpacing);
            }

            return positions;
        }

        private void EnsureCamera()
        {
            _targetCamera = Camera.main;
            if (_targetCamera == null)
            {
                _ownedCameraObject = new GameObject("LOD Culling Camera");
                _ownedCameraObject.tag = "MainCamera";
                _targetCamera = _ownedCameraObject.AddComponent<Camera>();
            }

            _targetCamera.orthographic = false;
            _targetCamera.fieldOfView = 60f;
            _targetCamera.nearClipPlane = 0.3f;
            _targetCamera.farClipPlane = 100f;
            _targetCamera.clearFlags = CameraClearFlags.SolidColor;
            _targetCamera.backgroundColor = new Color(0.035f, 0.05f, 0.075f);
            SetCameraPreset(0);
        }

        private void PositionCamera(Vector3 position, Vector3 target)
        {
            _targetCamera.transform.position = position;
            _targetCamera.transform.LookAt(target, Vector3.up);
        }

        private Material ResolveMaterial()
        {
            if (_materialTemplate != null)
            {
                _ownedMaterial = new Material(_materialTemplate)
                {
                    name = "LOD Culling Runtime Material"
                };
                return _ownedMaterial;
            }

            Shader shader = Shader.Find("Universal Render Pipeline/Unlit");
            if (shader == null)
            {
                throw new MissingReferenceException(
                    "Universal Render Pipeline/Unlit shader was not found.");
            }

            _ownedMaterial = new Material(shader)
            {
                name = "LOD Culling Runtime Material",
                color = new Color(0.2f, 0.72f, 1f, 1f)
            };
            return _ownedMaterial;
        }

        private void StartCounters()
        {
            _drawCallsRecorder = ProfilerRecorder.StartNew(
                ProfilerCategory.Render,
                "Draw Calls Count",
                1);
            _batchesRecorder = ProfilerRecorder.StartNew(
                ProfilerCategory.Render,
                "Batches Count",
                1);
            _setPassRecorder = ProfilerRecorder.StartNew(
                ProfilerCategory.Render,
                "SetPass Calls Count",
                1);
            _trianglesRecorder = ProfilerRecorder.StartNew(
                ProfilerCategory.Render,
                "Triangles Count",
                1);
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
                new Rect(16f, 16f, 520f, Mathf.Max(690f, logicalHeight - 32f)),
                GUI.skin.window);
            _scrollPosition = GUILayout.BeginScrollView(_scrollPosition);

            DetailCullingSnapshot snapshot = Snapshot;
            GUILayout.Label("Unity Core Lab", _titleStyle);
            GUILayout.Label("04.3 LOD and visibility policy", _labelStyle);
            GUILayout.Space(8f);
            GUILayout.Label($"Mode: {snapshot.Mode}", _labelStyle);
            GUILayout.Label($"Logical objects: {snapshot.ObjectCount}", _labelStyle);
            GUILayout.Label($"High-detail meshes: {snapshot.HighDetailCount}", _labelStyle);
            GUILayout.Label($"Low-detail meshes: {snapshot.LowDetailCount}", _labelStyle);
            GUILayout.Label(
                $"Policy-culled objects: {snapshot.PolicyCulledCount}",
                _labelStyle);
            GUILayout.Label(
                $"Estimated visible triangles: " +
                $"{snapshot.EstimatedVisibleTriangleCount:N0}",
                _labelStyle);
            GUILayout.Label(
                $"Simulated logic objects: " +
                $"{_workload.LastSimulatedUpdateCount:N0}",
                _labelStyle);
            GUILayout.Label(
                $"Simulated iterations per object: " +
                $"{_workload.LastSimulatedUpdateIterations}",
                _labelStyle);
            GUILayout.Space(8f);
            GUILayout.Label(
                $"Profiler Draw Calls: {CounterValue(_drawCallsRecorder)}",
                _labelStyle);
            GUILayout.Label(
                $"Profiler Batches: {CounterValue(_batchesRecorder)}",
                _labelStyle);
            GUILayout.Label(
                $"Profiler SetPass Calls: {CounterValue(_setPassRecorder)}",
                _labelStyle);
            GUILayout.Label(
                $"Profiler Triangles: {CounterValue(_trianglesRecorder)}",
                _labelStyle);

            GUILayout.Space(12f);
            if (GUILayout.Button("All objects: high detail", _buttonStyle))
            {
                SetMode(DetailCullingMode.AllHighDetail);
            }

            if (GUILayout.Button("Distance LOD", _buttonStyle))
            {
                SetMode(DetailCullingMode.DistanceLod);
            }

            if (GUILayout.Button(
                    "Distance LOD + explicit distance/frustum policy",
                    _buttonStyle))
            {
                SetMode(DetailCullingMode.DistanceLodAndExplicitCulling);
            }

            GUILayout.Space(10f);
            GUILayout.Label("Distance thresholds", _labelStyle);
            GUILayout.BeginHorizontal();
            if (GUILayout.Button("12 / 32", _buttonStyle))
            {
                SetDistances(12f, 32f);
            }

            if (GUILayout.Button("18 / 42", _buttonStyle))
            {
                SetDistances(18f, 42f);
            }

            if (GUILayout.Button("24 / 55", _buttonStyle))
            {
                SetDistances(24f, 55f);
            }

            GUILayout.EndHorizontal();

            GUILayout.Space(10f);
            GUILayout.Label("Object count (wait one frame after rebuild)", _labelStyle);
            GUILayout.BeginHorizontal();
            if (GUILayout.Button("225", _buttonStyle))
            {
                RebuildObjects(225);
            }

            if (GUILayout.Button("1024", _buttonStyle))
            {
                RebuildObjects(1024);
            }

            if (GUILayout.Button("4096", _buttonStyle))
            {
                RebuildObjects(4096);
            }

            GUILayout.EndHorizontal();

            GUILayout.Space(10f);
            GUILayout.Label("Synthetic CPU update scope", _labelStyle);
            if (GUILayout.Button("Update logic on all objects", _buttonStyle))
            {
                SetSimulatedUpdatePolicy(
                    policyActiveOnly: false,
                    iterations: _simulatedUpdateIterations);
            }

            if (GUILayout.Button(
                    "Update logic on policy-active objects only",
                    _buttonStyle))
            {
                SetSimulatedUpdatePolicy(
                    policyActiveOnly: true,
                    iterations: _simulatedUpdateIterations);
            }

            GUILayout.BeginHorizontal();
            SimulatedIterationsButton(8);
            SimulatedIterationsButton(32);
            SimulatedIterationsButton(96);
            GUILayout.EndHorizontal();

            GUILayout.Space(10f);
            GUILayout.Label("Camera preset", _labelStyle);
            GUILayout.BeginHorizontal();
            if (GUILayout.Button("Start", _buttonStyle))
            {
                SetCameraPreset(0);
            }

            if (GUILayout.Button("Middle", _buttonStyle))
            {
                SetCameraPreset(1);
            }

            if (GUILayout.Button("Far", _buttonStyle))
            {
                SetCameraPreset(2);
            }

            GUILayout.EndHorizontal();

            GUILayout.Space(12f);
            GUILayout.Label(
                "Policy-culled counts only objects explicitly disabled by the " +
                "sample. Unity can still internally frustum-cull enabled " +
                "Renderers, so Profiler triangle counts can be lower than the " +
                "deterministic estimate. Synthetic update work is intentionally " +
                "separate; compare its Profiler marker rather than treating it " +
                "as a real gameplay benchmark.",
                _labelStyle);

            GUILayout.EndScrollView();
            GUILayout.EndArea();
            GUI.matrix = previousMatrix;
        }

        private void SimulatedIterationsButton(int iterations)
        {
            if (GUILayout.Button(iterations.ToString(), _buttonStyle))
            {
                SetSimulatedUpdatePolicy(
                    _updatePolicyActiveOnly,
                    iterations);
            }
        }

        private static string CounterValue(ProfilerRecorder recorder)
        {
            return recorder.Valid ? recorder.LastValue.ToString() : "Unavailable";
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

        private static void DestroyOwned(Object ownedObject)
        {
            if (ownedObject == null)
            {
                return;
            }

            if (Application.isPlaying)
            {
                Object.Destroy(ownedObject);
            }
            else
            {
                Object.DestroyImmediate(ownedObject);
            }
        }
    }
}
