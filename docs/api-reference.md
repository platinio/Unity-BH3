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

`OnUpdate` only ever runs on a node that entered, so it may assume everything `OnEnter` set up. Entry is not
guaranteed: a guard on the node can turn it away, and a container retries a refused entry on a later tick
rather than ticking past it. If you write a container of your own, tick its children through
`ContainerNode.TickChild` — see [Custom Nodes](custom-nodes.md#a-custom-decorator).

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
