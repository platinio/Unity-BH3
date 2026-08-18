# API Reference

The runtime types you interact with from C#.

---

## `ExecutionStatus`

```csharp
ExecutionStatus.None       // no status yet
ExecutionStatus.Inactive   // the node has not started
ExecutionStatus.Failure    // the node failed
ExecutionStatus.Success    // the node completed successfully
ExecutionStatus.Running    // the node is still working
ExecutionStatus.Exception  // the node threw
```

In practice a node returns `Success`, `Failure` or `Running` from `OnUpdate`. The other three are states the
runtime sets — you read them, you don't usually return them.

---

## Node lifecycle

```csharp
public override void OnAwake()              // once, when the machine loads the graph
public override void OnEnter()              // when the node starts
public override ExecutionStatus OnUpdate()  // every tick while running
public override void OnExit()               // when the node ends
```

Return `Running` from `OnUpdate` for anything spanning more than one frame.

Declare ports in `Definition()`, always calling `base.Definition()` first:

```csharp
protected override void Definition()
{
    base.Definition();

    // an input with a default -- safe to leave unconnected
    Speed = ValueInput<float>(nameof(Speed), 3.5f);

    // an input with no default -- MUST be connected, or reading it throws
    Target = ValueInput<Transform>(nameof(Target));

    // an output -- the delegate is called on demand, never cached
    Distance = ValueOutput<float>(nameof(Distance), () => ComputeDistance());
}
```

Reading a port at runtime:

```csharp
float speed = Speed.GetValue<float>();
```

`GetValue<T>()` converts whenever the wired value is not already a `T` — the canvas connects convertible
types, not just identical ones, so an `int` output on a `float` port is legal and has to be readable.
Casting `GetValue()` instead throws on exactly those connections. `GetValueOrDefault<T>()` is the variant
that answers `default` rather than throwing when the value cannot be used. See
[What "compatible" means](ports-and-wiring.md#what-compatible-means).

### Node metadata

```csharp
public override string NodeName    => "Move To Target";  // the canvas label
public override string Description => "Moves toward Target at Speed.";  // shown in the Graph Inspector
public override int MaxChildrenLimit => 1;               // required on decorators
```

`[GraphCreateMenu("Category/Node Name")]` places the node in the canvas right-click menu.

---

## `BehaviorTreeMachine`

The component that runs a tree. It loads the graph on `Awake`, starts on `Start`, and ticks every frame in
`Update`.

```csharp
// Swap the running tree
void Switch(BehaviorTreeGraphAsset behaviorTreeGraphAsset);

// What the tree returned last frame
ExecutionStatus LastExecutionStatus { get; }

// The runtime clone actually being executed
BehaviorTreeGraphAsset GraphInstance { get; }

// The asset assigned in the inspector
BehaviorTreeGraphAsset GraphAsset { get; }

// The asset originally assigned, before any Switch
BehaviorTreeGraphAsset OriginalMacro { get; }

// The recording used by the Why panel
BehaviorTreeFlightRecorder FlightRecorder { get; }
void SetFlightRecorder(BehaviorTreeFlightRecorder recorder);
```

> `BehaviorTreeMachine` requires a `Variables` component; Unity adds one automatically if missing.

### Switching trees at runtime

```csharp
using ArcaneOnyx.BehaviorTree;
using UnityEngine;

public class EnemyController : MonoBehaviour
{
    [SerializeField] private BehaviorTreeMachine behaviorTreeMachine;
    [SerializeField] private BehaviorTreeGraphAsset patrolTree;
    [SerializeField] private BehaviorTreeGraphAsset combatTree;

    public void OnPlayerSpotted()
    {
        behaviorTreeMachine.Switch(combatTree);
    }

    public void OnPlayerLost()
    {
        behaviorTreeMachine.Switch(patrolTree);
    }
}
```

> Switching whole trees is a blunt instrument. Before reaching for it, consider whether a guarded branch
> inside one tree expresses the same thing — that keeps the transition visible on the canvas and explainable
> by [The Why Panel](why-panel.md). See
> [Best Practices](best-practices.md#give-each-branch-its-own-tree-asset).

#### What a switch does

`Switch` puts the old tree down and brings the new one up, in that order. Concretely:

1. **The outgoing branch is stopped.** Whatever was running receives its `OnExit`, so a node gets the chance
   to undo what it started — stop a `NavMeshAgent`, end an animation, release a claim. A tree that is merely
   abandoned never gets this, and the agent keeps acting on decisions made by a tree it is no longer running.
2. **The outgoing tree is destroyed.** It was a per-agent clone and nothing else refers to it.
3. **The incoming asset is instantiated.** The agent runs a private copy, exactly as it does for the tree
   assigned in the inspector — so two agents switched to the same asset do not share node state, and the
   asset on disk never accumulates runtime state.
4. **The new tree is awoken and entered.** `OnAwake` is what builds each composite's child list from the
   transitions and arms each guard onto its owner, so a switched-in tree honours its preconditions from its
   first tick.
5. **A halted agent is revived.** `Update` stops ticking once the root returns `Success` or `Failure`;
   switching clears that, which is what makes a one-shot tree handing over to the next phase work.

The machine enters the tree exactly once regardless of whether the switch happened before or after Unity's
`Start`, so calling `Switch` from `Start`, `OnEnable`, a coroutine or an event handler is all fine.

> **Do not call `Switch` from `Awake`.** Unity does not order `Awake` between components, so the call may
> land before the machine has set itself up. It is refused with an error rather than served, because
> serving it would corrupt the agent in a way that depends on component order. Use `Start` or later.

> `OriginalMacro` keeps reporting the asset assigned in the inspector — a switch does not rewrite it.

#### Exit graphs run on teardown too

A node's `OnExit` can be an author-written script graph (`On Exit Graph` on the Visual Scripting nodes).
Because a replaced *and* a destroyed tree are both now stopped properly, those graphs fire on a switch and
on agent destruction, not only when a branch finishes normally. An exit graph must not assume the agent's
other components are still alive — during destruction, the order between them is undefined.

---

## `BehaviorTreeGraphAsset`

The serialized tree. Three variable declaration lists, matching the Blackboard's Graph tab:

```csharp
VariableDeclarations declarations;          // the tree's own instance variables
VariableDeclarations requiredDeclarations;  // parameters callers must pass
VariableDeclarations optionalDeclarations;  // parameters with a default
```

The last two become input ports on any `RunBehaviorTreeGraphNode` that runs this tree — see
[Sub-Behavior Trees](sub-behavior-trees.md#parameters).

> **Never touch `BehaviorTreeGraphAssetInstance` or `BehaviorTreeGraphInstance` from editor or tooling
> code.** Both call `Object.Instantiate` on first access, so merely inspecting one clones the asset. Use
> `BehaviorTreeGraphAsset`, which is the serialized reference.

---

## `RunBehaviorTreeGraphNode`

```csharp
void SetBehaviorTreeGraphAsset(BehaviorTreeGraphAsset asset);
BehaviorTreeGraphAsset BehaviorTreeGraphAsset { get; }
IReadOnlyList<BehaviorTreeGraphParameter> Parameters { get; }

// Rebuild the parameter ports from the sub-tree's current declarations
void RefreshParameters();

// How the remembered contract differs from the sub-tree's current one; empty when they agree
List<string> DescribeContractDrift();

// Throws if this node's tree can reach itself, reporting the path (A -> B -> A)
void ThrowIfCausesRecursion();
```

---

## `ConditionalExecution`

```csharp
public abstract bool Evaluate();               // your guard logic
void UpdateOwner(BehaviorTreeNode owner);      // the node this guard protects
BehaviorTreeNode Owner { get; }
```

Guards are **not** parented — they name an owner, and the runtime attaches them when the graph awakes.
Several guards on one owner are ANDed.

> `UpdateOwner` records intent; it does not arm the guard. The link is made during graph awake, so a guard is
> inert until the machine loads the graph. Editor tooling that needs guard *behaviour* must awake the graph
> first.

---

## See also

- [Custom Nodes](custom-nodes.md) — the lifecycle in practice
- [Authoring From Code](authoring-from-code.md) — building trees programmatically
