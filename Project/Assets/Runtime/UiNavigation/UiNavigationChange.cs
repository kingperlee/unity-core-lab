namespace UnityCoreLab.UiNavigation
{
    public readonly struct UiNavigationChange
    {
        public UiNavigationChange(
            long revision,
            UiNavigationChangeKind kind,
            UiRoute route,
            UiRoute previousRoute)
        {
            Revision = revision;
            Kind = kind;
            Route = route;
            PreviousRoute = previousRoute;
        }

        public long Revision { get; }

        public UiNavigationChangeKind Kind { get; }

        public UiRoute Route { get; }

        public UiRoute PreviousRoute { get; }
    }
}
