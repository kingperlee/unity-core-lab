using System;
using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using UnityEngine;
using UnityCoreLab.AssetLifecycle;

namespace UnityCoreLab.Samples.AssetLifecycle
{
    /// <summary>
    /// Interactive Resources sample for shared loading and lease ownership.
    /// </summary>
    public sealed class AssetLifecycleDemo : MonoBehaviour
    {
        public const string ResourceKey = "AssetLifecycle/SharedProfile";

        private readonly List<string> _logs = new List<string>();
        private ResourcesUniTaskAssetProvider<TextAsset> _resourcesProvider;
        private AsyncSharedAssetStore<TextAsset> _store;
        private GameObject _firstOwnerObject;
        private GameObject _secondOwnerObject;
        private AssetLease<TextAsset> _firstLease;
        private AssetLease<TextAsset> _secondLease;
        private GUIStyle _titleStyle;
        private GUIStyle _labelStyle;
        private GUIStyle _buttonStyle;
        private Vector2 _scrollPosition;
        private bool _acquiring;
        private string _message = "Ready";
        private string _assetPreview = "Not loaded";

        public bool IsInitialized => _store != null;

        public int LoadRequestCount => _resourcesProvider?.LoadRequestCount ?? 0;

        public int UnloadCount => _resourcesProvider?.UnloadCount ?? 0;

        public int ReferenceCount => _store?.GetReferenceCount(ResourceKey) ?? 0;

        public int WaiterCount => _store?.GetWaiterCount(ResourceKey) ?? 0;

        public string AssetPreview => _assetPreview;

        private void Awake()
        {
            _resourcesProvider = new ResourcesUniTaskAssetProvider<TextAsset>();
            var adapter = new UniTaskAssetProviderAdapter<TextAsset>(
                _resourcesProvider);
            _store = new AsyncSharedAssetStore<TextAsset>(adapter);
            Record("Resources ownership demo initialized.");
        }

        private void OnDestroy()
        {
            _store?.Dispose();
            _store = null;
        }

        public async UniTask AcquireBothAsync()
        {
            if (_acquiring)
            {
                throw new InvalidOperationException("An acquire sample is already running.");
            }

            if (ReferenceCount > 0)
            {
                throw new InvalidOperationException(
                    "Release or destroy the current owners before acquiring again.");
            }

            _acquiring = true;
            _message = "Two owners are waiting for the same Resources key...";

            AssetLeaseScope firstOwner = CreateOwner(
                "Asset Owner A",
                ref _firstOwnerObject);
            AssetLeaseScope secondOwner = CreateOwner(
                "Asset Owner B",
                ref _secondOwnerObject);

            try
            {
                (_firstLease, _secondLease) = await UniTask.WhenAll(
                    _store.AcquireForOwnerAsync(ResourceKey, firstOwner),
                    _store.AcquireForOwnerAsync(ResourceKey, secondOwner));

                _assetPreview = _firstLease.Asset.text;
                _message = "Both owners acquired the same TextAsset instance.";
                Record(
                    $"Shared acquire complete | loads={LoadRequestCount} | refs={ReferenceCount}");
            }
            finally
            {
                _acquiring = false;
            }
        }

        public void ReleaseFirstOwnerEarly()
        {
            if (_firstLease == null)
            {
                _message = "Owner A has no active lease.";
                return;
            }

            _firstLease.Dispose();
            _firstLease = null;
            _message = "Owner A released early; Owner B still keeps the asset loaded.";
            Record($"Owner A released | refs={ReferenceCount} | unloads={UnloadCount}");
        }

        public void DestroyFirstOwner()
        {
            DestroyOwner(ref _firstOwnerObject, "Owner A");
            _firstLease = null;
        }

        public void DestroySecondOwner()
        {
            DestroyOwner(ref _secondOwnerObject, "Owner B");
            _secondLease = null;
        }

        private AssetLeaseScope CreateOwner(
            string ownerName,
            ref GameObject ownerObject)
        {
            if (ownerObject != null)
            {
                Destroy(ownerObject);
            }

            ownerObject = new GameObject(ownerName);
            ownerObject.transform.SetParent(transform, worldPositionStays: false);
            return ownerObject.AddComponent<AssetLeaseScope>();
        }

        private void DestroyOwner(ref GameObject ownerObject, string displayName)
        {
            if (ownerObject == null)
            {
                _message = $"{displayName} is already destroyed.";
                return;
            }

            Destroy(ownerObject);
            ownerObject = null;
            _message = $"{displayName} destruction scheduled; its scope will release on OnDestroy.";
            Record($"{displayName} destroy requested");
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
                1.25f);
            Matrix4x4 previousMatrix = GUI.matrix;
            GUI.matrix = Matrix4x4.Scale(new Vector3(scale, scale, 1f));

            float logicalWidth = Screen.width / scale;
            float logicalHeight = Screen.height / scale;
            float panelWidth = Mathf.Max(380f, Mathf.Min(620f, logicalWidth - 32f));
            float panelHeight = Mathf.Clamp(logicalHeight - 32f, 500f, 850f);

            GUILayout.BeginArea(
                new Rect(16f, 16f, panelWidth, panelHeight),
                GUI.skin.window);
            _scrollPosition = GUILayout.BeginScrollView(_scrollPosition);

            GUILayout.Label("Unity Core Lab", _titleStyle);
            GUILayout.Label("02.4 Resources ownership demo", _labelStyle);
            GUILayout.Space(8f);
            GUILayout.Label($"Resource key: {ResourceKey}", _labelStyle);
            GUILayout.Label($"Provider load requests: {LoadRequestCount}", _labelStyle);
            GUILayout.Label($"Active waiters: {WaiterCount}", _labelStyle);
            GUILayout.Label($"Active references: {ReferenceCount}", _labelStyle);
            GUILayout.Label($"Provider unloads: {UnloadCount}", _labelStyle);
            GUILayout.Label($"State: {_message}", _labelStyle);
            GUILayout.Label($"Asset: {_assetPreview}", _labelStyle);

            GUILayout.Space(12f);
            GUI.enabled = !_acquiring && ReferenceCount == 0;
            if (GUILayout.Button(
                    "Acquire the same asset for Owner A and Owner B",
                    _buttonStyle))
            {
                AcquireBothAsync().Forget(HandleException);
            }

            GUI.enabled = true;
            if (GUILayout.Button("Release Owner A lease early", _buttonStyle))
            {
                ReleaseFirstOwnerEarly();
            }

            if (GUILayout.Button("Destroy Owner A", _buttonStyle))
            {
                DestroyFirstOwner();
            }

            if (GUILayout.Button("Destroy Owner B", _buttonStyle))
            {
                DestroySecondOwner();
            }

            GUILayout.Space(14f);
            GUILayout.Label("Recent ownership events", _labelStyle);
            foreach (string entry in _logs)
            {
                GUILayout.Label(entry, _labelStyle);
            }

            GUILayout.EndScrollView();
            GUILayout.EndArea();
            GUI.matrix = previousMatrix;
        }

        private void HandleException(Exception exception)
        {
            if (exception is OperationCanceledException)
            {
                _message = "Acquire cancelled by an owner lifetime.";
                Record("Acquire cancelled");
                return;
            }

            _message = $"Acquire failed: {exception.Message}";
            Record(_message);
            Debug.LogException(exception, this);
        }

        private void Record(string message)
        {
            _logs.Insert(0, $"[{Time.frameCount}] {message}");
            if (_logs.Count > 8)
            {
                _logs.RemoveAt(_logs.Count - 1);
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
                wordWrap = true
            };
            _labelStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 18,
                wordWrap = true
            };
            _buttonStyle = new GUIStyle(GUI.skin.button)
            {
                fontSize = 18,
                wordWrap = true,
                fixedHeight = 52f
            };
        }
    }
}
