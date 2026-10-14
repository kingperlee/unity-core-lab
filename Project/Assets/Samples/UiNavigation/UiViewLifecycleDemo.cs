using System;
using System.Threading;
using System.Threading.Tasks;
using Cysharp.Threading.Tasks;
using UnityEngine;
using UnityEngine.UI;
using UnityCoreLab.UiNavigation;

namespace UnityCoreLab.Samples.UiNavigation
{
    /// <summary>
    /// 06.2 adapter: navigation changes create real Unity UI GameObjects through
    /// an asynchronous factory while the runtime coordinator owns cancellation.
    /// </summary>
    public sealed class UiViewLifecycleDemo : MonoBehaviour
    {
        private const int HistoryCapacity = 14;
        private readonly string[] _history = new string[HistoryCapacity];
        private int _historyStart;
        private int _historyCount;
        private UiNavigationStack _navigation;
        private UiViewLifecycleCoordinator _views;
        private DelayedUnityViewFactory _factory;
        private Transform _screenRoot;
        private Transform _modalRoot;
        private string _status;
        private Vector2 _scrollPosition;
        private GUIStyle _titleStyle;
        private GUIStyle _headingStyle;
        private GUIStyle _labelStyle;
        private GUIStyle _buttonStyle;

        public bool IsInitialized => _views != null;

        public UiNavigationStack Navigation => _navigation;

        public UiViewLifecycleCoordinator Views => _views;

        public int CreatedViewCount => _factory?.CreatedCount ?? 0;

        public int CancelledCreationCount => _factory?.CancelledCount ?? 0;

        public int DisposedViewCount => _factory?.DisposedCount ?? 0;

        private void Awake()
        {
            CreateCanvasRoots();
            _navigation = new UiNavigationStack(6, 3);
            _factory = new DelayedUnityViewFactory(
                _screenRoot,
                _modalRoot,
                HandleViewDisposed);
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
        }

        public UiNavigationResult OpenInventory()
        {
            return Apply("Push Inventory", _navigation.PushScreen("Inventory"));
        }

        public UiNavigationResult OpenProfile()
        {
            return Apply("Push Profile", _navigation.PushScreen("Profile"));
        }

        public UiNavigationResult OpenSlowDetails()
        {
            return Apply(
                "Push SlowDetails",
                _navigation.PushScreen("SlowDetails"));
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

        public void OpenSlowThenCancel()
        {
            UiNavigationResult push = OpenSlowDetails();
            UiNavigationResult back = NavigateBack();
            _status = $"Race sample: Push={push}, immediate Back={back}";
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
            if (!string.IsNullOrWhiteSpace(change.Detail))
            {
                line += $" | {change.Detail}";
            }

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

        private void HandleViewDisposed()
        {
            _factory.NotifyDisposed();
        }

        private void CreateCanvasRoots()
        {
            var canvasObject = new GameObject(
                "06.2 View Canvas",
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

            _screenRoot = CreateRoot("Screen Root", canvasObject.transform, 0);
            _modalRoot = CreateRoot("Modal Root", canvasObject.transform, 1);
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
            _scrollPosition = GUILayout.BeginScrollView(_scrollPosition);
            GUILayout.Label("Unity Core Lab", _titleStyle);
            GUILayout.Label("06.2 Async view lifecycle", _headingStyle);
            GUILayout.Label(
                "The right side contains real Unity UI GameObjects. " +
                "The control panel only drives and observes them.",
                _labelStyle);
            GUILayout.Space(10f);

            GUILayout.Label(
                $"Active screen: {_views.ActiveScreenId ?? "<loading>"}",
                _labelStyle);
            GUILayout.Label(
                $"Active modal: {_views.ActiveModalId ?? "<none>"}",
                _labelStyle);
            GUILayout.Label(
                $"Created / disposed: {CreatedViewCount} / {DisposedViewCount}",
                _labelStyle);
            GUILayout.Label(
                $"Cancelled factory requests: {CancelledCreationCount}",
                _labelStyle);

            GUILayout.Space(10f);
            if (GUILayout.Button("Push Inventory screen", _buttonStyle))
            {
                OpenInventory();
            }

            if (GUILayout.Button("Push Profile screen", _buttonStyle))
            {
                OpenProfile();
            }

            if (GUILayout.Button("Replace top with Shop", _buttonStyle))
            {
                ReplaceWithShop();
            }

            if (GUILayout.Button("Open Settings modal", _buttonStyle))
            {
                OpenSettingsModal();
            }

            if (GUILayout.Button("Open Confirm modal", _buttonStyle))
            {
                OpenConfirmModal();
            }

            if (GUILayout.Button("Back (modal first)", _buttonStyle))
            {
                NavigateBack();
            }

            if (GUILayout.Button(
                    "Push SlowDetails then immediately Back",
                    _buttonStyle))
            {
                OpenSlowThenCancel();
            }

            GUILayout.Space(8f);
            GUILayout.Label(_status ?? "Ready.", _labelStyle);
            GUILayout.Space(10f);
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

        private sealed class DelayedUnityViewFactory : IAsyncUiViewFactory
        {
            private readonly Transform _screenRoot;
            private readonly Transform _modalRoot;
            private readonly Action _onDisposed;

            public DelayedUnityViewFactory(
                Transform screenRoot,
                Transform modalRoot,
                Action onDisposed)
            {
                _screenRoot = screenRoot;
                _modalRoot = modalRoot;
                _onDisposed = onDisposed;
            }

            public int CreatedCount { get; private set; }

            public int CancelledCount { get; private set; }

            public int DisposedCount { get; private set; }

            public async Task<IUiView> CreateAsync(
                UiRoute route,
                CancellationToken cancellationToken)
            {
                try
                {
                    int delayMilliseconds = route.Id == "Home"
                        ? 1
                        : route.Id == "SlowDetails"
                            ? 900
                            : 280;
                    await UniTask.Delay(
                        delayMilliseconds,
                        DelayType.Realtime,
                        PlayerLoopTiming.Update,
                        cancellationToken);
                    await UniTask.SwitchToMainThread(
                        PlayerLoopTiming.Update,
                        cancellationToken);

                    Transform parent = route.Layer == UiLayer.Screen
                        ? _screenRoot
                        : _modalRoot;
                    CreatedCount++;
                    return new UnityPanelView(route, parent, _onDisposed);
                }
                catch (OperationCanceledException)
                {
                    CancelledCount++;
                    throw;
                }
            }

            public void NotifyDisposed()
            {
                DisposedCount++;
            }
        }

        private sealed class UnityPanelView : IUiView
        {
            private readonly GameObject _root;
            private readonly Action _onDisposed;
            private bool _disposed;

            public UnityPanelView(
                UiRoute route,
                Transform parent,
                Action onDisposed)
            {
                Route = route;
                _onDisposed = onDisposed;
                _root = CreatePanel(route, parent);
                _root.SetActive(false);
            }

            public UiRoute Route { get; }

            public void Enter()
            {
                SetVisible(true);
            }

            public void Cover()
            {
                SetVisible(false);
            }

            public void Resume()
            {
                SetVisible(true);
            }

            public void Exit()
            {
                SetVisible(false);
            }

            public void Dispose()
            {
                if (_disposed)
                {
                    return;
                }

                _disposed = true;
                if (_root != null)
                {
                    UnityEngine.Object.Destroy(_root);
                }

                _onDisposed?.Invoke();
            }

            private void SetVisible(bool visible)
            {
                if (!_disposed && _root != null)
                {
                    _root.SetActive(visible);
                }
            }

            private static GameObject CreatePanel(UiRoute route, Transform parent)
            {
                var panel = new GameObject(
                    $"{route.Id} {route.Layer} View",
                    typeof(RectTransform),
                    typeof(CanvasRenderer),
                    typeof(Image));
                var rect = (RectTransform)panel.transform;
                rect.SetParent(parent, false);
                if (route.Layer == UiLayer.Screen)
                {
                    rect.anchorMin = Vector2.zero;
                    rect.anchorMax = Vector2.one;
                }
                else
                {
                    rect.anchorMin = new Vector2(0.14f, 0.28f);
                    rect.anchorMax = new Vector2(0.86f, 0.72f);
                }

                rect.offsetMin = Vector2.zero;
                rect.offsetMax = Vector2.zero;
                Image image = panel.GetComponent<Image>();
                image.color = ColorFor(route);
                image.raycastTarget = false;

                var textObject = new GameObject(
                    "Title",
                    typeof(RectTransform),
                    typeof(CanvasRenderer),
                    typeof(Text));
                var textRect = (RectTransform)textObject.transform;
                textRect.SetParent(rect, false);
                textRect.anchorMin = new Vector2(0.08f, 0.18f);
                textRect.anchorMax = new Vector2(0.92f, 0.82f);
                textRect.offsetMin = Vector2.zero;
                textRect.offsetMax = Vector2.zero;
                Text text = textObject.GetComponent<Text>();
                text.font = Resources.GetBuiltinResource<Font>(
                    "LegacyRuntime.ttf");
                text.fontSize = route.Layer == UiLayer.Screen ? 52 : 40;
                text.fontStyle = FontStyle.Bold;
                text.alignment = TextAnchor.MiddleCenter;
                text.color = Color.white;
                text.raycastTarget = false;
                text.text =
                    $"{route.Layer.ToString().ToUpperInvariant()}\n{route.Id}\n\n" +
                    "created asynchronously · lifecycle owned";
                return panel;
            }

            private static Color ColorFor(UiRoute route)
            {
                int hash = route.Id.GetHashCode() & 0x7fffffff;
                float hue = (hash % 360) / 360f;
                Color color = Color.HSVToRGB(hue, 0.58f, 0.72f);
                color.a = route.Layer == UiLayer.Screen ? 1f : 0.96f;
                return color;
            }
        }
    }
}
