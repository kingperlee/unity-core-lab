namespace UnityCoreLab.UiNavigation
{
    public readonly struct UiViewLifecycleChange
    {
        public UiViewLifecycleChange(
            long revision,
            UiRoute route,
            UiViewState state,
            string detail)
        {
            Revision = revision;
            Route = route;
            State = state;
            Detail = detail;
        }

        public long Revision { get; }

        public UiRoute Route { get; }

        public UiViewState State { get; }

        public string Detail { get; }
    }
}
