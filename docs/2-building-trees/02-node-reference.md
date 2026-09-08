# Node reference

Every node that ships with BH3, organised the way the canvas right-click menu organises them.

Each node also shows its description in the Graph Inspector when selected. From the command line,
`bt_list_nodes` prints this catalogue from the code itself, including which ports must be connected, so it
can never go stale.

---

## Composite

Composites have several children and decide which of them run. Children are tried in **priority order**,
the numbered badge on each child, which normally reads left to right. See
[Execution order](05-execution-order.md).

| Menu | Type | Behaviour |
|---|---|---|
| Sequence | `Sequence` | Children in order. Ends with `Failure` at the first child that fails; `Success` when all succeed |
| Selector | `Selector` | Children in order. Ends with `Success` at the first child that succeeds; `Failure` when all fail. A higher-priority child whose reactive guard turns true takes over from a running lower one |
| Parallel Sequence | `ParallelSequence` | All children at once. Ends when any returns `Failure` |
| Parallel Selector | `ParallelSelector` | All children at once. Ends when any returns `Success` |
| Random Sequence | `RandomSequence` | A Sequence whose child order is reshuffled after every pass |
| Random Selector | `RandomSelector` | A Selector whose child order is reshuffled after every pass |

The naming is consistent once you see it: **Sequence** ends on failure, **Selector** ends on success; the
prefix only changes the *order* children are tried in.

## Decorator

Decorators wrap **exactly one** child and change how its result is read, or whether it runs at all.

| Menu | Type | Behaviour |
|---|---|---|
| Repeater | `Repeater` | Re-enters the child forever and always returns `Running`. Put one under Entry, or the tree runs once |
| Until Success | `UntilSuccess` | Re-runs the child until it returns `Success` |
| Until Failure | `UntilFailure` | Re-runs the child until it returns `Failure` |
| Return Success | `ReturnSuccess` | Overrides the child's result with `Success` |
| Return Failure | `ReturnFailure` | Overrides the child's result with `Failure` |
| Cooldown | `Cooldown` | Runs the child, then blocks it for `Duration` seconds (default 5), returning `Failure` while cooling down |
| Random Chance | `RandomChance` | Rolls once on enter. Runs the child if the roll passes `Chance` (default 0.5), else returns `Failure` without ticking it. A chance of 0 never fires and 1 always does |

The canvas and the command-line tools refuse a second child on a decorator. Code using
`BehaviorTreeAuthoring.Connect` directly can attach one, but only the first child ever runs.

## Condition

| Menu | Type | Behaviour |
|---|---|---|
| Boolean Condition | `BooleanCondition` | A leaf. Returns `Success` while `Value` is true, `Failure` otherwise |
| Conditional Execution | `BooleanConditionalExecution` | A guard asked **once, at entry**: the owner is skipped while `Value` is false, and not asked again while it runs |
| Reactive Guard | `BooleanReactiveGuard` | A guard that **keeps watching**: it aborts its owner when `Value` turns false, and can take the slot from a lower-priority sibling when it turns true |

A guard is not a decorator. It has no parent; it attaches to an **owner** node as a precondition, and several
guards on one owner are ANDed. Use a Condition for a decision made at one point in the flow, and a guard
when a branch should stop being valid the instant the world changes. See [Guards](06-guards.md).

## Flow

| Menu | Type | Behaviour |
|---|---|---|
| Wait | `WaitTime` | Waits `Time` seconds, then succeeds. `Time` defaults to 0 |
| Wait Range | `WaitTimeRandomRange` | Waits a random time between `minTime` and `maxTime`, both default 0 |
| Run Behavior Tree Graph | `RunBehaviorTreeGraphNode` | Runs another tree asset as a sub-tree, with its parameters as ports. See [Sub-trees](07-sub-trees.md) |

## Variables

| Menu | Behaviour |
|---|---|
| Get Variable | Reads a variable by `Key` and offers it on an output port |
| Set Variable | Writes `Value` to the variable named `Key` |
| Remove Variable | Removes the variable named `Key` |

Each takes a **Key** on a port, so it can be typed inline or driven by a graph, and a **Variable Kind** in
the inspector: `Graph`, `Object`, `Scene`, `Application` or `Saved`. See
[Variables and scope](04-variables-and-scope.md) for what each kind means.

A new node starts with **no kind chosen** and an empty key, and says so with a problem badge. Running it in
that state throws, naming the node, rather than quietly reading nothing.

## Literal

Literals hold a typed, inspector-editable value and offer it on an output port. They are the standard way
to fill a port that has no default.

`Boolean` · `Float` · `Integer` · `String` · `Vector2` · `Vector3` · `Variable Key` · `TPS Query` ·
`This GameObject` · `This Transform`

`TPS Query` only exists when the Tactical Position Selection module is installed.

## Math

`Add (A + B)` · `Subtract (A - B)` · `Multiply (A x B)` · `Divide (A / B)` · `Modulo (A % B)`

Every input defaults to 0, except `Divide`'s `B`, which defaults to 1.

## Logic

| Menu | Type | Behaviour |
|---|---|---|
| Not | `Not` | Outputs the inverse of `Value` |
| Is Not Null | `IsNotNull` | Outputs true when `Value` references a live object |

## Navigation

| Menu | Behaviour |
|---|---|
| Set Nav Agent Position | Sets the NavMeshAgent's destination to `NavPosition` |
| Generate Random NavMesh Position | A value node. Samples a random point between `MinDistance` and `MaxDistance` from the agent, trying up to `MaxTries` times (default 3), and outputs `Position` and `HasPosition`. `SampleDistance` (default 1) is how far from the random point it looks for NavMesh; 0 can never hit anything |
| Wait Until Reach Nav Target Position | Runs until the agent is within its stopping distance of its destination |
| Stop Nav Agent | Stops the NavMeshAgent |
| Tactical Position Selection | Runs a TPS query and outputs `SelectedPosition` and `HasPosition`. Needs the Tactical Position Selection module |

On the first, third and fourth, `Target` may be left empty and means the agent itself.

## Transform

| Menu | Behaviour |
|---|---|
| Set Position | Sets `Target`'s position to `NewPosition` |
| Look At | Rotates `Target` to look at `LookTarget` |
| Face Target | Rotates the agent to face `TransformTarget`, at `RotationSpeed`, succeeding within `AcceptableRotation` degrees |
| Rotate | Spins the transform around `Axis` at `Speed` degrees per second. Always `Running`; it never finishes on its own |
| Set Rotation | Turns to `TargetRotation` over `Duration` seconds, then succeeds. A `Duration` of 0 snaps and succeeds on the first tick |

`Rotate` keeps spinning, so put it under something that will stop it: a guard, or a sibling in a Parallel
that succeeds.

## Physics

| Menu | Behaviour |
|---|---|
| Add Force | Applies `Force` to the target Rigidbody |
| Add Torque | Applies torque to the target Rigidbody |
| Add Explosive Force | Applies a force simulating an explosion to the target Rigidbody |

## Animation

| Menu | Behaviour |
|---|---|
| Set Animator Value | Sets an int, float or bool parameter on `Animator` |
| Set Animator Trigger | Sets a trigger on `Animator` |
| Cross Fade Animation | Cross-fades `Animator` to `StateName` |

## Game Object

| Menu | Behaviour |
|---|---|
| Instantiate Object | Clones the original object |
| Destroy Object | Destroys the input GameObject, component or asset |
| Find Game Object | Finds a GameObject in the scene by name |
| Find Game Object With Tag | Finds a GameObject in the scene by tag |
| Dont Destroy On Load | Keeps the target object alive across scene loads |

## Audio

| Menu | Behaviour |
|---|---|
| Play Audio | Plays an audio clip |

## Debug

`Debug Log` · `Debug Log Warning` · `Debug Log Error` — write `LogText` to the console.

## Visual Scripting

| Menu | Behaviour |
|---|---|
| Script Graph | A node whose lifecycle hooks (`OnAwake`, `OnEnter`, `OnUpdate`, `OnExit`) are each a [Function](08-functions.md). `OnUpdate`'s Function must return an `ExecutionStatus` |
| Script Graph Variable | Runs a Function and offers its result on an output port. The bridge from Visual Scripting into any port. See [Ports and wiring](03-ports-and-wiring.md) |

---

## Ports you must connect

Most ports declare a default, so leaving them unconnected is fine. These declare none, and reading one that
is both unconnected and defaultless throws at runtime rather than falling back to zero. The node shows a red
problem badge until you connect something.

| Node | Ports |
|---|---|
| Face Target | `TransformTarget`, `RotationOffset`, `RotationSpeed`, `AcceptableRotation` |
| Set Variable | `Value` |
| Set Animator Value | `Animator`, `Value` |
| Set Animator Trigger | `Animator` |
| Cross Fade Animation | `Animator` |
| Debug Log, Debug Log Warning, Debug Log Error | `LogText` |

Two related cases look similar but are checked differently:

- **`Key` on the Variables nodes** defaults to empty. An empty key throws when the node runs, naming the
  node, and shows a problem badge beforehand.
- **`Target` on the Navigation nodes** may be left empty and means the agent itself.

To fill a defaultless port, connect a **Literal** of the matching type. Generating trees from code has an
extra trap here; see [Authoring from code](../4-extending-with-csharp/04-authoring-from-code.md#3-inline-values-do-not-survive-a-reload-on-a-defaultless-port).

---

## Next

- [Ports and wiring](03-ports-and-wiring.md)
- [Custom nodes](../4-extending-with-csharp/01-custom-nodes.md) — when none of the above fits
