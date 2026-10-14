using System;
using System.Collections.Generic;

namespace UnityCoreLab.UiNavigation
{
    /// <summary>
    /// Immutable route-to-asset mapping. Layer is part of the key, so a Screen
    /// and Modal may deliberately use the same route identifier.
    /// </summary>
    public sealed class UiRouteViewRegistry
    {
        private readonly Dictionary<string, string> _assetKeys;

        public UiRouteViewRegistry(IEnumerable<UiViewRegistration> registrations)
        {
            if (registrations == null)
            {
                throw new ArgumentNullException(nameof(registrations));
            }

            _assetKeys = new Dictionary<string, string>(StringComparer.Ordinal);
            foreach (UiViewRegistration registration in registrations)
            {
                string key = CreateKey(registration.Route);
                if (!_assetKeys.TryAdd(key, registration.AssetKey))
                {
                    throw new ArgumentException(
                        $"Route '{registration.Route}' is registered more than once.",
                        nameof(registrations));
                }
            }
        }

        public int Count => _assetKeys.Count;

        public bool TryGetAssetKey(UiRoute route, out string assetKey)
        {
            if (string.IsNullOrWhiteSpace(route.Id))
            {
                assetKey = null;
                return false;
            }

            return _assetKeys.TryGetValue(CreateKey(route), out assetKey);
        }

        public string GetAssetKey(UiRoute route)
        {
            if (!TryGetAssetKey(route, out string assetKey))
            {
                throw new KeyNotFoundException(
                    $"No UI asset is registered for route '{route}'.");
            }

            return assetKey;
        }

        private static string CreateKey(UiRoute route)
        {
            return $"{(int)route.Layer}:{route.Id}";
        }
    }
}
