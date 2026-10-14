using System;

namespace UnityCoreLab.UiNavigation
{
    /// <summary>
    /// Engine-independent, bounded navigation state. Modals consume Back
    /// before screens, and the final root screen cannot be removed.
    /// </summary>
    public sealed class UiNavigationStack
    {
        private readonly UiRoute[] _screens;
        private readonly UiRoute[] _modals;
        private int _screenCount;
        private int _modalCount;
        private bool _isNotifying;

        public UiNavigationStack(int screenCapacity, int modalCapacity)
        {
            if (screenCapacity <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(screenCapacity));
            }

            if (modalCapacity <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(modalCapacity));
            }

            _screens = new UiRoute[screenCapacity];
            _modals = new UiRoute[modalCapacity];
        }

        public event Action<UiNavigationChange> Changed;

        public int ScreenCapacity => _screens.Length;

        public int ModalCapacity => _modals.Length;

        public int ScreenCount => _screenCount;

        public int ModalCount => _modalCount;

        public long Revision { get; private set; }

        public UiNavigationResult PushScreen(string routeId)
        {
            return Push(new UiRoute(routeId, UiLayer.Screen));
        }

        public UiNavigationResult PushModal(string routeId)
        {
            return Push(new UiRoute(routeId, UiLayer.Modal));
        }

        public UiNavigationResult Push(UiRoute route)
        {
            EnsureMutationAllowed();
            ValidateRoute(route);
            UiRoute[] routes = GetRoutes(route.Layer);
            int count = GetCount(route.Layer);
            if (Contains(routes, count, route.Id))
            {
                return UiNavigationResult.DuplicateRoute;
            }

            if (count == routes.Length)
            {
                return UiNavigationResult.CapacityReached;
            }

            routes[count] = route;
            SetCount(route.Layer, count + 1);
            Publish(UiNavigationChangeKind.Pushed, route, default);
            return UiNavigationResult.Succeeded;
        }

        public UiNavigationResult ReplaceTopScreen(string routeId)
        {
            EnsureMutationAllowed();
            var replacement = new UiRoute(routeId, UiLayer.Screen);
            if (_screenCount == 0)
            {
                return Push(replacement);
            }

            UiRoute previous = _screens[_screenCount - 1];
            if (string.Equals(
                    previous.Id,
                    routeId,
                    StringComparison.Ordinal))
            {
                return UiNavigationResult.NoChange;
            }

            if (Contains(_screens, _screenCount - 1, routeId))
            {
                return UiNavigationResult.DuplicateRoute;
            }

            _screens[_screenCount - 1] = replacement;
            Publish(
                UiNavigationChangeKind.Replaced,
                replacement,
                previous);
            return UiNavigationResult.Succeeded;
        }

        public UiNavigationResult Back(out UiRoute removedRoute)
        {
            EnsureMutationAllowed();
            if (_modalCount > 0)
            {
                removedRoute = RemoveTop(_modals, ref _modalCount);
                Publish(
                    UiNavigationChangeKind.Popped,
                    removedRoute,
                    default);
                return UiNavigationResult.Succeeded;
            }

            if (_screenCount > 1)
            {
                removedRoute = RemoveTop(_screens, ref _screenCount);
                Publish(
                    UiNavigationChangeKind.Popped,
                    removedRoute,
                    default);
                return UiNavigationResult.Succeeded;
            }

            removedRoute = default;
            return _screenCount == 1
                ? UiNavigationResult.RootScreenProtected
                : UiNavigationResult.NoChange;
        }

        public bool TryGetTopScreen(out UiRoute route)
        {
            return TryGetTop(_screens, _screenCount, out route);
        }

        public bool TryGetTopModal(out UiRoute route)
        {
            return TryGetTop(_modals, _modalCount, out route);
        }

        public UiRoute GetScreen(int chronologicalIndex)
        {
            return GetAt(_screens, _screenCount, chronologicalIndex);
        }

        public UiRoute GetModal(int chronologicalIndex)
        {
            return GetAt(_modals, _modalCount, chronologicalIndex);
        }

        private void Publish(
            UiNavigationChangeKind kind,
            UiRoute route,
            UiRoute previousRoute)
        {
            Revision++;
            Action<UiNavigationChange> handler = Changed;
            if (handler == null)
            {
                return;
            }

            _isNotifying = true;
            try
            {
                handler(new UiNavigationChange(
                    Revision,
                    kind,
                    route,
                    previousRoute));
            }
            finally
            {
                _isNotifying = false;
            }
        }

        private void EnsureMutationAllowed()
        {
            if (_isNotifying)
            {
                throw new InvalidOperationException(
                    "Navigation cannot be mutated from a change callback.");
            }
        }

        private static void ValidateRoute(UiRoute route)
        {
            if (string.IsNullOrWhiteSpace(route.Id))
            {
                throw new ArgumentException(
                    "A valid UI route is required.",
                    nameof(route));
            }

            if (!Enum.IsDefined(typeof(UiLayer), route.Layer))
            {
                throw new ArgumentOutOfRangeException(nameof(route));
            }
        }

        private UiRoute[] GetRoutes(UiLayer layer)
        {
            return layer == UiLayer.Screen ? _screens : _modals;
        }

        private int GetCount(UiLayer layer)
        {
            return layer == UiLayer.Screen ? _screenCount : _modalCount;
        }

        private void SetCount(UiLayer layer, int count)
        {
            if (layer == UiLayer.Screen)
            {
                _screenCount = count;
            }
            else
            {
                _modalCount = count;
            }
        }

        private static bool Contains(
            UiRoute[] routes,
            int count,
            string routeId)
        {
            for (int index = 0; index < count; index++)
            {
                if (string.Equals(
                        routes[index].Id,
                        routeId,
                        StringComparison.Ordinal))
                {
                    return true;
                }
            }

            return false;
        }

        private static UiRoute RemoveTop(UiRoute[] routes, ref int count)
        {
            int index = count - 1;
            UiRoute route = routes[index];
            routes[index] = default;
            count--;
            return route;
        }

        private static bool TryGetTop(
            UiRoute[] routes,
            int count,
            out UiRoute route)
        {
            if (count == 0)
            {
                route = default;
                return false;
            }

            route = routes[count - 1];
            return true;
        }

        private static UiRoute GetAt(
            UiRoute[] routes,
            int count,
            int chronologicalIndex)
        {
            if (chronologicalIndex < 0 || chronologicalIndex >= count)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(chronologicalIndex));
            }

            return routes[chronologicalIndex];
        }
    }
}
