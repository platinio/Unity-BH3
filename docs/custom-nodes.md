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
public override void OnAwake(BTContext ctx)              // once, when the machine loads the graph
public override void OnEnter(BTContext ctx)              // when the node starts
public override ExecutionStatus OnUpdate(BTContext ctx)  // every tick while running
public override void OnExit(BTContext ctx)               // when the node ends
```

Return `Running` from `OnUpdate` for anything that needs more than one frame. See
[API Reference](api-reference.md) for the full `ExecutionStatus` set.

Declare ports in `Definition()`, **always calling `base.Definition()` first**.

`[GraphCreateMenu("Category/Node Name")]` controls where the node appears in the canvas right-click menu.
The type must be concrete with a public parameterless constructor.

### `ctx` is where the agent lives

Everything your node knows about the agent it is running on comes from `ctx`: `ctx.gameObject`,
`ctx.Machine`, `ctx.GetValue<T>(port)`, `ctx.TryResolve<T>(port, out var c)`, and — most importantly —
`ctx.Memory<T>()`, covered next.

There are older parameterless versions of all four hooks (`OnEnter()` with no argument, and so on). They
still work, and nodes written against them keep running untouched, but **don't write new ones**. They exist
only so that nodes written before `BTContext` existed did not all have to change at once, and every
remaining use of them is work the shared-tree refactor has to undo by hand.

### Per-agent state goes in `ctx.Memory<T>()`, never in a field

This is the one rule worth internalising, because the C# habit points the wrong way.

A node that needs to remember something between `OnEnter` and `OnUpdate` — a timer, a counter, a component
it resolved at entry — must **not** put it in an instance field:

```csharp
private float timer;   // WRONG. This is the field the shared-tree refactor cannot move for you.
```

Declare a small class instead, and read it through the context:

```csharp
private sealed class Memory
{
    public float Elapsed;
}

public override void OnEnter(BTContext ctx) => ctx.Memory<Memory>().Elapsed = 0f;

public override ExecutionStatus OnUpdate(BTContext ctx)
{
    var memory = ctx.Memory<Memory>();
    memory.Elapsed += Time.deltaTime;
    return memory.Elapsed < ctx.GetValue<float>(Duration) ? ExecutionStatus.Running : ExecutionStatus.Success;
}
```

One memory class per node, holding every field that node needs; asking for a second type throws.

The reason is what BH3 is becoming. Today the machine deep-clones the whole tree for every agent, which is
what makes a field on a node private to one agent — and what makes spawning two hundred agents expensive.
That clone is going away in favour of one shared tree plus a small per-agent state block. On that day a
field on a node is a field *every* agent writes, and the two hundred of them overwrite each other. State in
`ctx.Memory<T>()` moves across untouched; state in a field has to be found and rewritten by hand.

**Fields that hold authored settings are fine** and are not flagged — those are one value for every agent
running the tree, which is exactly what a shared node should carry:

```csharp
[Serialize, Inspectable] private bool ClearCurrentPath = false;   // fine: a setting, not state
```

> **Don't reach for `[Serialize]` to make a field look legitimate.** Whether a field is serialized says
> nothing about whether your node writes to it while it runs, so marking a timer `[Serialize]` makes the
> underlying problem worse. A serialized field is part of the *shared* tree, so after the instancing change
> it is one value every agent writes in turn, which is the bug the rule exists to prevent, now with nothing
> left to report it. Ask which of the two a field is: something a designer sets and the node only reads, or
> something the node writes while it runs. The second belongs in `ctx.Memory<T>()` whatever attributes it
> carries.

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

    public override void OnEnter(BTContext ctx)
    {
        var target = ctx.GetValue<GameObject>(Target);
        var damage = ctx.GetValue<float>(Damage);
        target?.GetComponent<HealthComponent>()?.TakeDamage(damage);
    }

    public override ExecutionStatus OnUpdate(BTContext ctx) => ExecutionStatus.Success;
}
```

`Description` is optional but worth setting — it shows in the Graph Inspector when the node is selected.

Read every port with `ctx.GetValue<T>(port)` at the type the port declares, as above. Casting an untyped
`GetValue()` compiles and then throws on any connection whose types merely convert — see
[What "compatible" means](ports-and-wiring.md#what-compatible-means).

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
[GraphCreateMenu("Condition/Has Ammo")]
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

> **Conditions and guards do not take a context yet.** `Evaluate()` has no `BTContext` overload, so these two
> node families still reach the agent through `gameObject` and read ports directly, as shown above. That is a
> known gap rather than a recommendation — they will get the same treatment as the lifecycle hooks in a later
> release. Until then, keep `Evaluate()` bodies free of remembered state for the same reason the lifecycle
> hooks avoid instance fields: whatever you store there is state the shared-tree refactor will have to move.

## A custom decorator

Derive from `Decorator`. A decorator wraps exactly one child, so declare that:

```csharp
[GraphCreateMenu("Decorator/Max Attempts")]
public class MaxAttempts : Decorator
{
    [DoNotSerialize] public ValueInput Attempts { get; private set; }

    public override string NodeName => "Max Attempts";
    public override string Description =>
        "Runs the child until it succeeds, giving up after Attempts failures.";

    public override int MaxChildrenLimit => 1;

    private sealed class Memory
    {
        public int Used;
    }

    protected override void Definition()
    {
        base.Definition();
        Attempts = ValueInput<int>(nameof(Attempts), 3);
    }

    public override void OnEnter(BTContext ctx)
    {
        ctx.Memory<Memory>().Used = 0;
    }

    public override ExecutionStatus OnUpdate(BTContext ctx)
    {
        if (GetChildren().Count == 0) return ExecutionStatus.Success;

        var child = GetChildren()[0];
        var result = TickChild(child);

        if (result == ExecutionStatus.Running) return ExecutionStatus.Running;
        if (result == ExecutionStatus.Success) return ExecutionStatus.Success;

        var memory = ctx.Memory<Memory>();
        memory.Used++;
        if (memory.Used >= ctx.GetValue<int>(Attempts)) return ExecutionStatus.Failure;

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

## Renaming or deleting one later

A node type is not free to rename once trees reference it — the asset stores the type's full name, so
renaming the class, moving it between namespaces, or deleting it leaves every tree that used it holding a
name nothing resolves.

Nothing is lost when that happens, and the fix for a rename is one attribute:

```csharp
[RenamedFrom("ArcaneOnyx.BehaviorTree.TheOldName")]
```

See [Renaming and Deleting Node Types](renaming-and-deleting-nodes.md) for the whole picture, including how
to put a node back onto a different type and what carries over when you do.

---

## See also

- [Ports and Wiring](ports-and-wiring.md) — declaring and reading ports in depth
- [API Reference](api-reference.md) — the runtime contract
- [Renaming and Deleting Node Types](renaming-and-deleting-nodes.md) — retiring a node type without breaking trees
- [Authoring From Code](authoring-from-code.md) — generating whole trees programmatically
