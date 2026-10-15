using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using UnityCoreLab.UiVirtualization;

namespace UnityCoreLab.Samples.UiVirtualization
{
    /// <summary>
    /// 06.6 fixed-height virtualized-list integration demo.
    /// </summary>
    public sealed class UiVirtualizedListDemo : MonoBehaviour
    {
        private const int DefaultItemCount = 1000;
        private const float ItemExtent = 72f;
        private const float RowHeight = 64f;
        private const int BufferItems = 2;

        private readonly List<VirtualizedListRow> _rows =
            new List<VirtualizedListRow>();

        private FixedHeightVirtualList _layout;
        private RectTransform _content;
        private RectTransform _viewport;
        private ScrollRect _scrollRect;
        private Text _metricsText;
        private bool _ignoreScrollEvent;
        private string _status;
        private GUIStyle _titleStyle;
        private GUIStyle _headingStyle;
        private GUIStyle _labelStyle;
        private GUIStyle _buttonStyle;

        public int ItemCount => _layout?.ItemCount ?? 0;

        public int CreatedRowCount => _rows.Count;

        public int ActiveRowCount { get; private set; }

        public int TotalBindOperations { get; private set; }

        public float ScrollOffset => _layout?.ScrollOffset ?? 0f;

        public float ContentExtent => _layout?.ContentExtent ?? 0f;

        public VirtualizedListRange CurrentRange =>
            _layout?.VisibleRange ?? new VirtualizedListRange(0, 0);

        public IReadOnlyList<VirtualizedListRow> Rows => _rows;

        private void Awake()
        {
            BuildInterface();
            Canvas.ForceUpdateCanvases();
            float viewportExtent = Mathf.Max(1f, _viewport.rect.height);
            _layout = new FixedHeightVirtualList(
                ItemExtent,
                viewportExtent,
                BufferItems);
            SetItemCount(DefaultItemCount);
        }

        private void OnDestroy()
        {
            if (_scrollRect != null)
            {
                _scrollRect.onValueChanged.RemoveListener(HandleScrollChanged);
            }
        }

        public void SetItemCount(int itemCount)
        {
            _layout.SetItemCount(itemCount);
            EnsureRowPool();
            UpdateContentExtent();
            ApplyModelOffset();
            _status = $"Dataset changed to {itemCount:N0} logical items.";
        }

        public void JumpToIndex(
            int index,
            VirtualizedListAlignment alignment =
                VirtualizedListAlignment.Center)
        {
            _layout.ScrollToIndex(index, alignment);
            ApplyModelOffset();
            _status = $"Jumped to item {index + 1:N0} ({alignment}).";
        }

        public void ScrollBy(float delta)
        {
            _layout.SetScrollOffset(_layout.ScrollOffset + delta);
            ApplyModelOffset();
            _status = $"Scrolled to offset {_layout.ScrollOffset:0}.";
        }

        public bool ActiveRowsMatchCurrentRange()
        {
            VirtualizedListRange range = CurrentRange;
            int active = 0;
            for (int index = 0; index < _rows.Count; index++)
            {
                VirtualizedListRow row = _rows[index];
                if (!row.gameObject.activeSelf)
                {
                    continue;
                }

                active++;
                if (!range.Contains(row.BoundIndex))
                {
                    return false;
                }
            }

            return active == range.Count;
        }

        private void BuildInterface()
        {
            Font font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            var canvasObject = new GameObject(
                "06.6 Virtualized List Canvas",
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
                "Virtualized List View",
                typeof(RectTransform),
                typeof(CanvasRenderer),
                typeof(Image));
            var panel = (RectTransform)panelObject.transform;
            panel.SetParent(canvasObject.transform, false);
            panel.anchorMin = new Vector2(0.34f, 0.055f);
            panel.anchorMax = new Vector2(0.97f, 0.945f);
            panel.offsetMin = Vector2.zero;
            panel.offsetMax = Vector2.zero;
            panelObject.GetComponent<Image>().color =
                new Color(0.055f, 0.07f, 0.1f, 0.99f);

            Text title = CreateText(
                "Title",
                panel,
                font,
                new Vector2(0.035f, 0.895f),
                new Vector2(0.965f, 0.98f),
                32,
                FontStyle.Bold,
                TextAnchor.MiddleLeft);
            title.text = "Fixed-height virtualized list";

            _metricsText = CreateText(
                "Metrics",
                panel,
                font,
                new Vector2(0.035f, 0.825f),
                new Vector2(0.965f, 0.90f),
                18,
                FontStyle.Normal,
                TextAnchor.MiddleLeft);

            var scrollObject = new GameObject(
                "Scroll View",
                typeof(RectTransform),
                typeof(CanvasRenderer),
                typeof(Image),
                typeof(ScrollRect));
            var scrollRectTransform = (RectTransform)scrollObject.transform;
            scrollRectTransform.SetParent(panel, false);
            scrollRectTransform.anchorMin = new Vector2(0.035f, 0.035f);
            scrollRectTransform.anchorMax = new Vector2(0.965f, 0.815f);
            scrollRectTransform.offsetMin = Vector2.zero;
            scrollRectTransform.offsetMax = Vector2.zero;
            scrollObject.GetComponent<Image>().color =
                new Color(0.035f, 0.045f, 0.065f, 1f);

            var viewportObject = new GameObject(
                "Viewport",
                typeof(RectTransform),
                typeof(CanvasRenderer),
                typeof(Image),
                typeof(RectMask2D));
            _viewport = (RectTransform)viewportObject.transform;
            _viewport.SetParent(scrollRectTransform, false);
            _viewport.anchorMin = Vector2.zero;
            _viewport.anchorMax = Vector2.one;
            _viewport.offsetMin = new Vector2(8f, 8f);
            _viewport.offsetMax = new Vector2(-8f, -8f);
            viewportObject.GetComponent<Image>().color =
                new Color(0.04f, 0.055f, 0.08f, 1f);

            var contentObject = new GameObject(
                "Virtual Content",
                typeof(RectTransform));
            _content = (RectTransform)contentObject.transform;
            _content.SetParent(_viewport, false);
            _content.anchorMin = new Vector2(0f, 1f);
            _content.anchorMax = new Vector2(1f, 1f);
            _content.pivot = new Vector2(0.5f, 1f);
            _content.anchoredPosition = Vector2.zero;
            _content.sizeDelta = Vector2.zero;

            _scrollRect = scrollObject.GetComponent<ScrollRect>();
            _scrollRect.content = _content;
            _scrollRect.viewport = _viewport;
            _scrollRect.horizontal = false;
            _scrollRect.vertical = true;
            _scrollRect.inertia = true;
            _scrollRect.decelerationRate = 0.12f;
            _scrollRect.scrollSensitivity = ItemExtent;
            _scrollRect.movementType = ScrollRect.MovementType.Clamped;
            _scrollRect.onValueChanged.AddListener(HandleScrollChanged);

            if (FindObjectOfType<EventSystem>() == null)
            {
                var eventSystem = new GameObject(
                    "06.6 Event System",
                    typeof(EventSystem),
                    typeof(StandaloneInputModule));
                eventSystem.transform.SetParent(transform, false);
            }
        }

        private void EnsureRowPool()
        {
            int required = _layout.RequiredViewCount;
            Font font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            while (_rows.Count < required)
            {
                int slot = _rows.Count;
                var rowObject = new GameObject(
                    $"Pooled Row Slot {slot}",
                    typeof(RectTransform),
                    typeof(CanvasRenderer),
                    typeof(Image),
                    typeof(VirtualizedListRow));
                var rect = (RectTransform)rowObject.transform;
                rect.SetParent(_content, false);
                rect.anchorMin = new Vector2(0f, 1f);
                rect.anchorMax = new Vector2(1f, 1f);
                rect.pivot = new Vector2(0.5f, 1f);
                rect.sizeDelta = new Vector2(-12f, RowHeight);

                Text label = CreateText(
                    "Row Label",
                    rect,
                    font,
                    Vector2.zero,
                    Vector2.one,
                    18,
                    FontStyle.Normal,
                    TextAnchor.MiddleLeft);
                RectTransform labelRect = label.rectTransform;
                labelRect.offsetMin = new Vector2(18f, 4f);
                labelRect.offsetMax = new Vector2(-18f, -4f);
                label.color = new Color(0.9f, 0.94f, 1f);

                var row = rowObject.GetComponent<VirtualizedListRow>();
                row.Configure(rowObject.GetComponent<Image>(), label);
                _rows.Add(row);
            }
        }

        private void UpdateContentExtent()
        {
            float height = Mathf.Max(
                _layout.ContentExtent,
                _layout.ViewportExtent);
            _content.sizeDelta = new Vector2(0f, height);
        }

        private void ApplyModelOffset()
        {
            _ignoreScrollEvent = true;
            Vector2 position = _content.anchoredPosition;
            position.x = 0f;
            position.y = _layout.ScrollOffset;
            _content.anchoredPosition = position;
            _ignoreScrollEvent = false;
            RefreshRows();
        }

        private void HandleScrollChanged(Vector2 normalizedPosition)
        {
            if (_ignoreScrollEvent || _layout == null)
            {
                return;
            }

            float requested = Mathf.Max(0f, _content.anchoredPosition.y);
            _layout.SetScrollOffset(requested);
            if (!Mathf.Approximately(requested, _layout.ScrollOffset))
            {
                ApplyModelOffset();
                return;
            }

            RefreshRows();
        }

        private void RefreshRows()
        {
            VirtualizedListRange range = _layout.VisibleRange;
            ActiveRowCount = range.Count;
            for (int slot = 0; slot < _rows.Count; slot++)
            {
                VirtualizedListRow row = _rows[slot];
                int itemIndex = range.StartIndex + slot;
                if (itemIndex >= range.EndExclusive)
                {
                    row.Hide();
                    continue;
                }

                RectTransform rect = (RectTransform)row.transform;
                rect.anchoredPosition = new Vector2(
                    0f,
                    -_layout.GetItemOffset(itemIndex));
                if (row.BoundIndex != itemIndex)
                {
                    row.Bind(itemIndex);
                    TotalBindOperations++;
                }

                row.gameObject.SetActive(true);
            }

            if (_metricsText != null)
            {
                _metricsText.text =
                    $"{_layout.ItemCount:N0} data items  ·  " +
                    $"{_rows.Count} row objects  ·  visible {range}";
            }
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

        private void OnGUI()
        {
            if (_layout == null)
            {
                return;
            }

            EnsureStyles();
            float scale = Mathf.Clamp(Screen.height / 1080f, 0.85f, 1.15f);
            Matrix4x4 previousMatrix = GUI.matrix;
            GUI.matrix = Matrix4x4.Scale(new Vector3(scale, scale, 1f));
            float logicalHeight = Screen.height / scale;

            GUILayout.BeginArea(
                new Rect(16f, 16f, 600f, logicalHeight - 32f),
                GUI.skin.window);
            GUILayout.Label("Unity Core Lab", _titleStyle);
            GUILayout.Label("06.6 Virtualized list window + row reuse", _headingStyle);
            GUILayout.Label(
                "Only the visible range plus a small buffer owns row GameObjects. " +
                "Scrolling rebinds those rows to different data indices.",
                _labelStyle);
            GUILayout.Space(8f);
            GUILayout.Label(
                $"Logical items: {_layout.ItemCount:N0} · content: " +
                $"{_layout.ContentExtent:N0}px",
                _labelStyle);
            GUILayout.Label(
                $"Created rows: {_rows.Count} · active: {ActiveRowCount} · " +
                $"bindings: {TotalBindOperations}",
                _labelStyle);
            GUILayout.Label(
                $"Offset: {_layout.ScrollOffset:0}/{_layout.MaxScrollOffset:0} · " +
                $"range: {_layout.VisibleRange}",
                _labelStyle);
            GUILayout.Space(8f);

            GUILayout.BeginHorizontal();
            if (GUILayout.Button("100 items", _buttonStyle))
            {
                SetItemCount(100);
            }
            if (GUILayout.Button("1,000 items", _buttonStyle))
            {
                SetItemCount(1000);
            }
            if (GUILayout.Button("10,000 items", _buttonStyle))
            {
                SetItemCount(10000);
            }
            GUILayout.EndHorizontal();

            if (GUILayout.Button("Jump to top", _buttonStyle))
            {
                JumpToIndex(0, VirtualizedListAlignment.Start);
            }
            if (GUILayout.Button("Jump to middle", _buttonStyle))
            {
                JumpToIndex(_layout.ItemCount / 2);
            }
            if (GUILayout.Button("Jump to bottom", _buttonStyle))
            {
                JumpToIndex(
                    _layout.ItemCount - 1,
                    VirtualizedListAlignment.End);
            }
            if (GUILayout.Button("Scroll one viewport", _buttonStyle))
            {
                ScrollBy(_layout.ViewportExtent);
            }

            GUILayout.Space(8f);
            GUILayout.Label(_status, _labelStyle);
            GUILayout.Label(
                "Drag or use the mouse wheel over the list. The content height " +
                "represents every item; row objects represent only the window.",
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
