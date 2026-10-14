using System;

namespace UnityCoreLab.UiNavigation
{
    public readonly struct UiViewRegistration
    {
        public UiViewRegistration(UiRoute route, string assetKey)
        {
            if (string.IsNullOrWhiteSpace(route.Id))
            {
                throw new ArgumentException(
                    "A valid route is required.",
                    nameof(route));
            }

            if (string.IsNullOrWhiteSpace(assetKey))
            {
                throw new ArgumentException(
                    "A UI asset key is required.",
                    nameof(assetKey));
            }

            Route = route;
            AssetKey = assetKey;
        }

        public UiRoute Route { get; }

        public string AssetKey { get; }
    }
}
