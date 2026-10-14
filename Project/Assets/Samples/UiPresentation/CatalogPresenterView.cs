using System;
using System.Text;
using UnityEngine;
using UnityEngine.UI;
using UnityCoreLab.UiPresentation;

namespace UnityCoreLab.Samples.UiPresentation
{
    /// <summary>
    /// Passive uGUI view. It renders snapshots and emits refresh intent only.
    /// </summary>
    public sealed class CatalogPresenterView :
        MonoBehaviour,
        IUiContentView<CatalogItem>
    {
        [SerializeField] private CanvasGroup canvasGroup;
        [SerializeField] private Text stateText;
        [SerializeField] private Text contentText;
        [SerializeField] private Button refreshButton;

        public event Action RefreshRequested;

        public UiContentState CurrentState { get; private set; }

        public long CurrentRevision { get; private set; }

        public int ContentCount { get; private set; }

        public int RenderCount { get; private set; }

        public string RenderedText => contentText != null
            ? contentText.text
            : string.Empty;

        public bool IsInteractive =>
            refreshButton != null && refreshButton.interactable;

        public void Configure(
            CanvasGroup group,
            Text stateLabel,
            Text contentLabel,
            Button refresh)
        {
            if (refreshButton != null)
            {
                refreshButton.onClick.RemoveListener(HandleRefreshClicked);
            }

            canvasGroup = group;
            stateText = stateLabel;
            contentText = contentLabel;
            refreshButton = refresh;
            refreshButton.onClick.AddListener(HandleRefreshClicked);
            SetInteractive(true);
        }

        public void Render(UiContentSnapshot<CatalogItem> snapshot)
        {
            if (snapshot == null)
            {
                throw new ArgumentNullException(nameof(snapshot));
            }

            CurrentState = snapshot.State;
            CurrentRevision = snapshot.Revision;
            ContentCount = snapshot.Items.Count;
            RenderCount++;

            stateText.text =
                $"{snapshot.State}  ·  request revision {snapshot.Revision}";
            stateText.color = ResolveStateColor(snapshot.State);
            contentText.text = FormatContent(snapshot);
        }

        public void SetInteractive(bool interactive)
        {
            if (refreshButton != null)
            {
                refreshButton.interactable = interactive;
            }

            if (canvasGroup != null)
            {
                canvasGroup.interactable = interactive;
                canvasGroup.blocksRaycasts = interactive;
                canvasGroup.alpha = interactive ? 1f : 0.68f;
            }
        }

        public void RequestRefresh()
        {
            RefreshRequested?.Invoke();
        }

        private void OnDestroy()
        {
            if (refreshButton != null)
            {
                refreshButton.onClick.RemoveListener(HandleRefreshClicked);
            }
        }

        private void HandleRefreshClicked()
        {
            RefreshRequested?.Invoke();
        }

        private static string FormatContent(
            UiContentSnapshot<CatalogItem> snapshot)
        {
            switch (snapshot.State)
            {
                case UiContentState.Loading:
                    return "Loading catalog data...\n\n" +
                           "The previous request has already been cancelled.";

                case UiContentState.Empty:
                    return "No items returned.\n\n" +
                           "Empty is a valid state, not an error fallback.";

                case UiContentState.Error:
                    return "Request failed\n\n" + snapshot.ErrorMessage;

                case UiContentState.Content:
                    var builder = new StringBuilder();
                    for (int index = 0; index < snapshot.Items.Count; index++)
                    {
                        CatalogItem item = snapshot.Items[index];
                        builder.Append(index + 1)
                            .Append(". ")
                            .AppendLine(item.Title)
                            .Append("   ")
                            .AppendLine(item.Detail)
                            .AppendLine();
                    }

                    return builder.ToString();

                default:
                    throw new ArgumentOutOfRangeException();
            }
        }

        private static Color ResolveStateColor(UiContentState state)
        {
            switch (state)
            {
                case UiContentState.Content:
                    return new Color(0.32f, 0.9f, 0.58f);
                case UiContentState.Empty:
                    return new Color(1f, 0.78f, 0.28f);
                case UiContentState.Error:
                    return new Color(1f, 0.38f, 0.38f);
                default:
                    return new Color(0.32f, 0.75f, 1f);
            }
        }
    }
}
