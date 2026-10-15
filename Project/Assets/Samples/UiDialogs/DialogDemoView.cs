using System;
using UnityEngine;
using UnityEngine.UI;
using UnityCoreLab.UiDialogs;

namespace UnityCoreLab.Samples.UiDialogs
{
    /// <summary>
    /// Passive uGUI dialog host. The full-screen CanvasGroup owns the visual
    /// input barrier while UiDialogQueue owns policy and completion.
    /// </summary>
    public sealed class DialogDemoView : MonoBehaviour, IUiDialogView
    {
        private CanvasGroup _canvasGroup;
        private Text _titleText;
        private Text _messageText;
        private Text _kindText;
        private Text _primaryLabel;
        private Text _secondaryLabel;
        private Button _primaryButton;
        private Button _secondaryButton;

        public event Action<UiDialogResult> DecisionRequested;

        public bool IsVisible { get; private set; }

        public bool BlocksInput =>
            _canvasGroup != null && _canvasGroup.blocksRaycasts;

        public string CurrentId { get; private set; } = string.Empty;

        public UiDialogKind CurrentKind { get; private set; }

        public int ShowCount { get; private set; }

        public int HideCount { get; private set; }

        public void Configure(
            CanvasGroup canvasGroup,
            Text titleText,
            Text messageText,
            Text kindText,
            Button primaryButton,
            Text primaryLabel,
            Button secondaryButton,
            Text secondaryLabel)
        {
            _canvasGroup = canvasGroup;
            _titleText = titleText;
            _messageText = messageText;
            _kindText = kindText;
            _primaryButton = primaryButton;
            _primaryLabel = primaryLabel;
            _secondaryButton = secondaryButton;
            _secondaryLabel = secondaryLabel;

            _primaryButton.onClick.AddListener(HandlePrimaryClicked);
            _secondaryButton.onClick.AddListener(HandleSecondaryClicked);
            SetVisible(false);
        }

        public void Show(UiDialogRequest request)
        {
            if (request == null)
            {
                throw new ArgumentNullException(nameof(request));
            }

            CurrentId = request.Id;
            CurrentKind = request.Kind;
            ShowCount++;
            _titleText.text = request.Title;
            _messageText.text = request.Message;
            _kindText.text =
                $"{request.Kind} · {request.Priority} · ID {request.Id}";

            bool notice = request.Kind == UiDialogKind.Notice;
            bool confirmation = request.Kind == UiDialogKind.Confirmation;
            _primaryButton.gameObject.SetActive(notice || confirmation);
            _secondaryButton.gameObject.SetActive(confirmation);
            _primaryLabel.text = notice ? "OK" : "Confirm";
            _secondaryLabel.text = "Cancel";
            SetVisible(true);
        }

        public void Hide()
        {
            HideCount++;
            CurrentId = string.Empty;
            SetVisible(false);
        }

        public void PressPrimary()
        {
            HandlePrimaryClicked();
        }

        public void PressSecondary()
        {
            HandleSecondaryClicked();
        }

        private void OnDestroy()
        {
            if (_primaryButton != null)
            {
                _primaryButton.onClick.RemoveListener(HandlePrimaryClicked);
            }

            if (_secondaryButton != null)
            {
                _secondaryButton.onClick.RemoveListener(HandleSecondaryClicked);
            }
        }

        private void HandlePrimaryClicked()
        {
            switch (CurrentKind)
            {
                case UiDialogKind.Notice:
                    DecisionRequested?.Invoke(UiDialogResult.Acknowledged);
                    break;
                case UiDialogKind.Confirmation:
                    DecisionRequested?.Invoke(UiDialogResult.Confirmed);
                    break;
                case UiDialogKind.Blocking:
                    // Blocking dialogs have no user-owned completion control.
                    break;
            }
        }

        private void HandleSecondaryClicked()
        {
            if (CurrentKind == UiDialogKind.Confirmation)
            {
                DecisionRequested?.Invoke(UiDialogResult.Cancelled);
            }
        }

        private void SetVisible(bool visible)
        {
            IsVisible = visible;
            _canvasGroup.alpha = visible ? 1f : 0f;
            _canvasGroup.interactable = visible;
            _canvasGroup.blocksRaycasts = visible;
        }
    }
}
