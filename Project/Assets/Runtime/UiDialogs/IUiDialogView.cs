using System;

namespace UnityCoreLab.UiDialogs
{
    /// <summary>
    /// Passive dialog host. It renders the active request and reports a user
    /// decision; queueing and completion ownership remain in UiDialogQueue.
    /// </summary>
    public interface IUiDialogView
    {
        event Action<UiDialogResult> DecisionRequested;

        void Show(UiDialogRequest request);

        void Hide();
    }
}
