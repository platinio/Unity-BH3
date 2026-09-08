# API reference

The runtime types you interact with from C#.

---

## `ExecutionStatus`

Defined in GraphCore.

```csharp
ExecutionStatus.None       // no status yet
ExecutionStatus.Inactive   // the node has not started
ExecutionStatus.Failure    // the node failed
ExecutionStatus.Success    // the node completed successfully
ExecutionStatus.Running    // the node is still working
ExecutionStatus.Exception  // the node threw
```

A node returns `Success`, `Failure` or `Running` from `OnUpdate`. The other three are states the runtime
sets; you read them, you do not return them.

---

## Node lifecycle

```csharp
public override void OnAwake(BTContext ctx)              // once, when the machine loads the graph
public override void OnEnter(BTContext ctx)              // when the node starts
public override ExecutionStatus OnUpdate(BTContext ctx)  // every tick while running
public override void OnExit(BTContext ctx)               // when the node ends
```

Each hook also has an older parameterless form (`OnEnter()` and so on). Nodes written against those keep
working, but new nodes must use the context overloads. `Condition.Evaluate()` and
`ConditionalExecution.Evaluate()` have no context overload yet.

**`OnUpdate` only ever runs on a node that entered**, so it may assume everything `OnEnter` set up. Entry
is not guaranteed, in two ways: a guard on the node can turn it away at the door, and if `OnEnter` throws
the node has not entered either, its status becomes `Exception`, and it is left not-running. In both cases
the parent retries the entry on a later tick rather than ticking past it. A node that failed to enter is not
exited either. A container that had already entered some children when a later one threw does exit those.

If you write a container of your own, tick its children through `ContainerNode.TickChild`. See
[A custom decorator](01-custom-nodes.md#a-custom-decorator).

### `BTContext`

`ctx` is everything the node's body knows about the agent it is running on:

| Member | What it gives you |
|---|---|
| `ctx.Memory<T>()` | This node's per-agent state, created on first use. **Where a node's fields belong.** One type per node; asking for a second throws |
| `ctx.gameObject` / `ctx.transform` | The agent |
| `ctx.Machine` | The `BehaviorTreeMachine` running the tree, or null in an edit-mode test |
| `ctx.GetValue<T>(port)` | Reads a port at the type it declares, converting where the canvas would |
| `ctx.GetComponent<T>()` | A component on the agent |
| `ctx.GetComponent<T>(port)` | The component a port points at, falling back to the agent's own |
| `ctx.TryResolve<T>(port, out c)` | The same, reporting by name when it finds nothing |
| `ctx.GetTargetGameObject(variable)` | The GameObject a blackboard variable names |
| `ctx.VariableScope` | The variables this node can see, innermost sub-tree first |
| `ctx.ScriptGraphVariables` | The flattened scope chain handed to a script graph the node runs |
| `ctx.FlightRecorder` | Where the node reports what it did, or null when nothing is recording |

### Declaring and reading ports

```csharp
protected override void Definition()
{
    base.Definition();

    Speed    = ValueInput<float>(nameof(Speed), 3.5f);      // has a default: safe to leave unconnected
    Target   = ValueInput<Transform>(nameof(Target));       // no default: must be connected, or reading it throws
    Distance = ValueOutput<float>(nameof(Distance), () => ComputeDistance());   // delegate, called on demand
}
```

Every key must be unique across the node. If a subclass reuses a name its base class declared, the
duplicate declaration throws and the node ends up with no ports at all.

```csharp
float speed = ctx.GetValue<float>(Speed);      // converts int → float, GameObject → Transform, etc.
var target  = Target.GetValueOrDefault<Transform>();   // default instead of throwing on an unusable value
```

`GetValue<T>()` converts whenever the wired value is not already a `T`, because the canvas connects
convertible types, not only identical ones. Casting an untyped `GetValue()` throws on exactly those
connections. See [What "compatible" means](../2-building-trees/03-ports-and-wiring.md#what-compatible-means).

### Node metadata

```csharp
public override string NodeName    => "Move To Target";                 // the canvas label
public override string Description => "Moves toward Target at Speed.";  // shown in the Graph Inspector
public override int MaxChildrenLimit => 1;                              // required on decorators
```

`[GraphCreateMenu("Category/Node Name")]` places the node in the canvas right-click menu.

### Reporting problems

Override `CollectProblems(List<NodeProblem> problems)` to make a node mark itself on the canvas before
Play, with a message and optionally a one-click repair. A rule that lives outside the node can register a
provider with `NodeProblemCache.AddProvider` in editor code. See
[Checking your tree](../2-building-trees/09-checking-your-tree.md).

---

## `BehaviorTreeMachine`

The component that runs a tree. It requires a `Variables` component, which Unity adds automatically.

```csharp
void Switch(BehaviorTreeGraphAsset asset);       // swap the running tree
ExecutionStatus LastExecutionStatus { get; }     // what the tree returned last tick
bool HasFinished { get; }                        // root returned Success or Failure; ticking has stopped
BehaviorTreeGraph RunningGraph { get; }          // the graph being executed
BehaviorTreeGraphAsset GraphInstance { get; }    // the runtime clone actually being executed
BehaviorTreeGraphAsset GraphAsset { get; }       // the asset currently assigned
BehaviorTreeGraphAsset OriginalMacro { get; }    // the asset assigned in the inspector, before any Switch
BehaviorTreeFlightRecorder FlightRecorder { get; }
void SetFlightRecorder(BehaviorTreeFlightRecorder recorder);
```

### Lifecycle

| Unity callback | The machine |
|---|---|
| `Awake` | Binds `Variables`, sets the `This` variable to the agent, attaches the recorder, instantiates a private copy of the asset and awakes it (which builds each composite's child list and arms each guard onto its owner) |
| `Start` | Enters the tree |
| `Update` | Ticks the tree, unless `HasFinished` |
| `LateUpdate` / `FixedUpdate` | Forwarded to the tree, unless `HasFinished` |
| `OnDestroy` | Exits the tree, so every running node gets `OnExit`, then destroys the copy |

**Each agent runs its own copy of the asset.** Two agents on the same asset do not share node state, and
the asset on disk never accumulates runtime state.

**The machine stops ticking once the root returns `Success` or `Failure`.** That is what `HasFinished`
reports, and why every real tree has a `Repeater` under `Entry`.

### Switching trees at runtime

```csharp
using ArcaneOnyx.BehaviorTree;
using UnityEngine;

public class EnemyController : MonoBehaviour
{
    [SerializeField] private BehaviorTreeMachine behaviorTreeMachine;
    [SerializeField] private BehaviorTreeGraphAsset patrolTree;
    [SerializeField] private BehaviorTreeGraphAsset combatTree;

    public void OnPlayerSpotted() => behaviorTreeMachine.Switch(combatTree);
    public void OnPlayerLost()    => behaviorTreeMachine.Switch(patrolTree);
}
```

`Switch` puts the old tree down and brings the new one up, in that order:

1. **The outgoing branch is stopped.** Whatever was running receives `OnExit`, so a node can undo what it
   started: stop a `NavMeshAgent`, end an animation, release a claim.
2. **The outgoing copy is destroyed.**
3. **The incoming asset is instantiated**, as a private copy, exactly like the one assigned in the inspector.
4. **The new tree is awoken and entered**, so it honours its guards from its first tick.
5. **A halted agent is revived.** Switching clears `HasFinished`, which is what makes a one-shot tree handing
   over to the next phase work.

The machine enters the tree exactly once whether the switch happened before or after Unity's `Start`, so
calling `Switch` from `Start`, `OnEnable`, a coroutine or an event handler is all fine. **Do not call it
before the machine's own `Awake` has run**: Unity does not order `Awake` between components, so a call from
another component's `Awake` may arrive first. It is refused with an error rather than served. Use `Start` or
later.

`OriginalMacro` keeps reporting the asset assigned in the inspector; a switch does not rewrite it.

> Switching whole trees is a blunt instrument. Before reaching for it, consider whether a guarded branch
> inside one tree expresses the same thing: that keeps the transition visible on the canvas and explainable
> by the [Why panel](../3-debugging/03-why-panel.md).

---

## `BehaviorTreeGraphAsset`

The serialized tree. Three variable declaration lists, matching the Blackboard's Graph tab:

```csharp
VariableDeclarations declarations;          // the tree's own instance variables
VariableDeclarations requiredDeclarations;  // parameters callers must pass
VariableDeclarations optionalDeclarations;  // parameters with a default
```

The last two become input ports on any `RunBehaviorTreeGraphNode` that runs this tree.

> **Never touch `BehaviorTreeGraphAssetInstance` or `BehaviorTreeGraphInstance` from editor or tooling
> code.** Both call `Object.Instantiate` on first access, so merely inspecting one clones the asset. Use
> `BehaviorTreeGraphAsset`, which is the serialized reference.

---

## `RunBehaviorTreeGraphNode`

```csharp
void SetBehaviorTreeGraphAsset(BehaviorTreeGraphAsset asset);
BehaviorTreeGraphAsset BehaviorTreeGraphAsset { get; }
IReadOnlyList<BehaviorTreeGraphParameter> Parameters { get; }

List<string> RefreshParameters();      // rebuild the parameter ports; returns what changed
List<string> DescribeContractDrift();  // how the remembered contract differs from the sub-tree's current one
void ThrowIfCausesRecursion();         // throws with the path (A -> B -> A) if this tree can reach itself
```

---

## Guards

```csharp
// ConditionalExecution — asked once, at entry
public abstract bool Evaluate();
void UpdateOwner(BehaviorTreeNode owner);      // the node this guard protects
BehaviorTreeNode Owner { get; }

// ReactiveGuard : ConditionalExecution — asked on a schedule while the owner runs
bool StopsItsOwnBranch { get; }
bool TakesOverLowerPriority { get; }
IReadOnlyList<GuardTrigger> Triggers { get; }
void SetCapabilities(bool stopsItsOwnBranch, bool takesOverLowerPriority);
void AddTrigger(GuardTrigger trigger);         // GuardTrigger.KeyChanged(key), EveryInterval(seconds, deviation), EveryFrame()
List<string> RefreshWatchedKeys();
public virtual bool Ask(bool fresh);           // override to replace the dirty-flag path; honour fresh
```

Guards are **not** parented. They name an owner, and the runtime attaches them when the graph awakes.
`UpdateOwner` records intent; it does not arm the guard, so editor tooling that needs guard *behaviour*
must awake the graph first. Several guards on one owner are ANDed.

---

## Variables from C#

```csharp
// Publish an agent fact so guards wake and the recorder can name the writer
var writer = AgentVariableWriter.On(gameObject);
bool changed = writer.Write(this, "hasTarget", true);   // false when the value did not change

// Read one
object value = writer.Read("hasTarget");
```

See [Writing facts from a sensor](../2-building-trees/04-variables-and-scope.md#writing-facts-from-a-sensor).

## The recorder from C#

```csharp
BehaviorTreeFlightRecorders.GloballyEnabled          // the Rec switch
BehaviorTreeFlightRecorders.TracingGloballyEnabled   // guard-chain capture only
BehaviorTreeFlightRecorders.DefaultCapacity          // events per agent, default 2048; set before agents spawn
BehaviorTreeFlightRecorders.DefaultTraceCapacity     // guard chains per agent, default 64
BehaviorTreeRecordingDump.ToJson(recorder)           // the same JSON the Timeline's Save button writes
```

---

## Next

- [Custom nodes](01-custom-nodes.md) — the lifecycle in practice
- [Authoring from code](04-authoring-from-code.md) — building trees programmatically
