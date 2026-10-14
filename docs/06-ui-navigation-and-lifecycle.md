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
