using Unity.Profiling;
using UnityEngine;
using UnityEngine.Rendering;
using Object = UnityEngine.Object;

namespace UnityCoreLab.Samples.RenderingPerformance.OverdrawFillRate
{
    /// <summary>
    /// Interactive comparison of opaque early-depth rejection and transparent
    /// alpha-blended overdraw at matched geometry and layer counts.
    /// </summary>
    public sealed class OverdrawFillRateDemo : MonoBehaviour
    {
        public const int DefaultLayerCount = 32;
        public const float DefaultCoverageRatio = 0.5f;
        public const int DefaultFragmentIterations = 8;
        private const string ShaderName =
            "UnityCoreLab/Rendering/OverdrawFillRate";

        [SerializeField] private Material _opaqueMaterialTemplate;
        [SerializeField] private Material _transparentMaterialTemplate;

        private OverdrawWorkload _workload;
        private Material _ownedOpaqueMaterial;
        private Material _ownedTransparentMaterial;
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

        public bool IsInitialized => _workload != null;

        public OverdrawWorkloadSnapshot Snapshot =>
            _workload?.Snapshot ?? default(OverdrawWorkloadSnapshot);

        private void Awake()
        {
            _meshSource = GameObject.CreatePrimitive(PrimitiveType.Quad);
            _meshSource.name = "Overdraw Quad Mesh Source";
            _meshSource.SetActive(false);
            Mesh mesh = _meshSource.GetComponent<MeshFilter>().sharedMesh;

            var workloadRoot = new GameObject("Overdraw Fill Rate Workload");
            workloadRoot.transform.SetParent(transform, false);

            EnsureCamera();
            _workload = new OverdrawWorkload(
                mesh,
                ResolveOpaqueMaterial(),
                ResolveTransparentMaterial(),
                workloadRoot.transform);
            StartCounters();
            Rebuild(
                OverdrawMode.OpaqueDepthWrite,
                DefaultLayerCount,
                DefaultCoverageRatio,
                DefaultFragmentIterations);
        }

        public void Rebuild(
            OverdrawMode mode,
            int layerCount,
            float coverageRatio,
            int fragmentIterations)
        {
            _workload.Rebuild(
                mode,
                layerCount,
                coverageRatio,
                fragmentIterations,
                FullscreenWorldSize());
        }

        private void OnDestroy()
        {
            _drawCallsRecorder.Dispose();
            _batchesRecorder.Dispose();
            _setPassRecorder.Dispose();
            _workload?.Dispose();
            _workload = null;
            DestroyOwned(_ownedOpaqueMaterial);
            DestroyOwned(_ownedTransparentMaterial);
            DestroyOwned(_meshSource);
            DestroyOwned(_ownedCameraObject);
        }

        private void EnsureCamera()
        {
            _targetCamera = Camera.main;
            if (_targetCamera == null)
            {
                _ownedCameraObject = new GameObject("Overdraw Fill Rate Camera");
                _ownedCameraObject.tag = "MainCamera";
                _targetCamera = _ownedCameraObject.AddComponent<Camera>();
            }

            _targetCamera.transform.position = new Vector3(0f, 0f, -10f);
            _targetCamera.transform.rotation = Quaternion.identity;
            _targetCamera.orthographic = true;
            _targetCamera.orthographicSize = 5f;
            _targetCamera.nearClipPlane = 0.3f;
            _targetCamera.farClipPlane = 50f;
            _targetCamera.clearFlags = CameraClearFlags.SolidColor;
            _targetCamera.backgroundColor = new Color(0.035f, 0.05f, 0.075f);
        }

        private Vector2 FullscreenWorldSize()
        {
            float height = _targetCamera.orthographicSize * 2f;
            float width = height * Mathf.Max(0.1f, _targetCamera.aspect);
            return new Vector2(width, height);
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

        private void OnGUI()
        {
            EnsureGuiStyles();
            GUI.depth = -100;

            float scale = Mathf.Clamp(Screen.height / 1080f, 0.85f, 1.15f);
            Matrix4x4 previousMatrix = GUI.matrix;
            GUI.matrix = Matrix4x4.Scale(new Vector3(scale, scale, 1f));

            float logicalHeight = Screen.height / scale;
            GUILayout.BeginArea(
                new Rect(16f, 16f, 500f, Mathf.Max(620f, logicalHeight - 32f)),
                GUI.skin.window);
            _scrollPosition = GUILayout.BeginScrollView(_scrollPosition);

            OverdrawWorkloadSnapshot snapshot = Snapshot;
            GUILayout.Label("Unity Core Lab", _titleStyle);
            GUILayout.Label("04.2 Overdraw / Fill Rate", _labelStyle);
            GUILayout.Space(8f);
            GUILayout.Label($"Mode: {snapshot.Mode}", _labelStyle);
            GUILayout.Label($"Overlapping layers: {snapshot.LayerCount}", _labelStyle);
            GUILayout.Label(
                $"Estimated screen coverage per layer: " +
                $"{snapshot.CoverageRatio:P0}",
                _labelStyle);
            GUILayout.Label(
                $"Equivalent fullscreen layers: " +
                $"{snapshot.EquivalentFullscreenLayers:0.0}",
                _labelStyle);
            GUILayout.Label(
                $"Fragment work iterations: {snapshot.FragmentIterations}",
                _labelStyle);
            GUILayout.Label(
                $"Relative fragment-work pressure: " +
                $"{snapshot.RelativeFragmentWork:0}",
                _labelStyle);
            GUILayout.Label($"Renderer components: {snapshot.RendererCount}", _labelStyle);
            GUILayout.Label($"Material instances: {snapshot.MaterialCount}", _labelStyle);
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
            if (GUILayout.Button("Opaque: ZWrite On", _buttonStyle))
            {
                Rebuild(
                    OverdrawMode.OpaqueDepthWrite,
                    snapshot.LayerCount,
                    snapshot.CoverageRatio,
                    snapshot.FragmentIterations);
            }

            if (GUILayout.Button(
                    "Transparent: Alpha Blend + ZWrite Off",
                    _buttonStyle))
            {
                Rebuild(
                    OverdrawMode.TransparentAlphaBlend,
                    snapshot.LayerCount,
                    snapshot.CoverageRatio,
                    snapshot.FragmentIterations);
            }

            GUILayout.Space(10f);
            GUILayout.Label("Layer count", _labelStyle);
            GUILayout.BeginHorizontal();
            LayerButton(snapshot, 8);
            LayerButton(snapshot, 32);
            LayerButton(snapshot, 64);
            LayerButton(snapshot, 128);
            GUILayout.EndHorizontal();

            GUILayout.Space(10f);
            GUILayout.Label("Estimated screen coverage per layer", _labelStyle);
            GUILayout.BeginHorizontal();
            CoverageButton(snapshot, 0.25f, "25%");
            CoverageButton(snapshot, 0.5f, "50%");
            CoverageButton(snapshot, 1f, "100%");
            GUILayout.EndHorizontal();

            GUILayout.Space(10f);
            GUILayout.Label("Matched fragment work", _labelStyle);
            GUILayout.BeginHorizontal();
            FragmentWorkButton(snapshot, 1);
            FragmentWorkButton(snapshot, 8);
            FragmentWorkButton(snapshot, 24);
            GUILayout.EndHorizontal();

            GUILayout.Space(12f);
            GUILayout.Label(
                "Both modes use the same fragment loop. Relative pressure = " +
                "layers x coverage x iterations; it is not measured GPU time. " +
                "Use 64 or 128 layers, 100% coverage, and 24 iterations to " +
                "make the desktop-GPU difference visible. Confirm GPU timing " +
                "in a Development Build.",
                _labelStyle);

            GUILayout.EndScrollView();
            GUILayout.EndArea();
            GUI.matrix = previousMatrix;
        }

        private void LayerButton(OverdrawWorkloadSnapshot snapshot, int count)
        {
            if (GUILayout.Button(count.ToString(), _buttonStyle))
            {
                Rebuild(
                    snapshot.Mode,
                    count,
                    snapshot.CoverageRatio,
                    snapshot.FragmentIterations);
            }
        }

        private void CoverageButton(
            OverdrawWorkloadSnapshot snapshot,
            float ratio,
            string label)
        {
            if (GUILayout.Button(label, _buttonStyle))
            {
                Rebuild(
                    snapshot.Mode,
                    snapshot.LayerCount,
                    ratio,
                    snapshot.FragmentIterations);
            }
        }

        private void FragmentWorkButton(
            OverdrawWorkloadSnapshot snapshot,
            int iterations)
        {
            if (GUILayout.Button(iterations.ToString(), _buttonStyle))
            {
                Rebuild(
                    snapshot.Mode,
                    snapshot.LayerCount,
                    snapshot.CoverageRatio,
                    iterations);
            }
        }

        private Material ResolveOpaqueMaterial()
        {
            if (_opaqueMaterialTemplate != null)
            {
                return _opaqueMaterialTemplate;
            }

            Shader shader = ResolveShader();
            _ownedOpaqueMaterial = new Material(shader)
            {
                name = "Runtime Opaque Depth Write",
                renderQueue = (int)RenderQueue.Geometry
            };
            _ownedOpaqueMaterial.SetInt("_SrcBlend", (int)BlendMode.One);
            _ownedOpaqueMaterial.SetInt("_DstBlend", (int)BlendMode.Zero);
            _ownedOpaqueMaterial.SetInt("_ZWrite", 1);
            _ownedOpaqueMaterial.SetOverrideTag("RenderType", "Opaque");
            return _ownedOpaqueMaterial;
        }

        private Material ResolveTransparentMaterial()
        {
            if (_transparentMaterialTemplate != null)
            {
                return _transparentMaterialTemplate;
            }

            Shader shader = ResolveShader();
            _ownedTransparentMaterial = new Material(shader)
            {
                name = "Runtime Transparent Alpha Blend",
                renderQueue = (int)RenderQueue.Transparent
            };
            _ownedTransparentMaterial.SetInt("_SrcBlend", (int)BlendMode.SrcAlpha);
            _ownedTransparentMaterial.SetInt(
                "_DstBlend",
                (int)BlendMode.OneMinusSrcAlpha);
            _ownedTransparentMaterial.SetInt("_ZWrite", 0);
            _ownedTransparentMaterial.SetOverrideTag("RenderType", "Transparent");
            return _ownedTransparentMaterial;
        }

        private static Shader ResolveShader()
        {
            Shader shader = Shader.Find(ShaderName);
            if (shader == null)
            {
                throw new MissingReferenceException($"{ShaderName} shader was not found.");
            }

            return shader;
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
