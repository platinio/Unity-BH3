# Custom nodes

Writing your own actions, conditions, guards and decorators in C#.

---

## Before you write one

Ask whether the node is genuinely reusable.

- **Reusable across agents?** Write a C# node. That is this page.
- **Specific to one situation?** Use a **Script Graph Variable** running a [Function](../2-building-trees/08-functions.md),
  or a **Script Graph** node. No code, no new type to maintain.
- **Would that graph be large and hard to read?** Then a C# node is the clearer choice after all.

And keep fetching separate from acting. A node that *gets* something exposes a `ValueOutput`; a node that
*does* something exposes a `ValueInput`. One node doing both is the pattern BH3 exists to avoid. See
[Best practices](../2-building-trees/10-best-practices.md#fetching-information-and-acting-on-it-are-two-separate-pieces).

---

## The lifecycle

```csharp
public override void OnAwake(BTContext ctx)              // once, when the machine loads the graph
public override void OnEnter(BTContext ctx)              // when the node starts
public override ExecutionStatus OnUpdate(BTContext ctx)  // every tick while running
public override void OnExit(BTContext ctx)               // when the node ends, however it ends
```

Return `Running` from `OnUpdate` for anything that needs more than one frame. `OnUpdate` only ever runs on a
node that entered, so it may assume everything `OnEnter` set up. `OnExit` runs whether the node finished,
was aborted by a guard, or its agent was destroyed, so it is the place to undo what `OnEnter` started.

Declare ports in `Definition()`, **always calling `base.Definition()` first**.

`[GraphCreateMenu("Category/Node Name")]` puts the node in the canvas right-click menu. The type must be
concrete with a public parameterless constructor.

### One `using` trap

`ValueInput` and `ValueOutput` exist in **both** `ArcaneOnyx.BehaviorTree` and `Unity.VisualScripting`, and
a node file usually imports both (the second for `[DoNotSerialize]`). Outside BH3's own namespace the
compiler reports an ambiguous reference. Pick one of:

```csharp
using ValueInput  = ArcaneOnyx.BehaviorTree.ValueInput;    // alias the BH3 ones
using ValueOutput = ArcaneOnyx.BehaviorTree.ValueOutput;
```

or skip `using Unity.VisualScripting;` and write `[Unity.VisualScripting.DoNotSerialize]` in full. The
examples on this page use the alias.

> Older nodes override parameterless hooks (`OnEnter()` and so on). They still work, but do not write new
> ones: they cannot see `ctx`, and they are the ones a planned change to tree instancing will have to
> rewrite by hand.

### `ctx` is where the agent lives

Everything a node knows about the agent it is running on comes from `ctx`: `ctx.gameObject`,
`ctx.Machine`, `ctx.GetValue<T>(port)`, `ctx.GetComponent<T>()`, `ctx.TryResolve<T>(port, out var c)`,
and, most importantly, `ctx.Memory<T>()`. The full list is in the [API reference](02-api-reference.md#btcontext).

### Per-agent state goes in `ctx.Memory<T>()`, never in a field

A node that needs to remember something between `OnEnter` and `OnUpdate`, a timer, a counter, a component
it resolved at entry, must **not** put it in an instance field:

```csharp
private float timer;   // WRONG
```

Declare a small class instead and read it through the context:

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

Why: today the machine clones the whole tree for every agent, which is what makes a field on a node private
to one agent, and what makes spawning two hundred agents expensive. That clone is planned to give way to one
shared tree plus a small per-agent state block. On that day a field on a node is a field *every* agent
writes. State in `ctx.Memory<T>()` moves across untouched; state in a field has to be found and rewritten by
hand.

**Fields that hold authored settings are fine.** Those are one value for every agent running the tree, which
is exactly what a shared node should carry:

```csharp
[Serialize, Inspectable] private bool ClearCurrentPath = false;   // a setting, not state
```

Do not reach for `[Serialize]` to make a runtime field look legitimate. Ask which of the two a field is:
something a designer sets and the node only reads, or something the node writes while it runs. The second
belongs in `ctx.Memory<T>()` whatever attributes it carries.

---

## A custom action

Derive from `GameplayNode`:

```csharp
using ArcaneOnyx.BehaviorTree;
using ArcaneOnyx.GraphCore;
using Unity.VisualScripting;
using UnityEngine;
using ValueInput = ArcaneOnyx.BehaviorTree.ValueInput;

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

`Description` is optional but worth setting: it shows in the Graph Inspector when the node is selected.

Read every port with `ctx.GetValue<T>(port)` at the type the port declares. Casting an untyped `GetValue()`
compiles and then throws on any connection whose types merely convert. See
[What "compatible" means](../2-building-trees/03-ports-and-wiring.md#what-compatible-means).

`Target` declares **no default**, so it must be connected; `Damage` declares `10f`, so it need not be. That
choice is part of your node's contract, and the canvas shows a problem badge for an empty `Target`.

## A custom condition

Derive from `Condition` and implement `Evaluate()`. A Condition is a leaf: it returns `Success` while
`Evaluate` is true, `Failure` otherwise, and is only asked when execution reaches it.

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

## A custom guard

A guard attaches to an owner node and decides whether it may run. Which base class you derive from decides
how often it is asked:

| Derive from | Asked | Use for |
|---|---|---|
| `ConditionalExecution` | Once, at entry | A gate decided once: a random roll, an expensive query, a config flag |
| `ReactiveGuard` | At entry, then on its trigger schedule while the owner runs | Anything that should abort the branch, or take over a lower one, when the world changes |

Both implement `Evaluate()`. Everything else, the two switches, the trigger list, entry-always-fresh, the
take-over poll and the recorder events, comes from the base class.

```csharp
[GraphCreateMenu("Condition/Has Ammo")]
public class HasAmmoGuard : ReactiveGuard
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

A C# guard reads its own values, so nothing can derive what it watches: give it an **Every Interval**
trigger in the inspector, or list the keys it reads on an **On Key Changed** trigger, or it never wakes. And
keep `Evaluate` pure; a reactive guard runs while unrelated branches execute. See
[Writing a guard in C#](../2-building-trees/06-guards.md#writing-a-guard-in-c).

For simple cases you do not need a type at all: use the built-in **Reactive Guard** or **Conditional
Execution** and wire any boolean source into its port.

> **Conditions and guards do not take a context yet.** `Evaluate()` has no `BTContext` overload, so these
> two node families reach the agent through `gameObject` and read ports directly, as shown above. Keep
> `Evaluate()` bodies free of remembered state for the same reason the lifecycle hooks avoid instance fields.

## A custom decorator

Derive from `Decorator`. A decorator wraps exactly one child, and must say so:

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

**Tick children through `TickChild`, never `OnUpdateInternal` directly.** It enters the child first if the
child is not already running, and that is the only thing standing between you and a node whose `OnUpdate`
runs without its `OnEnter`. An entry can be *refused* by a guard on the child, and a decorator that ticks
its child every frame regardless would run a `Wait` that never set its timer. A convention test fails the
build if a new container calls `OnUpdateInternal` directly.

The same rule is why the restart above is `child.OnNodeExit()` alone: the next `TickChild` asks the guard at
the moment the answer is used.

**`MaxChildrenLimit => 1` is required on a decorator.** An architecture test enforces it, and the canvas
uses it to refuse a second wire.

Before writing a decorator, check the [Node reference](../2-building-trees/02-node-reference.md#decorator):
`Cooldown`, `RandomChance`, `Repeater`, `UntilSuccess`, `UntilFailure`, `ReturnSuccess` and `ReturnFailure`
already ship.

---

## Renaming or deleting one later

A node type is not free to rename once trees reference it: the asset stores the type's full name, so
renaming the class, moving it between namespaces, or deleting it leaves every tree that used it holding a
name nothing resolves. Nothing is lost when that happens, and the fix for a rename is one attribute:

```csharp
[RenamedFrom("ArcaneOnyx.BehaviorTree.TheOldName")]
```

See [Renaming and deleting node types](03-renaming-and-deleting-node-types.md).

---

## Next

- [API reference](02-api-reference.md) — the runtime contract
- [Ports and wiring](../2-building-trees/03-ports-and-wiring.md#ports-in-c) — declaring and reading ports in depth
- [Authoring from code](04-authoring-from-code.md) — generating whole trees programmatically
