# UI Navigation and View Lifecycle

## 06.1 Bounded navigation stack baseline

UI navigation is state management before it is animation, prefab loading, or
visual styling. The first UI stage therefore isolates deterministic navigation
rules in `UiNavigationStack`, which belongs to the engine-independent runtime
assembly and can be tested without entering Play Mode.

The model owns two fixed-capacity stacks:

- screens represent navigable full-page destinations;
- modals temporarily own input above the current screen.

`Back` always removes the top modal first. With no modal present, it removes the
top screen while preserving the final root screen. Successful mutations
increment a revision and publish a `UiNavigationChange`. Rejected duplicate,
capacity, no-change, and root-protection operations do not change the revision.

Routes are unique within their own layer. This baseline deliberately rejects a
second `Inventory` screen or `Settings` modal instead of silently creating
ambiguous ownership. A later stage can introduce an explicit instance key for
flows that legitimately need multiple instances of one route.

Change callbacks are observation points, not command handlers. Mutating the
stack from inside `Changed` throws an exception because nested mutation makes
event order and view ownership difficult to reason about. A production UI can
enqueue a command for the next coordinator tick when callback-driven
navigation is required.

Open
`Project/Assets/Samples/UiNavigation/UiNavigationDemo.unity` and enter Play
Mode. The sample starts with a protected `Home` root. Try the following:

1. push `Inventory`, then push `Profile`;
2. open `Settings`, then `Confirm`;
3. press Back and observe that both modals close before a screen is removed;
4. press Back at `Home` and observe `RootScreenProtected`;
5. press the same route twice and observe `DuplicateRoute`;
6. use Replace to exchange only the top screen without increasing depth.

The scene uses IMGUI only as an observable control surface for the navigation
model. It is not presented as the final UI implementation. The next section
keeps this navigation contract unchanged while adding real Unity view
instances, asynchronous creation, owner cancellation, and deterministic
close/dispose behavior.

## 06.2 Asynchronous view ownership and lifecycle

`UiViewLifecycleCoordinator` subscribes to successful navigation changes and
maps each accepted route to one asynchronously created `IUiView`. The
navigation stack remains the source of truth; the coordinator owns the side
effects required to make that state visible.

The lifecycle states are observable independently from navigation revision:

- `Loading` means a factory request is in flight;
- `Ready` means an instance exists but has not necessarily become topmost;
- `Visible` owns presentation for its screen or modal layer;
- `Covered` remains alive below a newer view and can be resumed;
- `Cancelled`, `Closed`, and `Faulted` are explicit terminal outcomes.

Screen and modal presentation are independent. A new screen covers the previous
screen only after its own factory request succeeds. A modal covers only the
previous modal; it does not hide the active screen. Removing the active entry
calls `Exit`, disposes the owned instance exactly once, and resumes the newest
available entry in the same layer.

Cancellation is an ownership rule, not merely a loading optimization. When
Back removes a route that is still loading, the coordinator cancels its token
and removes the entry immediately. A provider might ignore cancellation or be
unable to cancel native work, so completion is checked against the current
owned entry. A late result is disposed without receiving `Enter`; it can never
resurrect a route that navigation has already removed.

Open
`Project/Assets/Samples/UiNavigation/UiViewLifecycleDemo.unity` and enter Play
Mode. The right side contains real `Canvas`, `Image`, and `Text` GameObjects
created by a delayed UniTask factory. Try the following:

1. push `Inventory`, wait for it to become visible, then Back to resume `Home`;
2. open `Settings` and `Confirm`, then observe modal-only cover/resume behavior;
3. push and replace screens while watching Loading/Ready/Visible history;
4. run `Push SlowDetails then immediately Back` and verify that cancellation
   increases while `Home` remains visible and no late view appears.

The runtime coordinator uses `Task` and has no Unity engine reference. The
sample factory adapts UniTask PlayerLoop work to that boundary and constructs
Unity objects only on the main thread. A production project can replace the
factory with Addressables or a project-specific UI asset service without
changing navigation or lifecycle policy.

## 06.3 Route registry and leased prefab factory

The 06.2 programmatic factory is useful for isolating lifecycle behavior, but a
project normally resolves navigation routes to authored prefabs. 06.3 keeps the
same `IAsyncUiViewFactory` boundary and adds three replaceable pieces:

- `UiRouteViewRegistry` is an immutable, engine-independent mapping from
  `(layer, route ID)` to an asset key;
- `ResourcesPrefabUiViewFactory` resolves the key and acquires the prefab
  through the existing shared asset store;
- `PrefabUiViewBehaviour` contains only prefab presentation behavior while a
  wrapper owns the instantiated GameObject and the prefab asset lease.

Layer belongs to route identity. `Screen:Settings` and `Modal:Settings` may map
to different prefabs, while duplicate registration of the exact same route is
rejected at startup. Missing mappings fail the corresponding view lifecycle
explicitly instead of silently constructing a fallback UI.

One view instance owns one `AssetLease<GameObject>`. Covered screens retain both
their instance and lease so they can resume without another load. On permanent
close, the wrapper destroys the instance and disposes the lease. The shared
store forgets the prefab after the final lease is released. This keeps instance
lifetime and source-asset ownership related without treating them as the same
object.

There is an important Resources-specific boundary here: Unity rejects
`Resources.UnloadAsset` for `GameObject` prefab assets. Therefore
`ResourcesPrefabUniTaskAssetProvider.Unload` records that the final logical
lease was released but does not pretend that one prefab was physically
unloaded. A production Resources integration schedules the global
`Resources.UnloadUnusedAssets` sweep at a controlled loading/scene transition,
after unused instances have been destroyed. An Addressables implementation can
instead retain and release the exact load handle. The navigation and lifecycle
layers do not change for either policy.

Open
`Project/Assets/Samples/UiNavigation/UiPrefabFactoryDemo.unity` and enter Play
Mode. Push screens and modals while observing load requests, currently loaded
prefabs tracked by the shared store, live instances, and lease release count.
Back should close the top view, destroy its instance, release its lease, remove
the prefab from the store after its final owner, and resume the covered view
without loading it again.

The included editor command
`Unity Core Lab/Samples/Rebuild UI Prefab Factory Demo` regenerates all six
sample prefabs and the demo scene. Resources is intentionally used as a small,
self-contained adapter; replacing it with Addressables changes the factory and
asset provider, not navigation or lifecycle policy.

## 06.4 Presenter and explicit content states

Navigation and view ownership do not answer where UI decisions belong. Putting
loading, retry, empty-result and error rules directly in a `MonoBehaviour`
makes those rules difficult to test and encourages every screen to invent a
different state model. 06.4 introduces an engine-independent presentation
boundary under `Runtime/UiPresentation`:

- `IUiContentSource<TItem>` owns asynchronous data acquisition;
- `IUiContentView<TItem>` emits refresh intent and renders a snapshot;
- `UiContentPresenter<TItem>` owns request lifetime and state transitions;
- `UiContentSnapshot<TItem>` is an immutable Loading, Content, Empty, or Error
  value consumed by the View.

The View is deliberately passive. It does not call a service, catch domain
exceptions, decide whether zero items means Empty, or keep its own request
version. Its only output is `RefreshRequested`; its only input is `Render`.
This makes the same Presenter testable without loading a Unity scene.

Every refresh receives a monotonically increasing revision and its own
`CancellationTokenSource`. Starting a newer refresh cancels the previous one.
Cancellation alone is not treated as proof that an underlying operation really
stopped, so a result is rendered only when its request is still the current
request. A data source that ignores cancellation therefore cannot overwrite a
newer result. Disposing the Presenter cancels the active request, detaches the
View event, and rejects every later completion.

The Presenter uses ordinary `Task` and has no Unity reference. It is intended
to be created and refreshed from the UI owner context; normal `await` then
returns rendering to Unity's captured synchronization context. A data adapter
that deliberately suppresses context capture must marshal its completion back
to the UI context before touching a Unity View.

Open `Project/Assets/Samples/UiPresentation/UiPresenterDemo.unity` and enter
Play Mode. The right side is a real uGUI View; the left side controls its sample
source. Exercise these paths:

1. load Content, Empty, and Error and observe one explicit state per result;
2. start a slow request, then immediately load Content and observe cancellation
   plus a newer revision;
3. start a slow request and dispose the Presenter; the View becomes inactive
   and receives no late render;
4. recreate the Presenter and observe that a new owner starts at revision 1;
5. use the View's own refresh button to verify that it emits intent without
   loading data itself.

## 06.5 Awaitable dialog requests and queue policy

The routed modal pages in 06.1 through 06.3 are navigation destinations with
stack and View lifecycle behavior. A dialog in this stage is instead a
short-lived Notice, Confirmation, or Blocking request whose caller waits for a
result. If every screen instantiates its own confirmation prefab, projects
quickly accumulate duplicate dialogs, overlapping input barriers, unresolved
callbacks, and unclear completion ownership. 06.5 isolates that request policy
in the engine-independent `UiDialogQueue` and keeps uGUI inside a passive host.

The queue maintains exactly one active request and a pending list. Pending
requests are ordered by priority, while requests with the same priority retain
FIFO order. A newly queued Critical request does not tear down or preempt the
current owner; priority only selects which pending request becomes active
next. This avoids silently dismissing a decision that the caller is already
awaiting.

Request IDs are ownership keys. Showing the same ID twice with the same kind
returns the existing completion task instead of creating a second visual. A
duplicate ID with a different kind is rejected because one identity cannot
simultaneously mean both a notice and a confirmation. Once the original entry
finishes, that ID may be used again for a new request.

Three explicit dialog kinds demonstrate different completion ownership:

- Notice exposes one acknowledgement path;
- Confirmation exposes Confirm and Cancel and can be consumed as
  `await dialogQueue.ShowConfirmAsync(...)`;
- Blocking exposes no user-owned close button and must be completed by the
  operation owner through `Complete`.

`DialogDemoView` owns only rendering and user intent. Its full-screen
`CanvasGroup` sets `blocksRaycasts` while a dialog is active, preventing the
underlying uGUI from receiving pointer input. It does not select the next
request, resolve duplicates, or complete a blocking operation. Disposing the
queue detaches the View and resolves all active and pending ownership with
the explicit `Dismissed` result so no awaiting caller is left hanging.

`UiDialogQueue` is responsible only for request coordination and result
completion. View construction, route integration, and application-specific
presentation remain independent concerns.

Open `Project/Assets/Samples/UiDialogs/UiDialogDemo.unity` and enter Play Mode:

1. click the underlying uGUI button with no dialog, then show a Notice and
   verify that the same button no longer receives pointer input;
2. open a Confirmation and choose Confirm or Cancel; the status displays the
   awaited Boolean result;
3. show a Blocking operation and verify that its hidden buttons cannot dismiss
   it, then use `Complete blocking operation` as the operation owner;
4. show the duplicate ID twice and observe one View presentation plus one
   shared completion task;
5. build the priority scenario and advance it: the active gate remains first,
   followed by Critical, High, and Normal pending requests.
