# Custom Nodes

Writing your own actions, conditions and decorators.

---

## Before you write one

Stop and ask whether the node is genuinely reusable.

- **Reusable across agents?** Write a C# node. That's this page.
- **Specific to one situation?** Use a **Script Graph Variable** or a **Script Graph** node instead — no code,
  no new type to maintain. See [Ports and Wiring](ports-and-wiring.md#feeding-a-port-from-visual-scripting).
- **Would that graph be large and hard to read?** Then a C# node is the clearer choice after all.

And keep fetching separate from acting. A node that *gets* something exposes a `ValueOutput`; a node that
*does* something exposes a `ValueInput`. One node doing both is the pattern BH3 exists to avoid — see
[Best Practices](best-practices.md#fetching-information-and-game-logic-are-two-separate-puzzle-pieces).

---

## The lifecycle

```csharp
public override void OnAwake()              // once, when the machine loads the graph
public override void OnEnter()              // when the node starts
public override ExecutionStatus OnUpdate()  // every tick while running
public override void OnExit()               // when the node ends
```

Return `Running` from `OnUpdate` for anything that needs more than one frame. See
[API Reference](api-reference.md) for the full `ExecutionStatus` set.

Declare ports in `Definition()`, **always calling `base.Definition()` first**.

`[GraphCreateMenu("Category/Node Name")]` controls where the node appears in the canvas right-click menu.
The type must be concrete with a public parameterless constructor.

---

## A custom action

Derive from `GameplayNode`:

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
    public override string Description => "Applies Damage to the Target's health component.";

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

`Description` is optional but worth setting — it shows in the Graph Inspector when the node is selected.

Note that `Target` declares **no default**, so it must be connected; `Damage` declares `10f`, so it need not
be. That choice is part of your node's contract.

## A custom condition

Derive from `Condition` and implement `Evaluate()`:

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

## A custom conditional execution (guard)

Derive from `ConditionalExecution` and implement `Evaluate()`. Unlike a Condition, this attaches to an owner
node as a guard and is re-checked every tick — if it turns false mid-branch, the branch aborts.

```csharp
[GraphCreateMenu("Add Conditional Execution/Has Ammo")]
public class HasAmmoConditionalExecution : ConditionalExecution
{
    public override string NodeName => "Has Ammo";
    public override string Description => "Runs the guarded node only while the weapon has ammo.";

    public override bool Evaluate()
    {
        var weapon = gameObject.GetComponent<WeaponComponent>();
        return weapon != null && weapon.Ammo > 0;
    }
}
```

For simple cases you don't need a type at all — use the built-in **Boolean Conditional** and wire any boolean
source into its port.

## A custom decorator

Derive from `Decorator`. A decorator wraps exactly one child, so declare that:

```csharp
[GraphCreateMenu("Decorator/Create Max Attempts")]
public class MaxAttempts : Decorator
{
    [DoNotSerialize] public ValueInput Attempts { get; private set; }

    public override string NodeName => "Max Attempts";
    public override string Description =>
        "Runs the child until it succeeds, giving up after Attempts failures.";

    public override int MaxChildrenLimit => 1;

    private int used;

    protected override void Definition()
    {
        base.Definition();
        Attempts = ValueInput<int>(nameof(Attempts), 3);
    }

    public override void OnEnter()
    {
        used = 0;
    }

    public override ExecutionStatus OnUpdate()
    {
        if (GetChildren().Count == 0) return ExecutionStatus.Success;

        var child = GetChildren()[0];
        var result = TickChild(child);

        if (result == ExecutionStatus.Running) return ExecutionStatus.Running;
        if (result == ExecutionStatus.Success) return ExecutionStatus.Success;

        used++;
        if (used >= Attempts.GetValue<int>()) return ExecutionStatus.Failure;

        child.OnNodeExit();
        return ExecutionStatus.Running;
    }
}
```

> **Tick children through `TickChild`, never `OnUpdateInternal` directly.** It enters the child first if the
> child is not already running, and that is the only thing standing between you and a node whose `OnUpdate`
> runs without its `OnEnter`. An entry can be *refused* — a guard on the child says no and it never starts —
> and a decorator that entered its child once and then ticked it every frame afterwards has no way to notice.
> The guard turns true a frame later, the tick sails through, and a `WaitTime` counts down a timer it never
> set and reports that the wait elapsed. A convention test fails the build if a new container calls
> `OnUpdateInternal` directly.
>
> The same rule is why the restart above is `child.OnNodeExit()` alone. Re-entering the child here would
> decide *this child may start* on one frame and act on that decision on the next; leaving it to the next
> `TickChild` asks the guard at the moment the answer is used. It costs nothing — the child's next `OnUpdate`
> lands on the following frame either way — and it means `OnEnter` never runs for a child that will not run.

> **`MaxChildrenLimit => 1` is required on a decorator.** The architecture tests enforce the declaration, and
> without it a second child can be attached that will never run.

Before writing a decorator, check the [Node Reference](node-reference.md#decorator) — `Cooldown`,
`RandomChance`, `Repeater`, `UntilSuccess`, `UntilFailure`, `ReturnSuccess` and `ReturnFailure` already ship.

---

## See also

- [Ports and Wiring](ports-and-wiring.md) — declaring and reading ports in depth
- [API Reference](api-reference.md) — the runtime contract
- [Authoring From Code](authoring-from-code.md) — generating whole trees programmatically
