using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using UnityCoreLab.UiPresentation;

namespace UnityCoreLab.Samples.UiPresentation
{
    /// <summary>
    /// 06.4 integration demo for passive View + testable Presenter ownership.
    /// </summary>
    public sealed class UiPresenterDemo : MonoBehaviour
    {
        private SimulatedCatalogSource _source;
        private UiContentPresenter<CatalogItem> _presenter;
        private CatalogPresenterView _view;
        private Task _lastRefresh = Task.CompletedTask;
        private string _status;
        private GUIStyle _titleStyle;
        private GUIStyle _headingStyle;
        private GUIStyle _labelStyle;
        private GUIStyle _buttonStyle;

        public CatalogPresenterView View => _view;

        public bool HasPresenter => _presenter != null;

        public bool IsLoading => _presenter?.IsLoading ?? false;

        public int RequestCount => _source?.RequestCount ?? 0;

        public int CancellationCount => _source?.CancellationCount ?? 0;

        private void Awake()
        {
            _source = new SimulatedCatalogSource();
            _view = BuildView();
            CreatePresenter();
            LoadContent();
        }

        private void OnDestroy()
        {
            _presenter?.Dispose();
            _presenter = null;
        }

        public void LoadContent()
        {
            Refresh(CatalogLoadScenario.Content);
        }

        public void LoadEmpty()
        {
            Refresh(CatalogLoadScenario.Empty);
        }

        public void LoadError()
        {
            Refresh(CatalogLoadScenario.Error);
        }

        public void StartSlowRequest()
        {
            Refresh(CatalogLoadScenario.SlowContent);
        }

        public void DisposePresenter()
        {
            if (_presenter == null)
            {
                return;
            }

            _presenter.Dispose();
            _presenter = null;
            _view.SetInteractive(false);
            _status = "Presenter disposed: active request cancelled; late result ignored.";
        }

        public void RecreatePresenter()
        {
            if (_presenter != null)
            {
                return;
            }

            CreatePresenter();
            LoadContent();
        }

        private void CreatePresenter()
        {
            _presenter = new UiContentPresenter<CatalogItem>(_view, _source);
            _view.SetInteractive(true);
            _status = "Presenter created.";
        }

        private void Refresh(CatalogLoadScenario scenario)
        {
            if (_presenter == null)
            {
                _status = "Presenter is disposed. Recreate it before refreshing.";
                return;
            }

            _source.Scenario = scenario;
            _lastRefresh = _presenter.RefreshAsync();
            _status = $"Started {scenario}.";
        }

        private CatalogPresenterView BuildView()
        {
            Font font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            var canvasObject = new GameObject(
                "06.4 Presenter Canvas",
                typeof(RectTransform),
                typeof(Canvas),
                typeof(CanvasScaler),
                typeof(GraphicRaycaster));
            canvasObject.transform.SetParent(transform, false);
            Canvas canvas = canvasObject.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;

            CanvasScaler scaler = canvasObject.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.matchWidthOrHeight = 0.5f;

            var panelObject = new GameObject(
                "Catalog Presenter View",
                typeof(RectTransform),
                typeof(CanvasRenderer),
                typeof(Image),
                typeof(CanvasGroup),
                typeof(CatalogPresenterView));
            var panel = (RectTransform)panelObject.transform;
            panel.SetParent(canvasObject.transform, false);
            panel.anchorMin = new Vector2(0.36f, 0.07f);
            panel.anchorMax = new Vector2(0.96f, 0.93f);
            panel.offsetMin = Vector2.zero;
            panel.offsetMax = Vector2.zero;
            panelObject.GetComponent<Image>().color =
                new Color(0.075f, 0.095f, 0.13f, 0.98f);

            Text title = CreateText(
                "Title",
                panel,
                font,
                new Vector2(0.06f, 0.84f),
                new Vector2(0.94f, 0.96f),
                34,
                FontStyle.Bold,
                TextAnchor.MiddleLeft);
            title.text = "Presenter-driven catalog";

            Text state = CreateText(
                "State",
                panel,
                font,
                new Vector2(0.06f, 0.72f),
                new Vector2(0.94f, 0.83f),
                22,
                FontStyle.Bold,
                TextAnchor.MiddleLeft);

            Text content = CreateText(
                "Content",
                panel,
                font,
                new Vector2(0.06f, 0.19f),
                new Vector2(0.94f, 0.70f),
                20,
                FontStyle.Normal,
                TextAnchor.UpperLeft);

            Button refresh = CreateButton(
                "Refresh Button",
                panel,
                font,
                new Vector2(0.06f, 0.055f),
                new Vector2(0.44f, 0.15f),
                "Refresh current scenario");

            var view = panelObject.GetComponent<CatalogPresenterView>();
            view.Configure(
                panelObject.GetComponent<CanvasGroup>(),
                state,
                content,
                refresh);

            if (FindObjectOfType<EventSystem>() == null)
            {
                var eventSystem = new GameObject(
                    "06.4 Event System",
                    typeof(EventSystem),
                    typeof(StandaloneInputModule));
                eventSystem.transform.SetParent(transform, false);
            }

            return view;
        }

        private static Text CreateText(
            string name,
            Transform parent,
            Font font,
            Vector2 anchorMin,
            Vector2 anchorMax,
            int fontSize,
            FontStyle fontStyle,
            TextAnchor alignment)
        {
            var textObject = new GameObject(
                name,
                typeof(RectTransform),
                typeof(CanvasRenderer),
                typeof(Text));
            var rect = (RectTransform)textObject.transform;
            rect.SetParent(parent, false);
            rect.anchorMin = anchorMin;
            rect.anchorMax = anchorMax;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;

            Text text = textObject.GetComponent<Text>();
            text.font = font;
            text.fontSize = fontSize;
            text.fontStyle = fontStyle;
            text.alignment = alignment;
            text.color = new Color(0.92f, 0.95f, 1f);
            text.horizontalOverflow = HorizontalWrapMode.Wrap;
            text.verticalOverflow = VerticalWrapMode.Truncate;
            return text;
        }

        private static Button CreateButton(
            string name,
            Transform parent,
            Font font,
            Vector2 anchorMin,
            Vector2 anchorMax,
            string label)
        {
            var buttonObject = new GameObject(
                name,
                typeof(RectTransform),
                typeof(CanvasRenderer),
                typeof(Image),
                typeof(Button));
            var rect = (RectTransform)buttonObject.transform;
            rect.SetParent(parent, false);
            rect.anchorMin = anchorMin;
            rect.anchorMax = anchorMax;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;

            Image image = buttonObject.GetComponent<Image>();
            image.color = new Color(0.12f, 0.42f, 0.7f);
            Button button = buttonObject.GetComponent<Button>();
            button.targetGraphic = image;

            Text text = CreateText(
                "Label",
                rect,
                font,
                Vector2.zero,
                Vector2.one,
                18,
                FontStyle.Bold,
                TextAnchor.MiddleCenter);
            text.text = label;
            return button;
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
            GUILayout.Label("Unity Core Lab", _titleStyle);
            GUILayout.Label("06.4 Presenter and UI state binding", _headingStyle);
            GUILayout.Label(
                "The View emits intent and renders snapshots. The Presenter owns " +
                "loading, cancellation, stale-result suppression and state mapping.",
                _labelStyle);
            GUILayout.Space(8f);
            GUILayout.Label(
                $"Presenter: {(HasPresenter ? "active" : "disposed")} · " +
                $"in flight: {IsLoading}",
                _labelStyle);
            GUILayout.Label(
                $"Requests: {RequestCount} · cancellations: {CancellationCount} · " +
                $"renders: {_view.RenderCount}",
                _labelStyle);
            GUILayout.Label(
                $"View state: {_view.CurrentState} · revision: " +
                $"{_view.CurrentRevision} · items: {_view.ContentCount}",
                _labelStyle);
            GUILayout.Space(8f);

            if (GUILayout.Button("Load content", _buttonStyle))
            {
                LoadContent();
            }

            if (GUILayout.Button("Load empty result", _buttonStyle))
            {
                LoadEmpty();
            }

            if (GUILayout.Button("Load simulated error", _buttonStyle))
            {
                LoadError();
            }

            if (GUILayout.Button("Start slow request", _buttonStyle))
            {
                StartSlowRequest();
            }

            if (GUILayout.Button(
                    "Supersede current request with content",
                    _buttonStyle))
            {
                LoadContent();
            }

            if (GUILayout.Button("Dispose Presenter", _buttonStyle))
            {
                DisposePresenter();
            }

            if (GUILayout.Button("Recreate Presenter", _buttonStyle))
            {
                RecreatePresenter();
            }

            GUILayout.Space(8f);
            GUILayout.Label(_status, _labelStyle);
            GUILayout.Label(
                _lastRefresh.IsCompleted
                    ? "Last refresh task: completed"
                    : "Last refresh task: running",
                _labelStyle);
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
