using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using UnityCoreLab.UiDialogs;

namespace UnityCoreLab.Samples.UiDialogs
{
    /// <summary>
    /// 06.5 integration demo for queued, awaitable dialog ownership.
    /// </summary>
    public sealed class UiDialogDemo : MonoBehaviour
    {
        private UiDialogQueue _queue;
        private DialogDemoView _view;
        private Task _lastCompletion = Task.CompletedTask;
        private string _status = "Ready.";
        private int _underlayClickCount;
        private bool _lastDuplicateCoalesced;
        private GUIStyle _titleStyle;
        private GUIStyle _headingStyle;
        private GUIStyle _labelStyle;
        private GUIStyle _buttonStyle;

        public UiDialogQueue Queue => _queue;

        public DialogDemoView View => _view;

        public int UnderlayClickCount => _underlayClickCount;

        public bool LastDuplicateCoalesced => _lastDuplicateCoalesced;

        public string Status => _status;

        private void Awake()
        {
            _view = BuildView();
            _queue = new UiDialogQueue(_view);
        }

        private void OnDestroy()
        {
            _queue?.Dispose();
            _queue = null;
        }

        public void ShowNotice()
        {
            _lastCompletion = ObserveResultAsync(
                _queue.ShowNoticeAsync(
                    "notice",
                    "Notice",
                    "A notice has one acknowledgement path."),
                "Notice");
        }

        public void ShowConfirmation()
        {
            _lastCompletion = ObserveConfirmationAsync(
                _queue.ShowConfirmAsync(
                    "delete-confirmation",
                    "Delete local save?",
                    "The caller awaits a bool while the View only emits intent."));
        }

        public void ShowBlocking()
        {
            _lastCompletion = ObserveResultAsync(
                _queue.ShowBlockingAsync(
                    "blocking-operation",
                    "Synchronizing",
                    "Input is blocked until the operation owner completes it."),
                "Blocking operation");
        }

        public void CompleteBlocking()
        {
            bool completed = _queue.Complete(
                "blocking-operation",
                UiDialogResult.Completed);
            _status = completed
                ? "The operation owner completed the blocking dialog."
                : "No blocking-operation request is active or queued.";
        }

        public void ShowDuplicateNoticeTwice()
        {
            Task<UiDialogResult> first = _queue.ShowNoticeAsync(
                "duplicate-notice",
                "Duplicate suppression",
                "Two callers share this one visible request.");
            Task<UiDialogResult> second = _queue.ShowNoticeAsync(
                "duplicate-notice",
                "This replacement text is intentionally ignored",
                "The first owner keeps the presentation.");
            _lastDuplicateCoalesced = ReferenceEquals(first, second);
            _lastCompletion = ObserveResultAsync(first, "Coalesced notice");
            _status = _lastDuplicateCoalesced
                ? "Duplicate IDs returned the same completion task."
                : "Unexpected: duplicate tasks were not coalesced.";
        }

        public void RunPriorityQueueScenario()
        {
            if (_queue.ActiveRequest != null)
            {
                _status = "Resolve the current dialog before starting the scenario.";
                return;
            }

            _queue.ShowBlockingAsync(
                "priority-gate",
                "Queue gate",
                "This active request is not preempted by later priority.",
                UiDialogPriority.Normal);
            _queue.ShowNoticeAsync(
                "priority-normal",
                "Normal pending request",
                "This was queued first but has the lowest priority.",
                UiDialogPriority.Normal);
            _queue.ShowNoticeAsync(
                "priority-critical",
                "Critical pending request",
                "This becomes active first after the current owner completes.",
                UiDialogPriority.Critical);
            _queue.ShowNoticeAsync(
                "priority-high",
                "High pending request",
                "This runs after Critical and before Normal.",
                UiDialogPriority.High);
            _status = "Queued order after the active gate: Critical, High, Normal.";
        }

        public void AdvancePriorityQueueScenario()
        {
            UiDialogRequest active = _queue.ActiveRequest;
            if (active == null)
            {
                _status = "There is no active dialog to advance.";
                return;
            }

            if (active.Kind == UiDialogKind.Blocking)
            {
                _queue.Complete(active.Id, UiDialogResult.Completed);
            }
            else
            {
                _view.PressPrimary();
            }

            _status = "Resolved the active owner; the next queued request advanced.";
        }

        public void PressPrimary()
        {
            _view.PressPrimary();
        }

        public void PressSecondary()
        {
            _view.PressSecondary();
        }

        public void InvokeUnderlayAction()
        {
            _underlayClickCount++;
            _status = $"Underlying UI received click #{_underlayClickCount}.";
        }

        private async Task ObserveConfirmationAsync(Task<bool> confirmation)
        {
            bool confirmed = await confirmation;
            _status = confirmed
                ? "Awaited confirmation result: true."
                : "Awaited confirmation result: false.";
        }

        private async Task ObserveResultAsync(
            Task<UiDialogResult> completion,
            string label)
        {
            UiDialogResult result = await completion;
            _status = $"{label} completed with {result}.";
        }

        private DialogDemoView BuildView()
        {
            Font font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            var canvasObject = new GameObject(
                "06.5 Dialog Canvas",
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

            var backgroundObject = CreateImage(
                "Underlying Application UI",
                canvasObject.transform,
                Vector2.zero,
                Vector2.one,
                new Color(0.055f, 0.075f, 0.11f));
            Text backgroundTitle = CreateText(
                "Background Title",
                backgroundObject.transform,
                font,
                new Vector2(0.40f, 0.72f),
                new Vector2(0.93f, 0.86f),
                34,
                FontStyle.Bold,
                TextAnchor.MiddleCenter);
            backgroundTitle.text = "Underlying application UI";
            Text clickCount = CreateText(
                "Underlay Count",
                backgroundObject.transform,
                font,
                new Vector2(0.40f, 0.57f),
                new Vector2(0.93f, 0.69f),
                22,
                FontStyle.Normal,
                TextAnchor.MiddleCenter);
            clickCount.text = "Click the button before and during a dialog";
            Button underlayButton = CreateButton(
                "Underlying Action",
                backgroundObject.transform,
                font,
                new Vector2(0.53f, 0.43f),
                new Vector2(0.80f, 0.54f),
                "Underlying action",
                out _);
            underlayButton.onClick.AddListener(() =>
            {
                InvokeUnderlayAction();
                clickCount.text =
                    $"Underlying click count: {_underlayClickCount}";
            });

            var overlayObject = CreateImage(
                "Dialog Input Barrier",
                canvasObject.transform,
                Vector2.zero,
                Vector2.one,
                new Color(0f, 0f, 0f, 0.68f));
            var canvasGroup = overlayObject.AddComponent<CanvasGroup>();
            var view = overlayObject.AddComponent<DialogDemoView>();

            var panelObject = CreateImage(
                "Dialog Panel",
                overlayObject.transform,
                new Vector2(0.43f, 0.27f),
                new Vector2(0.90f, 0.75f),
                new Color(0.10f, 0.13f, 0.19f, 1f));
            Text kind = CreateText(
                "Kind",
                panelObject.transform,
                font,
                new Vector2(0.07f, 0.78f),
                new Vector2(0.93f, 0.91f),
                17,
                FontStyle.Normal,
                TextAnchor.MiddleLeft);
            kind.color = new Color(0.40f, 0.78f, 1f);
            Text title = CreateText(
                "Title",
                panelObject.transform,
                font,
                new Vector2(0.07f, 0.60f),
                new Vector2(0.93f, 0.79f),
                31,
                FontStyle.Bold,
                TextAnchor.MiddleLeft);
            Text message = CreateText(
                "Message",
                panelObject.transform,
                font,
                new Vector2(0.07f, 0.28f),
                new Vector2(0.93f, 0.61f),
                21,
                FontStyle.Normal,
                TextAnchor.UpperLeft);
            Button primary = CreateButton(
                "Primary",
                panelObject.transform,
                font,
                new Vector2(0.52f, 0.08f),
                new Vector2(0.91f, 0.24f),
                "Confirm",
                out Text primaryLabel);
            Button secondary = CreateButton(
                "Secondary",
                panelObject.transform,
                font,
                new Vector2(0.09f, 0.08f),
                new Vector2(0.48f, 0.24f),
                "Cancel",
                out Text secondaryLabel);

            view.Configure(
                canvasGroup,
                title,
                message,
                kind,
                primary,
                primaryLabel,
                secondary,
                secondaryLabel);

            if (FindObjectOfType<EventSystem>() == null)
            {
                var eventSystem = new GameObject(
                    "06.5 Event System",
                    typeof(EventSystem),
                    typeof(StandaloneInputModule));
                eventSystem.transform.SetParent(transform, false);
            }

            return view;
        }

        private static GameObject CreateImage(
            string name,
            Transform parent,
            Vector2 anchorMin,
            Vector2 anchorMax,
            Color color)
        {
            var imageObject = new GameObject(
                name,
                typeof(RectTransform),
                typeof(CanvasRenderer),
                typeof(Image));
            var rect = (RectTransform)imageObject.transform;
            rect.SetParent(parent, false);
            rect.anchorMin = anchorMin;
            rect.anchorMax = anchorMax;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
            imageObject.GetComponent<Image>().color = color;
            return imageObject;
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
            string label,
            out Text labelText)
        {
            GameObject buttonObject = CreateImage(
                name,
                parent,
                anchorMin,
                anchorMax,
                new Color(0.12f, 0.42f, 0.70f));
            Button button = buttonObject.AddComponent<Button>();
            button.targetGraphic = buttonObject.GetComponent<Image>();
            labelText = CreateText(
                "Label",
                buttonObject.transform,
                font,
                Vector2.zero,
                Vector2.one,
                19,
                FontStyle.Bold,
                TextAnchor.MiddleCenter);
            labelText.text = label;
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
            GUILayout.Label("06.5 Awaitable dialog policy", _headingStyle);
            GUILayout.Label(
                "One active owner, a stable priority queue, duplicate " +
                "coalescing, and a full-screen uGUI input barrier.",
                _labelStyle);
            GUILayout.Space(8f);
            GUILayout.Label(
                $"Active: {_queue.ActiveRequest?.Id ?? "none"} · " +
                $"pending: {_queue.PendingCount} · " +
                $"blocks input: {_queue.IsInputBlocked}",
                _labelStyle);
            GUILayout.Label(
                $"View shows: {_view.ShowCount} · hides: {_view.HideCount} · " +
                $"underlay clicks: {_underlayClickCount}",
                _labelStyle);
            GUILayout.Space(8f);

            if (GUILayout.Button("Show notice", _buttonStyle)) ShowNotice();
            if (GUILayout.Button("Await confirmation", _buttonStyle))
                ShowConfirmation();
            if (GUILayout.Button("Show blocking operation", _buttonStyle))
                ShowBlocking();
            if (GUILayout.Button("Complete blocking operation", _buttonStyle))
                CompleteBlocking();
            if (GUILayout.Button("Show duplicate ID twice", _buttonStyle))
                ShowDuplicateNoticeTwice();
            if (GUILayout.Button("Build priority queue scenario", _buttonStyle))
                RunPriorityQueueScenario();
            if (GUILayout.Button("Advance priority scenario", _buttonStyle))
                AdvancePriorityQueueScenario();

            GUILayout.Space(8f);
            GUILayout.Label(_status, _labelStyle);
            GUILayout.Label(
                _lastCompletion.IsCompleted
                    ? "Last observed completion: completed"
                    : "Last observed completion: awaiting user/owner",
                _labelStyle);
            GUILayout.Label(
                "The IMGUI control surface stays operable for the demo. " +
                "The dialog barrier blocks the underlying uGUI button.",
                _labelStyle);
            GUILayout.EndArea();
            GUI.matrix = previousMatrix;
        }

        private void EnsureStyles()
        {
            if (_titleStyle != null) return;
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
                fixedHeight = 40f,
                wordWrap = true
            };
        }
    }
}
