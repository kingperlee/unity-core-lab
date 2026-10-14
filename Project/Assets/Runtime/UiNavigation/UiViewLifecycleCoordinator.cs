using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace UnityCoreLab.UiNavigation
{
    /// <summary>
    /// Turns committed navigation changes into owned asynchronous view instances.
    /// A removed route is cancelled immediately, and a factory result that arrives
    /// after removal is disposed without ever becoming visible.
    /// </summary>
    public sealed class UiViewLifecycleCoordinator : IDisposable
    {
        private readonly UiNavigationStack _navigation;
        private readonly IAsyncUiViewFactory _factory;
        private readonly Dictionary<string, Entry> _entries =
            new Dictionary<string, Entry>(StringComparer.Ordinal);
        private readonly List<Entry> _screens = new List<Entry>();
        private readonly List<Entry> _modals = new List<Entry>();
        private readonly CancellationTokenSource _lifetimeCancellation =
            new CancellationTokenSource();
        private Entry _activeScreen;
        private Entry _activeModal;
        private long _lifecycleRevision;
        private bool _disposed;

        public UiViewLifecycleCoordinator(
            UiNavigationStack navigation,
            IAsyncUiViewFactory factory)
        {
            _navigation = navigation ??
                throw new ArgumentNullException(nameof(navigation));
            _factory = factory ?? throw new ArgumentNullException(nameof(factory));
            _navigation.Changed += HandleNavigationChanged;
        }

        public event Action<UiViewLifecycleChange> Changed;

        public string ActiveScreenId => _activeScreen?.Route.Id;

        public string ActiveModalId => _activeModal?.Route.Id;

        public int TrackedViewCount => _entries.Count;

        public bool TryGetState(
            string routeId,
            UiLayer layer,
            out UiViewState state)
        {
            if (_entries.TryGetValue(CreateKey(routeId, layer), out Entry entry))
            {
                state = entry.State;
                return true;
            }

            state = default;
            return false;
        }

        public void Dispose()
        {
            if (_disposed)
            {
                return;
            }

            _disposed = true;
            _navigation.Changed -= HandleNavigationChanged;
            _lifetimeCancellation.Cancel();

            Entry[] entries = new Entry[_entries.Count];
            _entries.Values.CopyTo(entries, 0);
            foreach (Entry entry in entries)
            {
                CloseEntry(entry, publish: false, activateFallback: false);
            }

            _entries.Clear();
            _screens.Clear();
            _modals.Clear();
            _activeScreen = null;
            _activeModal = null;
            _lifetimeCancellation.Dispose();
        }

        private void HandleNavigationChanged(UiNavigationChange change)
        {
            if (_disposed)
            {
                return;
            }

            switch (change.Kind)
            {
                case UiNavigationChangeKind.Pushed:
                    StartLoading(change.Route);
                    break;

                case UiNavigationChangeKind.Popped:
                    CloseRoute(change.Route);
                    break;

                case UiNavigationChangeKind.Replaced:
                    CloseRoute(change.PreviousRoute);
                    StartLoading(change.Route);
                    break;

                default:
                    throw new ArgumentOutOfRangeException(nameof(change));
            }
        }

        private void StartLoading(UiRoute route)
        {
            string key = CreateKey(route.Id, route.Layer);
            if (_entries.ContainsKey(key))
            {
                throw new InvalidOperationException(
                    $"A view lifecycle already exists for '{route}'.");
            }

            var cancellation = CancellationTokenSource.CreateLinkedTokenSource(
                _lifetimeCancellation.Token);
            var entry = new Entry(route, cancellation);
            _entries.Add(key, entry);
            GetLayerEntries(route.Layer).Add(entry);
            Publish(entry, UiViewState.Loading, "Factory request started.");
            entry.LoadTask = LoadAsync(entry);
        }

        private async Task LoadAsync(Entry entry)
        {
            try
            {
                IUiView view = await _factory.CreateAsync(
                    entry.Route,
                    entry.Cancellation.Token);
                if (view == null)
                {
                    throw new InvalidOperationException(
                        $"The factory returned null for '{entry.Route}'.");
                }

                if (entry.IsClosed ||
                    !_entries.TryGetValue(entry.Key, out Entry current) ||
                    !ReferenceEquals(current, entry))
                {
                    view.Dispose();
                    return;
                }

                entry.View = view;
                Publish(entry, UiViewState.Ready, "Factory request completed.");
                if (IsTopDesiredEntry(entry))
                {
                    Activate(entry);
                }
            }
            catch (OperationCanceledException)
                when (entry.Cancellation.IsCancellationRequested)
            {
                if (!entry.IsClosed)
                {
                    Publish(entry, UiViewState.Cancelled, "Loading was cancelled.");
                }
            }
            catch (Exception exception)
            {
                if (!entry.IsClosed)
                {
                    Publish(entry, UiViewState.Faulted, exception.Message);
                }
            }
            finally
            {
                entry.DisposeCancellation();
            }
        }

        private void CloseRoute(UiRoute route)
        {
            if (!_entries.TryGetValue(
                    CreateKey(route.Id, route.Layer),
                    out Entry entry))
            {
                return;
            }

            CloseEntry(entry, publish: true, activateFallback: true);
        }

        private void CloseEntry(
            Entry entry,
            bool publish,
            bool activateFallback)
        {
            if (entry.IsClosed)
            {
                return;
            }

            entry.IsClosed = true;
            _entries.Remove(entry.Key);
            GetLayerEntries(entry.Route.Layer).Remove(entry);
            entry.Cancel();

            bool wasActive = ReferenceEquals(GetActive(entry.Route.Layer), entry);
            if (wasActive)
            {
                SetActive(entry.Route.Layer, null);
            }

            if (entry.View != null)
            {
                if (entry.HasEntered)
                {
                    entry.View.Exit();
                }

                entry.View.Dispose();
                entry.View = null;
                if (publish)
                {
                    Publish(entry, UiViewState.Closed, "View exited and was disposed.");
                }
            }
            else if (publish)
            {
                Publish(entry, UiViewState.Cancelled, "Pending creation was cancelled.");
            }

            if (activateFallback && wasActive)
            {
                ActivateBestAvailable(entry.Route.Layer);
            }
        }

        private void ActivateBestAvailable(UiLayer layer)
        {
            List<Entry> entries = GetLayerEntries(layer);
            for (int index = entries.Count - 1; index >= 0; index--)
            {
                Entry candidate = entries[index];
                if (candidate.View != null && !candidate.IsClosed)
                {
                    Activate(candidate);
                    return;
                }
            }
        }

        private void Activate(Entry entry)
        {
            Entry active = GetActive(entry.Route.Layer);
            if (ReferenceEquals(active, entry))
            {
                return;
            }

            if (active != null && active.View != null)
            {
                active.View.Cover();
                Publish(active, UiViewState.Covered, $"Covered by {entry.Route.Id}.");
            }

            if (entry.HasEntered)
            {
                entry.View.Resume();
                Publish(entry, UiViewState.Visible, "View resumed.");
            }
            else
            {
                entry.View.Enter();
                entry.HasEntered = true;
                Publish(entry, UiViewState.Visible, "View entered.");
            }

            SetActive(entry.Route.Layer, entry);
        }

        private bool IsTopDesiredEntry(Entry entry)
        {
            List<Entry> entries = GetLayerEntries(entry.Route.Layer);
            return entries.Count > 0 && ReferenceEquals(entries[entries.Count - 1], entry);
        }

        private List<Entry> GetLayerEntries(UiLayer layer)
        {
            return layer == UiLayer.Screen ? _screens : _modals;
        }

        private Entry GetActive(UiLayer layer)
        {
            return layer == UiLayer.Screen ? _activeScreen : _activeModal;
        }

        private void SetActive(UiLayer layer, Entry entry)
        {
            if (layer == UiLayer.Screen)
            {
                _activeScreen = entry;
            }
            else
            {
                _activeModal = entry;
            }
        }

        private void Publish(Entry entry, UiViewState state, string detail)
        {
            entry.State = state;
            _lifecycleRevision++;
            Changed?.Invoke(new UiViewLifecycleChange(
                _lifecycleRevision,
                entry.Route,
                state,
                detail));
        }

        private static string CreateKey(string routeId, UiLayer layer)
        {
            return $"{(int)layer}:{routeId}";
        }

        private sealed class Entry
        {
            public Entry(UiRoute route, CancellationTokenSource cancellation)
            {
                Route = route;
                Key = CreateKey(route.Id, route.Layer);
                Cancellation = cancellation;
                State = UiViewState.Loading;
            }

            public UiRoute Route { get; }

            public string Key { get; }

            public CancellationTokenSource Cancellation { get; private set; }

            public IUiView View { get; set; }

            public Task LoadTask { get; set; }

            public UiViewState State { get; set; }

            public bool HasEntered { get; set; }

            public bool IsClosed { get; set; }

            public void Cancel()
            {
                Cancellation?.Cancel();
            }

            public void DisposeCancellation()
            {
                CancellationTokenSource cancellation = Cancellation;
                Cancellation = null;
                cancellation?.Dispose();
            }
        }
    }
}
