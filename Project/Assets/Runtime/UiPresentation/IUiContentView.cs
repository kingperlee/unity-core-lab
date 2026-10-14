using System;

namespace UnityCoreLab.UiPresentation
{
    /// <summary>
    /// Passive view boundary. The view publishes intent and renders snapshots;
    /// it does not load data or decide which state should be visible.
    /// </summary>
    public interface IUiContentView<TItem>
    {
        event Action RefreshRequested;

        void Render(UiContentSnapshot<TItem> snapshot);
    }
}
