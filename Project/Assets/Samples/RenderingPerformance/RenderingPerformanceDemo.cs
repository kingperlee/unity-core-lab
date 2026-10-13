using Unity.Profiling;
using UnityEngine;
using Object = UnityEngine.Object;

namespace UnityCoreLab.Samples.RenderingPerformance
{
    /// <summary>
    /// Interactive rendering workload for inspecting material ownership,
    /// batching behavior, and actual Profiler counters.
    /// </summary>
    public sealed class RenderingPerformanceDemo : MonoBehaviour
    {
        public const int DefaultInstanceCount = 400;
        private const string InstancedShaderName =
            "UnityCoreLab/Rendering/InstancedColor";

        [SerializeField] private Material _rendererMaterialTemplate;
        [SerializeField] private Material _instancedMaterialTemplate;

        private RenderingWorkload _workload;
        private Material _ownedRendererMaterial;
        private Material _ownedInstancedMaterial;
        private GameObject _meshSource;
        private GameObject _ownedCameraObject;
        private Camera _targetCamera;
        private ProfilerRecorder _drawCallsRecorder;
        private ProfilerRecorder _batchesRecorder;
        private ProfilerRecorder _setPassRecorder;
        private GUIStyle _titleStyle;
        private GUIStyle _labelStyle;
        private GUIStyle _buttonStyle;
        private Vector2 _scrollPosition;
        private int _instanceCount = DefaultInstanceCount;

        public RenderingWorkloadSnapshot Snapshot =>
            _workload?.Snapshot ?? default(RenderingWorkloadSnapshot);

        public bool IsInitialized => _workload != null;

        private void Awake()
        {
            _meshSource = GameObject.CreatePrimitive(PrimitiveType.Cube);
            _meshSource.name = "Rendering Workload Mesh Source";
            _meshSource.SetActive(false);
            Mesh mesh = _meshSource.GetComponent<MeshFilter>().sharedMesh;

            Material rendererMaterial = ResolveRendererMaterial();
            Material instancedMaterial = ResolveInstancedMaterial();

            var workloadRoot = new GameObject("Rendering Workload");
            workloadRoot.transform.SetParent(transform, false);
            _workload = new RenderingWorkload(
                mesh,
                rendererMaterial,
                instancedMaterial,
                workloadRoot.transform);

            EnsureCamera();
            StartCounters();
            Rebuild(RenderingWorkloadMode.UniqueMaterialRenderers);
        }

        public void Rebuild(RenderingWorkloadMode mode, int instanceCount)
        {
            _instanceCount = instanceCount;
            int columns = Mathf.Max(1, Mathf.CeilToInt(Mathf.Sqrt(instanceCount)));
            _workload.Rebuild(mode, instanceCount, columns);
            FitCamera(instanceCount, columns);
        }

        private void Rebuild(RenderingWorkloadMode mode)
        {
            Rebuild(mode, _instanceCount);
        }

        private void LateUpdate()
        {
            _workload?.RenderInstanced(_targetCamera);
        }

        private void OnDestroy()
        {
            _drawCallsRecorder.Dispose();
            _batchesRecorder.Dispose();
            _setPassRecorder.Dispose();
            _workload?.Dispose();
            _workload = null;
            DestroyOwned(_ownedRendererMaterial);
            DestroyOwned(_ownedInstancedMaterial);
            DestroyOwned(_meshSource);
            DestroyOwned(_ownedCameraObject);
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
        }

        private void EnsureCamera()
        {
            _targetCamera = Camera.main;
            if (_targetCamera != null)
            {
                return;
            }

            _ownedCameraObject = new GameObject("Rendering Performance Camera");
            _ownedCameraObject.tag = "MainCamera";
            _ownedCameraObject.transform.position = new Vector3(0f, 0f, -20f);
            _targetCamera = _ownedCameraObject.AddComponent<Camera>();
            _targetCamera.orthographic = true;
            _targetCamera.orthographicSize = 10f;
            _targetCamera.clearFlags = CameraClearFlags.SolidColor;
            _targetCamera.backgroundColor = new Color(0.055f, 0.075f, 0.11f);
        }

        private void FitCamera(int instanceCount, int columns)
        {
            if (_targetCamera == null || !_targetCamera.orthographic)
            {
                return;
            }

            const float spacing = 0.85f;
            int rows = Mathf.CeilToInt((float)instanceCount / columns);
            float verticalHalfSize = rows * spacing * 0.5f + 1f;
            float horizontalHalfSize = columns * spacing * 0.5f + 1f;
            float aspect = Mathf.Max(0.1f, _targetCamera.aspect);
            _targetCamera.orthographicSize = Mathf.Max(
                verticalHalfSize,
                horizontalHalfSize / aspect);
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
                new Rect(16f, 16f, 500f, Mathf.Max(560f, logicalHeight - 32f)),
                GUI.skin.window);
            _scrollPosition = GUILayout.BeginScrollView(_scrollPosition);

            RenderingWorkloadSnapshot snapshot = Snapshot;
            GUILayout.Label("Unity Core Lab", _titleStyle);
            GUILayout.Label("04.1 Rendering submission baseline", _labelStyle);
            GUILayout.Space(8f);
            GUILayout.Label($"Mode: {snapshot.Mode}", _labelStyle);
            GUILayout.Label($"Logical instances: {snapshot.InstanceCount}", _labelStyle);
            GUILayout.Label($"Renderer components: {snapshot.RendererCount}", _labelStyle);
            GUILayout.Label($"Material instances: {snapshot.MaterialCount}", _labelStyle);
            GUILayout.Label(
                $"Explicit instanced API calls: {snapshot.ExplicitInstancedBatchCount}",
                _labelStyle);
            GUILayout.Label(
                $"Explicit per-instance colors: {snapshot.ExplicitColorCount}",
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

            GUILayout.Space(12f);
            if (GUILayout.Button("Unique material per Renderer", _buttonStyle))
            {
                Rebuild(RenderingWorkloadMode.UniqueMaterialRenderers);
            }

            if (GUILayout.Button("Shared material only", _buttonStyle))
            {
                Rebuild(RenderingWorkloadMode.SharedMaterialRenderers);
            }

            if (GUILayout.Button(
                    "Shared material + MaterialPropertyBlock",
                    _buttonStyle))
            {
                Rebuild(RenderingWorkloadMode.SharedMaterialPropertyBlocks);
            }

            if (GUILayout.Button("Explicit GPU instancing", _buttonStyle))
            {
                Rebuild(RenderingWorkloadMode.ExplicitGpuInstancing);
            }

            GUILayout.Space(10f);
            GUILayout.BeginHorizontal();
            if (GUILayout.Button("100", _buttonStyle))
            {
                Rebuild(snapshot.Mode, 100);
            }

            if (GUILayout.Button("400", _buttonStyle))
            {
                Rebuild(snapshot.Mode, 400);
            }

            if (GUILayout.Button("1000", _buttonStyle))
            {
                Rebuild(snapshot.Mode, 1000);
            }

            GUILayout.EndHorizontal();
            GUILayout.Space(12f);
            GUILayout.Label(
                "Profiler counters include the whole frame. Use Frame Debugger " +
                "to inspect how URP actually grouped these submissions.",
                _labelStyle);

            GUILayout.EndScrollView();
            GUILayout.EndArea();
            GUI.matrix = previousMatrix;
        }

        private Material ResolveRendererMaterial()
        {
            if (_rendererMaterialTemplate != null)
            {
                _ownedRendererMaterial = new Material(_rendererMaterialTemplate)
                {
                    name = "Rendering Workload Runtime SRP Material",
                    enableInstancing = false
                };
                return _ownedRendererMaterial;
            }

            Shader shader = Shader.Find("Universal Render Pipeline/Unlit");
            if (shader == null)
            {
                throw new MissingReferenceException(
                    "Universal Render Pipeline/Unlit shader was not found.");
            }

            _ownedRendererMaterial = new Material(shader)
            {
                name = "Rendering Workload Runtime SRP Material",
                enableInstancing = false,
                color = new Color(0.2f, 0.72f, 1f, 1f)
            };
            return _ownedRendererMaterial;
        }

        private Material ResolveInstancedMaterial()
        {
            if (_instancedMaterialTemplate != null)
            {
                _ownedInstancedMaterial = new Material(_instancedMaterialTemplate)
                {
                    name = "Rendering Workload Runtime Instanced Material",
                    enableInstancing = true
                };
                return _ownedInstancedMaterial;
            }

            Shader shader = Shader.Find(InstancedShaderName);
            if (shader == null)
            {
                throw new MissingReferenceException(
                    $"{InstancedShaderName} shader was not found.");
            }

            _ownedInstancedMaterial = new Material(shader)
            {
                name = "Rendering Workload Runtime Instanced Material",
                enableInstancing = true
            };
            return _ownedInstancedMaterial;
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
