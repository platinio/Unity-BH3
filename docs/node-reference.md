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
| Rotate | Rotates the transform to a target rotation over a given duration |

> **Known issue:** `Rotate` and `SetRotation` both register under `Unity/Transform/Rotate`, so only one of
> them is reachable from the create menu. Reference the types directly if you need the other from code.

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
| Generate Random Navmesh Position | Generates a random NavMesh position; returns `SUCCESS`/`FAILURE` depending on whether one was found |
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
| Script Graph | A node whose lifecycle hooks (`OnAwake`, `OnEnter`, `OnUpdate`, `OnExit`) are Visual Scripting graphs |
| Run Script Graph | Executes a `ScriptGraphAsset` on enter |
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
