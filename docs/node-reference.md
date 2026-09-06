# Node Reference

Every node that ships with BH3, organised the way the canvas right-click menu organises them.

Descriptions here are the nodes' own — each one also shows its description in the Graph Inspector when
selected, which is the fastest way to check something without leaving the editor.

---

## Composite

Composites have multiple children and decide which of them run. **Children execute in canvas X order** —
leftmost first.

| Menu | Type | Behavior |
|---|---|---|
| Create Sequence | `Sequence` | Children in order, left to right. Ends when a child returns `FAILURE` |
| Create Selector | `Selector` | Children in order, left to right. Ends when a child returns `SUCCESS` |
| Create Parallel Sequence | `ParallelSequence` | All children at the same time. Ends when any returns `FAILURE` |
| Create Parallel Selector | `ParallelSelector` | All children at the same time. Ends when any returns `SUCCESS` |
| Random Sequence | `RandomSequence` | Children in random order. Ends when any returns `FAILURE` |
| Random Selector | `RandomSelector` | Children in random order. Ends when any returns `SUCCESS` |

The naming is consistent once you see it: **Sequence** ends on failure, **Selector** ends on success; the
prefix only changes the *order* children are tried in.

## Decorator

Decorators wrap **exactly one** child and change how its result is interpreted, or whether it runs at all.

| Menu | Type | Behavior |
|---|---|---|
| Create Repeater | `Repeater` | Re-enters the child forever. Always returns `Running` — never `Success` or `Failure` |
| Create Until Success | `UntilSuccess` | Re-runs the child until it returns `SUCCESS` |
| Create Until Failure | `UntilFailure` | Re-runs the child until it returns `FAILURE` |
| Create Return Success | `ReturnSuccess` | Overrides the child's result with `SUCCESS` |
| Create Return Failure | `ReturnFailure` | Overrides the child's result with `FAILURE` |
| Create Cooldown | `Cooldown` | Runs the child, then blocks it for `Duration` seconds. Returns `FAILURE` while cooling down |
| Create Random Chance | `RandomChance` | Rolls once on enter. Runs the child if the roll passes, else returns `FAILURE` without ticking it |

`Cooldown` defaults to 5 seconds; `RandomChance` to 0.5. A chance of 0 never fires and 1 always does — the
bounds are handled explicitly rather than left to floating-point luck.

> **A decorator takes one child and only one.** Nothing stops you connecting a second in code, but only the
> first will ever run.

## Condition

| Menu | Type | Behavior |
|---|---|---|
| Boolean Condition | `BooleanCondition` | Returns `SUCCESS` while the input boolean is true, `FAILURE` otherwise |

## Add Conditional Execution

| Menu | Type | Behavior |
|---|---|---|
| Boolean Conditional | `BooleanConditionalExecution` | Only executes the node it guards while the input boolean is true |

**A Conditional Execution is not a decorator.** It isn't parented to anything — it attaches to an owner node
as a *guard*, and it is re-evaluated on **every tick** while that node runs. If it turns false mid-branch,
the branch is aborted immediately. That difference is the whole reason to prefer it over a Condition for
interruptions — see [Best Practices](best-practices.md#prefer-conditional-executions-over-condition-nodes-for-interruptions).

Guards on the same owner are ANDed: attach several and the node runs only when all of them hold.

## Gameplay

| Menu | Type | Behavior |
|---|---|---|
| Wait | `WaitTime` | Waits `Time` seconds |
| Wait Range | `WaitTimeRandomRange` | Waits a random time between `minTime` and `maxTime` |
| Face Target | `FaceTarget` | Rotates the target transform to face `TransformTarget` |
| Run Behavior Tree Graph | `RunBehaviorTreeGraphNode` | Executes another Behavior Tree asset — see [Sub-Behavior Trees](sub-behavior-trees.md) |

## Logic

| Menu | Type | Behavior |
|---|---|---|
| Not | `Not` | Outputs the inverse of `Value` |
| Is Not Null | `IsNotNull` | Outputs true when `Value` references a live object |

## Unity

### Animation

| Menu | Behavior |
|---|---|
| Set Animator Value | Sets a value in the animator — int, float or bool |
| Set Animator Trigger | Sets `triggerName` in the animator |
| Cross Fade Animation | Cross-fades to `StateName` |

### Transform

| Menu | Behavior |
|---|---|
| Set Position | Sets the transform's position |
| Look At | Rotates `Target` to look at `LookTarget` |
| Rotate | Spins the transform around `Axis` at `Speed` degrees per second; runs until something stops it |
| Set Rotation | Turns the transform to `TargetRotation` over `Duration`, then succeeds |

> `Rotate` keeps spinning and never finishes on its own, so put it under something that will stop it — a
> guard, or a sibling that succeeds. `Set Rotation` is the one that ends. A `Duration` of `0` means "face
> there now": it snaps to the target and succeeds on the first tick.

### Physics

| Menu | Behavior |
|---|---|
| Add Force | Applies physics force to the target Rigidbody |
| Add Torque | Applies torque to the target Rigidbody |
| Add Explosive Force | Applies a force simulating an explosion to the target Rigidbody |

### Navigation

| Menu | Behavior |
|---|---|
| Set NavAgent Position | Updates the nav agent's destination |
| Generate Random Navmesh Position | Generates a random NavMesh position; returns `SUCCESS`/`FAILURE` depending on whether one was found. `SampleDistance` is how far from the random point it will look for navmesh — the default of `1` suits human-scale agents, and `0` can never hit anything |
| Wait Until Reach Nav Target Position | Runs until the agent reaches its destination |
| Stop NavAgent | Stops the nav agent |

### Game Object

| Menu | Behavior |
|---|---|
| Instante Object | Clones the original object *(the menu label has a typo in the source; the node is `InstantiateObject`)* |
| Destroy Object | Destroys the input GameObject, component or asset |
| Find Game Object | Finds a GameObject in the scene |
| Dont Destroy On Load | Keeps the target object alive across scene loads |

### Variables

| Menu | Behavior |
|---|---|
| Get Variable | Reads a variable by key |
| Set Variable | Writes a variable by key |
| Remove Variable | Removes a variable by key |

Each of these takes a **key** (the variable's name) and a **Variable Kind** (which store to look in). The
key goes on a port, so it can be typed inline or driven by a graph; the kind is a dropdown on the node.

| Variable Kind | Where the value lives |
|---|---|
| `Graph` | The running tree, through the calling chain — a branch sees its own values first, then its caller's, out to the agent. A branch's writes stay in the branch, which is what makes one safe to reuse across unrelated agents. |
| `Object` | The agent's own `Variables` component. Facts every branch on that agent can see, and the ones reactive guards watch. |
| `Scene` | Everything in the active scene. |
| `Application` | Shared across scenes, reset when the application quits. |
| `Saved` | Outlives the application. Unity object references are not supported. |

**A new node starts with no store chosen**, and says so: the canvas marks it with a problem, and running it
throws naming the node rather than quietly reading nothing. Pick a kind, the same way you would fill in the
key. `Generate Random Navmesh Position` carries the same field for the position it writes.

> Older trees may contain a node whose store was `Flow` — a Visual Scripting kind a behavior tree could
> never serve, and the value nodes used to default to. Those load as unset and light up on the canvas.
> Pick the store the node was always meant to use.

### Literal

Literals exist to give a port a typed, inspector-editable value. They are the standard way to fill a port
that declares no default (see below).

`Boolean` · `Float` · `Integer` · `String` · `Vector2` · `Vector3` · `Variable Key` ·
`This/GameObject` · `This/Transform`

### Math

`Sum (A + B)` · `Subtract (A - B)` · `Multiply (A x B)` · `Divide (A / B)` · `Modulo (A % B)`

### Logs

`Debug Log` · `Debug Log Warning` · `Debug Log Error`

### Other

| Menu | Behavior |
|---|---|
| Play Audio | Plays an audio clip |

### Visual Scripting

| Menu | Behavior |
|---|---|
| Script Graph | A node whose lifecycle hooks (`OnAwake`, `OnEnter`, `OnUpdate`, `OnExit`) are Functions |
| Script Graph Variable | Returns a value from a Script Graph — the bridge for feeding ports, see [Ports and Wiring](ports-and-wiring.md) |

---

## Ports you must connect

Most ports declare a default, so leaving them unconnected is fine. A few declare none, and reading one that
is both unconnected and defaultless **throws at runtime** rather than falling back to zero.

Verified ports with no default:

| Node | Ports |
|---|---|
| `WaitTime` | `Time` |
| `WaitTimeRandomRange` | `minTime`, `maxTime` |
| `SetNavAgentPosition` | `NavPosition` |
| `FaceTarget` | `TransformTarget`, `RotationOffset`, `RotationSpeed`, `AcceptableRotation` |
| `GetVariable` | `Key` |
| `SetVariable` | `Key`, `Value` |

**`Target` ports are the exception.** On `SetNavAgentPosition`, `StopNavAgent` and
`WaitUntilReachNavTargetPosition`, `Target` is read through `GetComponent<T>(port)`, which falls back to the
machine's own GameObject when nothing is connected. Leaving those empty is normal and means "me".

To fill a defaultless port, connect a **Literal** node of the matching type. If you are generating trees from
code there is an extra trap here — see
[Authoring From Code](authoring-from-code.md#3-inline-values-dont-survive-a-reload-on-a-defaultless-port).

---

## See also

- [Custom Nodes](custom-nodes.md) — when none of the above fits
- [API Reference](api-reference.md) — `ExecutionStatus` and the node lifecycle
