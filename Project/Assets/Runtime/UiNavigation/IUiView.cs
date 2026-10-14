using System;

namespace UnityCoreLab.UiNavigation
{
    /// <summary>
    /// A created view instance owned by the lifecycle coordinator. Implementations
    /// translate these callbacks into engine-specific visibility and cleanup work.
    /// </summary>
    public interface IUiView : IDisposable
    {
        UiRoute Route { get; }

        void Enter();

        void Cover();

        void Resume();

        void Exit();
    }
}
