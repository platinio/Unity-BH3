# Behavior Trees - BH3

A visual node-based behavior tree system for designing AI and gameplay logic, built on top of Unity Visual Scripting.

---

## Overview

BH3 is a Behavior Tree implementation in Unity that heavily focuses on Visual Scripting. Visual Scripting enables the creation of custom nodes without writing a single line of code, which is perfect if you are an artist wanting to explore Behavior Trees. It also comes in handy for programmers when creating simple one-off nodes or making quick changes on the fly.

BH3 uses ports to keep your node logic separate from where it gets its data. A node doesn't care if the value comes from the Blackboard, a Visual Scripting graph, or a literal — it just reads the port. This removes the need to write custom code to keep the blackboard updated. Fetching data and operating on it become two independent pieces that you can arrange freely to create different behaviors.

---

## Setup

**1. Create a Behavior Tree Asset**

Right-click in the Project window → **Create → Visual Scripting → Behavior Tree**

This creates a `.asset` file that holds your tree definition.

**2. Open the Graph Editor**

Double-click the asset to open the visual editor. An **Entry** node is created automatically. This is the root of your tree and cannot be deleted.

**3. Build Your Tree**

Right-click the canvas to add nodes. Connect them by dragging from one node's output to another's input. Every tree must have at least one node connected to the Entry node.

**4. Add BehaviorTreeMachine to Your GameObject**

Add a **BehaviorTreeMachine** component to any GameObject in your scene. Drag your tree asset into the **Nest** field on the component.

> **Note:** `BehaviorTreeMachine` requires a `Variables` component. Unity adds it automatically if it's missing.

**5. Press Play**

The machine loads the graph on `Awake`, starts execution on `Start`, and ticks it every frame on `Update`. No additional code required.

---

## Node Types

### Composite Nodes

Composites have multiple children and control which ones execute.

| Node | Behavior |
|---|---|
| **Sequence** | Executes children left to right. Stops and returns `Failure` if any child fails. Succeeds only if all children succeed. |
| **Selector** | Executes children left to right. Stops and returns `Success` as soon as one child succeeds. Fails only if all children fail. |
| **Parallel** | Executes all children simultaneously every frame. Return policy depends on configuration. |
| **Random Sequence** | Same as Sequence but children execute in random order. |
| **Random Selector** | Same as Selector but children execute in random order. |
| **Parallel Sequence** | Runs all children in parallel, fails if any child fails. |

### Decorator Nodes

Decorators wrap a single child and modify how its result is interpreted or when it executes.

| Node | Behavior |
|---|---|
| **Repeater** | Loops the child indefinitely. Never returns `Failure`. |
| **Until Success** | Re-runs the child until it returns `Success`. |
| **Until Failure** | Re-runs the child until it returns `Failure`. |
| **Return Success** | Forces the child's result to `Success` regardless of outcome. |
| **Return Failure** | Forces the child's result to `Failure` regardless of outcome. |
| **Conditional Execution** | Skips the child entirely if the guard condition fails. |

### Action Nodes

Actions are leaf nodes that do work and return `Success`, `Failure`, or `Running`.

| Category | Nodes |
|---|---|
| **Transform** | `SetPosition`, `SetRotation`, `LookAt`, `Rotate` |
| **Animation** | `SetAnimatorValue`, `SetAnimatorTrigger`, `CrossFadeAnimation` |
| **Physics** | `AddForce`, `AddTorque`, `AddExplosiveForce` |
| **Navigation** | `GenerateRandomNavMeshPosition`, `StopNavAgent` |
| **GameObject** | `InstantiateObject`, `DestroyObject`, `DontDestroyOnLoad` |
| **Audio** | `PlayAudio` |
| **Flow** | `WaitTime`, `WaitTimeRandomRange` |
| **Nesting** | `RunBehaviorTreeGraphNode`, `RunScriptGraph` |
| **Debug** | `DebugLog`, `DebugLogWarning`, `DebugLogError` |

### Condition Nodes

Conditions evaluate a boolean and return `Success` if true, `Failure` if false. Use them inside Selectors or as guards on Decorators.

---

## Creating Custom Nodes

### Custom Action Node

Override `GameplayNode` for nodes that perform a single action:

```csharp
using ArcaneOnyx.BehaviorTree;
using ArcaneOnyx.GraphCore;
using Unity.VisualScripting;
using UnityEngine;

[GraphCreateMenu("Custom/Deal Damage")]
public class DealDamageNode : GameplayNode
{
    [DoNotSerialize] public ValueInput Target { get; private set; }
    [DoNotSerialize] public ValueInput Damage { get; private set; }

    public override string NodeName => "Deal Damage";

    protected override void Definition()
    {
        base.Definition();
        Target = ValueInput<GameObject>(nameof(Target));
        Damage = ValueInput<float>(nameof(Damage), 10f);
    }

    public override void OnEnter()
    {
        var target = Target.GetValue<GameObject>();
        var damage = Damage.GetValue<float>();
        target?.GetComponent<HealthComponent>()?.TakeDamage(damage);
    }

    public override ExecutionStatus OnUpdate() => ExecutionStatus.Success;
}
```

### Custom Condition Node

Override `Condition` and implement `Evaluate()`:

```csharp
[GraphCreateMenu("Custom/Is Health Low")]
public class IsHealthLowCondition : Condition
{
    [DoNotSerialize] public ValueInput Threshold { get; private set; }

    public override string NodeName => "Is Health Low";

    protected override void Definition()
    {
        base.Definition();
        Threshold = ValueInput<float>(nameof(Threshold), 30f);
    }

    public override bool Evaluate()
    {
        var health = gameObject.GetComponent<HealthComponent>();
        return health != null && health.CurrentHealth < Threshold.GetValue<float>();
    }
}
```

### Custom Decorator Node

Override `Decorator` to wrap a child node:

```csharp
[GraphCreateMenu("Custom/Cooldown")]
public class CooldownDecorator : Decorator
{
    [DoNotSerialize] public ValueInput Duration { get; private set; }

    private float cooldownTimer = 0f;

    public override string NodeName => "Cooldown";

    protected override void Definition()
    {
        base.Definition();
        Duration = ValueInput<float>(nameof(Duration), 5f);
    }

    public override ExecutionStatus OnUpdate()
    {
        if (cooldownTimer > 0f)
        {
            cooldownTimer -= Time.deltaTime;
            return ExecutionStatus.Failure;
        }

        var result = GetChildren()[0].OnUpdateInternal();

        if (result == ExecutionStatus.Success)
            cooldownTimer = Duration.GetValue<float>();

        return result;
    }
}
```

> **Note:** The `[GraphCreateMenu("Category/NodeName")]` attribute controls where the node appears in the editor's right-click menu.

---

## Switching Trees at Runtime

`BehaviorTreeMachine` exposes a `Switch` method that lets you swap the running tree at any point:

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

---

## Sub-Behavior Trees

Sub-Behavior Trees let you share parts of a behavior tree across multiple AI instances to avoid duplication. For example, in an FPS game different enemies may have different behaviors but all share the same shooting logic. You can encapsulate that logic in a single tree and reuse it everywhere using **Gameplay/Run Behavior Tree Graph**.

Changes made to a shared tree asset are reflected across all instances that reference it once the game enters play mode.

---

## API Reference

### `BehaviorTreeMachine`

```csharp
// Swap the running tree at runtime
behaviorTreeMachine.Switch(BehaviorTreeGraphAsset newTree);

// Check what the tree returned last frame
ExecutionStatus status = behaviorTreeMachine.LastExecutionStatus;
```

### Node Lifecycle

```csharp
public override void OnAwake() { }                    // Called once on load
public override void OnEnter() { }                    // Called when node starts
public override ExecutionStatus OnUpdate() { }        // Called every frame
public override void OnExit() { }                     // Called when node ends
```

### `ExecutionStatus`

```csharp
ExecutionStatus.Success   // Node completed successfully
ExecutionStatus.Failure   // Node failed
ExecutionStatus.Running   // Node is still working
ExecutionStatus.Inactive  // Node has not started yet
```

### Ports

```csharp
// Define inputs and outputs inside Definition()
MyInput  = ValueInput<float>(nameof(MyInput), 1.0f);
MyOutput = ValueOutput<float>(nameof(MyOutput), () => someValue);

// Read a connected value at runtime
float value = MyInput.GetValue<float>();
```

---

## Best Practices

### Fetching Information and Game Logic are Two Separate Puzzle Pieces

Traditionally, when we create Behavior Tree Nodes fetching the information and game logic form part of the same node:

```csharp
public class SetNavAgentPosition : BehaviorTreeNode
{
    public string targetPositionKey;

    private ExecutionStatus OnUpdate()
    {
        Vector3 targetPosition = blackboard.Get<Vector3>(targetPositionKey);
        navAgent.SetNavAgentPosition(targetPosition);
    }
}
```

This node is hard to reuse. Something else must constantly update the blackboard before it can run, and designers can't change what it reads without a developer's help. The better approach is to split the two responsibilities into separate nodes.

```csharp
public class GetEscapePosition : GameplayNode
{
    [DoNotSerialize] public ValueOutput Value { get; private set; }

    public override string NodeName => "Get Escape Position";

    protected override void Definition()
    {
        base.Definition();
        Value = ValueOutput<Vector3>(nameof(Value), () => CalculateEscapePosition());
    }

    private Vector3 CalculateEscapePosition()
    {
        // logic to calculate escape position
    }
}
```

```csharp
public class SetNavAgentPosition : GameplayNode
{
    [DoNotSerialize] public ValueInput NavPosition { get; private set; }

    protected override void Definition()
    {
        base.Definition();
        NavPosition = ValueInput<Vector3>(nameof(NavPosition));
    }

    private ExecutionStatus OnUpdate()
    {
        Vector3 pos = (Vector3)NavPosition.GetValue();
        navAgent.SetNavAgentPosition(pos);
    }
}
```

Now each node does one thing. `SetNavAgentPosition` works with any `Vector3` source, and `GetEscapePosition` can feed into any node that needs a position. Designers mix and match without touching code.

---

### We Have Conditionals but Try First ConditionalExecutions

If you want to prematurely end an action because the AI has something more important to do, like attacking an enemy while in the middle of an idle action, we can use Conditional nodes. But try first `ConditionalExecution` nodes, which will mark an entire behavior tree branch as failed based on custom logic.

---

### Think of Behavior Tree Branches Like a Function that Does Just One Thing

Each branch does exactly one job and knows nothing about the others. The Patrol branch doesn't care about enemies, only a `ConditionalExecution` decides when to interrupt it.

Once your branches are that focused, you can share them across different AI using Sub-Behavior Trees. Designers then build new enemies by picking existing branches and assigning priorities, creating the illusion of unique behavior from reusable pieces.
