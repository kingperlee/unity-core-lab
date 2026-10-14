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
model. It is not presented as the final UI implementation. Stage 06.2 will add
real Unity view instances, asynchronous creation, cancellation tied to an
owner, and deterministic close/dispose behavior while keeping this navigation
contract unchanged.
