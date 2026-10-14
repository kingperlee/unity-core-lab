using UnityEngine;
using UnityEngine.UI;
using UnityCoreLab.UiNavigation;

namespace UnityCoreLab.Samples.UiNavigation
{
    /// <summary>
    /// 06.3 integration demo: route registry -> Resources prefab -> owned view
    /// instance -> shared asset lease release.
    /// </summary>
    public sealed class UiPrefabFactoryDemo : MonoBehaviour
    {
        private const int HistoryCapacity = 12;
        private readonly string[] _history = new string[HistoryCapacity];
        private int _historyStart;
        private int _historyCount;
        private UiNavigationStack _navigation;
        private UiViewLifecycleCoordinator _views;
        private ResourcesPrefabUiViewFactory _factory;
        private string _status;
        private Vector2 _scroll;
        private GUIStyle _titleStyle;
        private GUIStyle _headingStyle;
        private GUIStyle _labelStyle;
        private GUIStyle _buttonStyle;

        public UiNavigationStack Navigation => _navigation;

        public UiViewLifecycleCoordinator Views => _views;

        public int LoadRequestCount => _factory?.LoadRequestCount ?? 0;

        public int TrackedPrefabCount => _factory?.TrackedPrefabCount ?? 0;

        public int ReleaseCount => _factory?.ReleaseCount ?? 0;

        public int LiveInstanceCount => _factory?.LiveInstanceCount ?? 0;

        private void Awake()
        {
            CreateCanvasRoots(out Transform screenRoot, out Transform modalRoot);
            UiRouteViewRegistry registry = CreateRegistry();
            _navigation = new UiNavigationStack(6, 3);
            _factory = new ResourcesPrefabUiViewFactory(
                registry,
                screenRoot,
                modalRoot);
            _views = new UiViewLifecycleCoordinator(_navigation, _factory);
            _views.Changed += HandleLifecycleChanged;
            Apply("Create Home root", _navigation.PushScreen("Home"));
        }

        private void OnDestroy()
        {
            if (_views != null)
            {
                _views.Changed -= HandleLifecycleChanged;
                _views.Dispose();
            }

            _factory?.Dispose();
        }

        public UiNavigationResult OpenInventory()
        {
            return Apply("Push Inventory", _navigation.PushScreen("Inventory"));
        }

        public UiNavigationResult OpenProfile()
        {
            return Apply("Push Profile", _navigation.PushScreen("Profile"));
        }

        public UiNavigationResult ReplaceWithShop()
        {
            return Apply(
                "Replace top with Shop",
                _navigation.ReplaceTopScreen("Shop"));
        }

        public UiNavigationResult OpenSettingsModal()
        {
            return Apply(
                "Push Settings modal",
                _navigation.PushModal("Settings"));
        }

        public UiNavigationResult OpenConfirmModal()
        {
            return Apply(
                "Push Confirm modal",
                _navigation.PushModal("Confirm"));
        }

        public UiNavigationResult NavigateBack()
        {
            UiNavigationResult result = _navigation.Back(out UiRoute removed);
            return Apply(
                result == UiNavigationResult.Succeeded
                    ? $"Back removed {removed}"
                    : "Back",
                result);
        }

        private UiNavigationResult Apply(
            string action,
            UiNavigationResult result)
        {
            _status = $"{action}: {result}";
            return result;
        }

        private void HandleLifecycleChanged(UiViewLifecycleChange change)
        {
            string line =
                $"v{change.Revision}: {change.Route} -> {change.State}";
            int writeIndex = (_historyStart + _historyCount) % HistoryCapacity;
            if (_historyCount < HistoryCapacity)
            {
                _historyCount++;
            }
            else
            {
                writeIndex = _historyStart;
                _historyStart = (_historyStart + 1) % HistoryCapacity;
            }

            _history[writeIndex] = line;
        }

        private static UiRouteViewRegistry CreateRegistry()
        {
            return new UiRouteViewRegistry(new[]
            {
                Register("Home", UiLayer.Screen, "UiNavigation/Prefabs/HomeScreen"),
                Register(
                    "Inventory",
                    UiLayer.Screen,
                    "UiNavigation/Prefabs/InventoryScreen"),
                Register(
                    "Profile",
                    UiLayer.Screen,
                    "UiNavigation/Prefabs/ProfileScreen"),
                Register("Shop", UiLayer.Screen, "UiNavigation/Prefabs/ShopScreen"),
                Register(
                    "Settings",
                    UiLayer.Modal,
                    "UiNavigation/Prefabs/SettingsModal"),
                Register(
                    "Confirm",
                    UiLayer.Modal,
                    "UiNavigation/Prefabs/ConfirmModal")
            });
        }

        private static UiViewRegistration Register(
            string routeId,
            UiLayer layer,
            string assetKey)
        {
            return new UiViewRegistration(new UiRoute(routeId, layer), assetKey);
        }

        private void CreateCanvasRoots(
            out Transform screenRoot,
            out Transform modalRoot)
        {
            var canvasObject = new GameObject(
                "06.3 Prefab View Canvas",
                typeof(RectTransform),
                typeof(Canvas),
                typeof(CanvasScaler));
            canvasObject.transform.SetParent(transform, false);
            Canvas canvas = canvasObject.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = -10;

            CanvasScaler scaler = canvasObject.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.matchWidthOrHeight = 0.5f;

            screenRoot = CreateRoot("Screen Root", canvasObject.transform, 0);
            modalRoot = CreateRoot("Modal Root", canvasObject.transform, 1);
        }

        private static Transform CreateRoot(
            string name,
            Transform parent,
            int siblingIndex)
        {
            var root = new GameObject(name, typeof(RectTransform));
            var rect = (RectTransform)root.transform;
            rect.SetParent(parent, false);
            rect.anchorMin = new Vector2(0.34f, 0f);
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
            rect.SetSiblingIndex(siblingIndex);
            return rect;
        }

        private void OnGUI()
        {
            EnsureStyles();
            float scale = Mathf.Clamp(Screen.height / 1080f, 0.85f, 1.15f);
            Matrix4x4 previousMatrix = GUI.matrix;
            GUI.matrix = Matrix4x4.Scale(new Vector3(scale, scale, 1f));
            float logicalHeight = Screen.height / scale;

            GUILayout.BeginArea(
                new Rect(16f, 16f, 610f, logicalHeight - 32f),
                GUI.skin.window);
            _scroll = GUILayout.BeginScrollView(_scroll);
            GUILayout.Label("Unity Core Lab", _titleStyle);
            GUILayout.Label("06.3 Route-to-Prefab factory", _headingStyle);
            GUILayout.Label(
                "Routes resolve to real Resources prefabs. Every instance owns " +
                "a shared asset lease until its View is disposed.",
                _labelStyle);
            GUILayout.Space(8f);
            GUILayout.Label(
                $"Active screen / modal: {_views.ActiveScreenId ?? "<loading>"} / " +
                $"{_views.ActiveModalId ?? "<none>"}",
                _labelStyle);
            GUILayout.Label(
                $"Load requests: {LoadRequestCount} · tracked prefabs: " +
                $"{TrackedPrefabCount}",
                _labelStyle);
            GUILayout.Label(
                $"Live instances: {LiveInstanceCount} · lease releases: " +
                $"{ReleaseCount}",
                _labelStyle);
            GUILayout.Space(8f);

            if (GUILayout.Button("Push Inventory prefab", _buttonStyle))
            {
                OpenInventory();
            }

            if (GUILayout.Button("Push Profile prefab", _buttonStyle))
            {
                OpenProfile();
            }

            if (GUILayout.Button("Replace top with Shop prefab", _buttonStyle))
            {
                ReplaceWithShop();
            }

            if (GUILayout.Button("Open Settings modal prefab", _buttonStyle))
            {
                OpenSettingsModal();
            }

            if (GUILayout.Button("Open Confirm modal prefab", _buttonStyle))
            {
                OpenConfirmModal();
            }

            if (GUILayout.Button("Back (release lease)", _buttonStyle))
            {
                NavigateBack();
            }

            GUILayout.Space(8f);
            GUILayout.Label(_status ?? "Ready.", _labelStyle);
            GUILayout.Label("Lifecycle history", _headingStyle);
            for (int index = _historyCount - 1; index >= 0; index--)
            {
                int physicalIndex = (_historyStart + index) % HistoryCapacity;
                GUILayout.Label(_history[physicalIndex], _labelStyle);
            }

            GUILayout.EndScrollView();
            GUILayout.EndArea();
            GUI.matrix = previousMatrix;
        }

        private void EnsureStyles()
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
            _headingStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 19,
                fontStyle = FontStyle.Bold,
                wordWrap = true
            };
            _labelStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 16,
                wordWrap = true
            };
            _buttonStyle = new GUIStyle(GUI.skin.button)
            {
                fontSize = 16,
                fixedHeight = 42f,
                wordWrap = true
            };
        }
    }
}
