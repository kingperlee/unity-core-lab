using System;
using System.Collections.Generic;
using UnityEngine;
using UnityCoreLab.ObjectPooling;
using Object = UnityEngine.Object;

namespace UnityCoreLab.Samples.ObjectPooling
{
    /// <summary>
    /// Interactive GameObject pooling sample with bounded retention and visible
    /// reuse statistics.
    /// </summary>
    public sealed class ObjectPoolingDemo : MonoBehaviour
    {
        public const int RetainedCapacity = 4;
        public const int PrewarmCount = 4;

        private readonly List<GameObject> _activeInstances =
            new List<GameObject>();
        private GameObjectPool _pool;
        private GameObject _template;
        private GameObject _ownedCamera;
        private Material _material;
        private GUIStyle _titleStyle;
        private GUIStyle _labelStyle;
        private GUIStyle _buttonStyle;
        private Vector2 _scrollPosition;

        public bool IsInitialized => _pool != null;

        public int ActiveCount => Snapshot.ActiveCount;

        public int AvailableCount => Snapshot.AvailableCount;

        public int CreatedCount => Snapshot.CreatedCount;

        public int ReusedCount => Snapshot.ReusedCount;

        public int DiscardedCount => Snapshot.DiscardedCount;

        public int PeakActiveCount => Snapshot.PeakActiveCount;

        public PoolSnapshot Snapshot =>
            _pool?.Snapshot ?? default(PoolSnapshot);

        private void Awake()
        {
            Transform activeRoot = CreateChild("Active Instances");
            Transform inactiveRoot = CreateChild("Inactive Instances");
            _template = CreateTemplate();
            _template.transform.SetParent(inactiveRoot, false);

            _pool = new GameObjectPool(
                _template,
                activeRoot,
                inactiveRoot,
                RetainedCapacity);
            _pool.Prewarm(PrewarmCount);
            EnsureCamera();
        }

        public GameObject SpawnOne()
        {
            int index = _activeInstances.Count;
            float x = ((index % 5) - 2) * 1.35f;
            float y = ((index / 5) - 1) * 1.35f;
            GameObject instance = _pool.Rent(new Vector3(x, y, 0f));

            var actor = instance.GetComponent<PooledDemoActor>();
            float horizontalSpeed = 0.45f + ((index % 3) * 0.2f);
            float verticalSpeed = 0.25f + ((index % 2) * 0.18f);
            actor.Configure(new Vector3(
                index % 2 == 0 ? horizontalSpeed : -horizontalSpeed,
                index % 3 == 0 ? verticalSpeed : -verticalSpeed,
                0f));

            _activeInstances.Add(instance);
            return instance;
        }

        public void SpawnBurst(int count = 6)
        {
            if (count < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(count));
            }

            for (int i = 0; i < count; i++)
            {
                SpawnOne();
            }
        }

        public void ReturnOldest()
        {
            if (_activeInstances.Count == 0)
            {
                return;
            }

            GameObject instance = _activeInstances[0];
            _activeInstances.RemoveAt(0);
            _pool.Return(instance);
        }

        public void ReturnAll()
        {
            for (int i = _activeInstances.Count - 1; i >= 0; i--)
            {
                _pool.Return(_activeInstances[i]);
            }

            _activeInstances.Clear();
        }

        private void OnDestroy()
        {
            if (_pool != null)
            {
                ReturnAll();
                _pool.Dispose();
                _pool = null;
            }

            DestroyOwned(_material);
            DestroyOwned(_ownedCamera);
        }

        private Transform CreateChild(string childName)
        {
            var child = new GameObject(childName);
            child.transform.SetParent(transform, false);
            return child.transform;
        }

        private GameObject CreateTemplate()
        {
            GameObject template = GameObject.CreatePrimitive(PrimitiveType.Cube);
            template.name = "Pooled Cube Template";
            template.transform.localScale = Vector3.one * 0.72f;
            template.AddComponent<PooledDemoActor>();

            Shader shader = Shader.Find("Universal Render Pipeline/Unlit");
            if (shader == null)
            {
                shader = Shader.Find("Unlit/Color");
            }

            if (shader != null)
            {
                _material = new Material(shader)
                {
                    color = new Color(0.18f, 0.72f, 1f, 1f)
                };
                template.GetComponent<Renderer>().sharedMaterial = _material;
            }

            template.SetActive(false);
            return template;
        }

        private void EnsureCamera()
        {
            if (Camera.main != null)
            {
                return;
            }

            _ownedCamera = new GameObject("Object Pooling Demo Camera");
            _ownedCamera.tag = "MainCamera";
            _ownedCamera.transform.position = new Vector3(0f, 0f, -10f);
            Camera cameraComponent = _ownedCamera.AddComponent<Camera>();
            cameraComponent.orthographic = true;
            cameraComponent.orthographicSize = 5f;
            cameraComponent.clearFlags = CameraClearFlags.SolidColor;
            cameraComponent.backgroundColor = new Color(0.08f, 0.11f, 0.16f);
        }

        private void OnGUI()
        {
            EnsureGuiStyles();
            GUI.depth = -100;

            const float referencePixelCount = 1080f * 1920f;
            float pixelCount = Mathf.Max(1f, Screen.width * Screen.height);
            float scale = Mathf.Clamp(
                Mathf.Sqrt(pixelCount / referencePixelCount),
                0.85f,
                1.15f);
            Matrix4x4 previousMatrix = GUI.matrix;
            GUI.matrix = Matrix4x4.Scale(new Vector3(scale, scale, 1f));

            float logicalWidth = Screen.width / scale;
            float logicalHeight = Screen.height / scale;
            float panelWidth = Mathf.Max(360f, Mathf.Min(540f, logicalWidth - 32f));
            float panelHeight = Mathf.Clamp(logicalHeight - 32f, 480f, 760f);

            GUILayout.BeginArea(
                new Rect(16f, 16f, panelWidth, panelHeight),
                GUI.skin.window);
            _scrollPosition = GUILayout.BeginScrollView(_scrollPosition);

            GUILayout.Label("Unity Core Lab", _titleStyle);
            GUILayout.Label("03.2 GameObject pool lifecycle", _labelStyle);
            GUILayout.Space(8f);
            GUILayout.Label($"Active rentals: {ActiveCount}", _labelStyle);
            GUILayout.Label($"Available / capacity: {AvailableCount} / {RetainedCapacity}", _labelStyle);
            GUILayout.Label($"Created: {CreatedCount}", _labelStyle);
            GUILayout.Label($"Reused: {ReusedCount}", _labelStyle);
            GUILayout.Label($"Discarded overflow: {DiscardedCount}", _labelStyle);
            GUILayout.Label($"Peak active: {PeakActiveCount}", _labelStyle);

            GUILayout.Space(12f);
            if (GUILayout.Button("Spawn one", _buttonStyle))
            {
                SpawnOne();
            }

            if (GUILayout.Button("Spawn burst (6)", _buttonStyle))
            {
                SpawnBurst();
            }

            if (GUILayout.Button("Return oldest", _buttonStyle))
            {
                ReturnOldest();
            }

            if (GUILayout.Button("Return all", _buttonStyle))
            {
                ReturnAll();
            }

            GUILayout.Space(12f);
            GUILayout.Label(
                "Try: Spawn burst, Return all, then Spawn burst again. " +
                "Only four inactive objects are retained; the rest are discarded.",
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
                fontSize = 17,
                wordWrap = true,
                fixedHeight = 46f
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
