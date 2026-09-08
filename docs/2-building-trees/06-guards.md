# Guards

A guard is a precondition attached to a node. It decides whether the node may start, and, for one kind of
guard, whether it must stop. This page covers both kinds, when to use each, and the rules that keep them
cheap.

---

## Two kinds

| | Conditional Execution | Reactive Guard |
|---|---|---|
| Asks | *May I start?* | *Is this still true?* |
| Evaluated | Once, at entry | At entry, and again on a schedule while its owner runs |
| Gates entry | yes | yes |
| Aborts its own branch mid-run | no | **Stops Its Own Branch**, default on |
| Takes over from a lower-priority branch | no | **Takes Over Lower Priority**, default on |

The doorman checks once and stops caring. The watchman keeps looking, and can throw you out, or let someone
more important in ahead of you.

### Attaching one

Right-click the node to protect and choose **Condition → Conditional Execution** or
**Condition → Reactive Guard**. The guard appears beside that node as its **owner**. Feed its `Value` port
with any boolean source: a `Get Variable`, a `Not`, an `Is Not Null`, or a [Function](08-functions.md)
through a Script Graph Variable.

Guards do not go on **Entry**. Any other node takes them, including a **Run Behavior Tree Graph** node,
which is the usual place: branch in its own asset, guard at the call site.

Several guards on one owner are **ANDed**: the node runs only while all of them hold. There is no `And`
node and none is needed.

---

## Why reactive guards exist

Without them, the only way a branch could end early was its own guard turning false, so every branch had to
carry the negated preconditions of everything above it:

```
Selector
├── Attack   hasTarget AND targetInRange
├── Chase    hasTarget AND NOT targetInRange
└── Idle     NOT hasTarget AND NOT lowHP AND NOT ...
```

Every branch knew about every branch above it, and adding one meant editing all the others. With reactive
guards each branch states only its own precondition, and the fallback carries none:

```
Selector
├── Attack   Reactive Guard: targetInRange   (Takes Over Lower Priority on, Stops Its Own Branch off)
├── Chase    Reactive Guard: hasTarget       (both on)
└── Idle     no guard
```

When `targetInRange` turns true, Attack takes the slot from Chase **on the same frame**. Idle does not know
Attack or Chase exist.

### The two switches are independent

Turning **Stops Its Own Branch** off is a real authoring move, not a way to disable the guard. It is the
**committed swing**: Attack bids for control the moment the target comes into range, but once the animation
has started it finishes even if the target steps back out. That combination is unreachable with one switch,
which is why there are two.

### Where a take-over works

A take-over (`bt_verify` calls it preemption) is a contract on the composite, and each composite answers
differently:

- **Selector**: a higher-priority child becoming eligible takes over. Built.
- **Sequence**: its earlier children already succeeded, so a guard there would be a sustained requirement
  whose failure should fail the sequence. Specified, not yet built.
- **Parallel Selector**: no reaction. Children already run concurrently, so there is no slot to move.

A guard with **Takes Over Lower Priority** whose owner is not a direct child of a Selector still gates entry
and still aborts; it just has nobody to bid against. `bt_verify` says so.

**Guard the branch, not its steps.** To make a multi-step routine stop the moment a condition drops, put the
reactive guard on the thing that *is* the branch, the **Run Behavior Tree Graph** node or the composite,
rather than on one child inside it. Stopping the call node aborts the whole sub-tree, and the abort reaches
every node inside the instance.

### Mixing the two kinds on one node

This is legal and useful. Give Attack a Reactive Guard on `targetInRange` and a Conditional Execution on
`hasAttackToken`: the reactive guard is the trigger, the conditional is a veto. A take-over only fires when
**every** guard on the candidate passes, so a veto cannot evict the running branch and then fail the
candidate's own entry.

---

## When a Conditional Execution is the right answer

Entry-only gating is not a downgrade. It is the correct tool whenever re-evaluation would be wrong or
wasteful:

- **Random or one-shot gates.** `RandomChance` on a Taunt branch. A re-rolling guard would kill its branch
  within a tick.
- **Expensive one-shot validation.** "Is there a valid cover point from here?" A tactical position query is
  unthinkable per frame and correct at the door.
- **Config gates.** `difficulty >= Hard`, `isEliteVariant`. They never change; watching them is waste.

---

## Triggers: when a reactive guard may recompute

A reactive guard holds a list of triggers under **Recompute When**, ORed together. **A trigger does not
evaluate the guard. It marks it dirty, and the tick decides.** A guard nothing has marked dirty costs one
bool check instead of a graph run.

| Trigger | Use it for |
|---|---|
| **On Key Changed** | The default. An agent fact like `hasTarget`. Free when nothing changed |
| **Every Interval** (seconds ± deviation) | Continuous quantities with no "changed" event: a distance, an angle, a resource level. The deviation spreads many agents' checks across frames |
| **Every Frame** | The honest escape hatch. Say it out loud rather than leaving the list empty |

**An empty trigger list means every tick.** That is the most expensive thing a guard can do, so `bt_verify`
reports it: not because it is wrong, but so the cost is chosen rather than inherited.

### Entering a branch always recomputes

Entering ignores the dirty flag. A stale *false* only costs latency, but a stale *true* would enter a branch
whose precondition no longer holds: the animation starts, the token is claimed, and the abort has to unwind
it. Entries are rare next to ticks, so the extra evaluation is cheap insurance.

It recomputes once, though, not twice: the tick that follows an entry on the same frame reuses the entry
verdict rather than asking again. Whether a branch enters is one decision per frame.

### What On Key Changed can see

**Object variables only**, the agent's facts. A branch's own Graph variables are per-call-site scratch, and
a guard watching those would be watching its own noise. Use an interval for anything else.

A key only counts as changed when its **value** actually moved. A fact republished every frame to the same
value costs its watchers nothing.

Only writes that go through BH3 bump the key. `AgentVariableWriter.Write`, the **Set Variable** tree node
and the **Set BT Variable** unit do; Unity's stock **Set Variable** unit and a plain `Variables` write from
C# do not, and a guard reading such a key simply never wakes, with no error anywhere. See
[Variables and scope](04-variables-and-scope.md#which-writes-are-visible).

---

## Guards must be pure

**A guard may only read.** A reactive guard evaluates on its own schedule, including while completely
unrelated branches execute.

The tempting mistake is a graph that finds the nearest enemy, **caches it into `currentTarget`**, and
returns whether it is non-null. Under a reactive guard that overwrites `currentTarget` several times a
second while another branch is mid-swing using it.

*Find and cache the target* computes and writes: that is a sensor's job. *Is there a target* reads and
answers: that is a guard. `bt_verify` warns when a guard's graph contains a write, naming the guard and the
unit.

---

## Watched keys and Functions

A guard reads nothing itself. It pulls a boolean off a port, so every key in its **On Key Changed** trigger
is really a *claim* about what the thing feeding that port reads. A wrong claim is silent: the guard just
never wakes.

When the condition is a **Function**, the claim has a source. A Function declares the agent facts it reads,
and a guard whose condition reaches that Function picks them up automatically, at runtime, as a union with
any keys you typed by hand. Edit the Function to read a new fact and every guard referencing it wakes on it
from the next run. The walk goes backwards through the whole condition, so a `Not` between the guard and the
Function does not hide it.

Two things can still go wrong, and both are reported on the canvas and by `bt_verify`:

| Problem | What you see | Fix |
|---|---|---|
| **The guard's key list lags the Function.** When a guard is built or the tree is saved, the Function's declared keys are copied into the trigger so the schedule is visible on the asset. Add a key to the Function later and the guard still wakes on it at runtime, but the asset says otherwise | Amber badge on the guard naming the missing key | **Refresh Watched Keys**, in the guard's inspector, its right-click menu, or `bt_refresh_guard_keys`. It adds and never removes, because a hand-typed key is legitimate and indistinguishable from a copied one |
| **The Function reads a fact it never declared.** Adding a **Get BT Variable** unit to a Function does not change what it declares, and a guard inherits the *declaration*. The guard never wakes on the new fact and the branch quietly stops firing | The message below, on the Function-backed node and on any guard reading it | **Declare 'stamina' on IsHurt**, in the inspector; it confirms first, because the Function is shared |

The second one reads:

> This guard's condition reads 'stamina' without declaring it, so the guard never wakes on it and its
> branch can stop firing with nothing to point at.

The repair is on the Function, which is why the button confirms first: declaring a key changes behaviour in
every tree that uses that Function. From the command line, `fn_set_metadata --watched_keys hp,stamina`.

**Inheritance never creates a trigger.** A guard with no trigger recomputes every tick and keeps doing so,
even when its condition declares keys, because a condition can also depend on things no key can express, a
raycast or a timer. `bt_guard_on_function` seeds the trigger when it builds the guard; for a guard built by
hand, add the **On Key Changed** trigger yourself. `bt_verify` names any guard that has no trigger while its
condition declares keys.

---

## Reading what happened

A take-over is recorded as its own event, distinct from an abort. *"Your precondition stopped holding"* and
*"something more important wanted the slot"* are different answers, and the [Why panel](../3-debugging/03-why-panel.md)
gives them different headlines. When a guard flips, the values on every wire feeding it are captured, so the
panel can show exactly what it was looking at.

---

## Writing a guard in C#

For programmers. It assumes [Custom nodes](../4-extending-with-csharp/01-custom-nodes.md); designers can
stop here.

A Visual Scripting condition costs a graph run per evaluation. For something checked per agent per frame, a
distance, an angle, a cone of vision, write the guard in C# and pay a method call.

`ReactiveGuard` is abstract for this reason: it owns the switches, the trigger list, the dirty-flag caching
and the take-over behaviour, and leaves only *how do I get my boolean* to the subclass. `BooleanReactiveGuard`
is just the version that reads a port.

```csharp
using ArcaneOnyx.BehaviorTree;
using ArcaneOnyx.GraphCore;
using Unity.VisualScripting;
using UnityEngine;
using ValueInput = ArcaneOnyx.BehaviorTree.ValueInput;   // both namespaces declare one; see Custom nodes

[GraphCreateMenu("Condition/Target In Range")]
public class TargetInRangeGuard : ReactiveGuard
{
    [DoNotSerialize] public ValueInput Range { get; private set; }

    public override string NodeName => "Target In Range";

    protected override void Definition()
    {
        base.Definition();
        Range = ValueInput<float>(nameof(Range), 2.0f);
    }

    public override bool Evaluate()
    {
        if (!VariableScope.TryGet("target", out var value) || value is not GameObject target) return false;

        float range = Range.GetValue<float>();
        return (target.transform.position - gameObject.transform.position).sqrMagnitude <= range * range;
    }
}
```

Everything else arrives for free: both switches with their inspector fields, the trigger list,
entry-always-fresh, the take-over poll, and the recorder events. `Range` stays a port so a designer can
tune it; the distance maths is compiled.

Two things the tools cannot check for you:

- **`bt_verify` cannot see a write in C#.** The purity rule is enforced by review for a C# guard.
- **Nothing can derive a C# guard's watched keys.** A guard that reads `hasTarget` inside `Evaluate` must
  list that key on its own **On Key Changed** trigger, or it never wakes. Most C# guards read continuous
  quantities, so **Every Interval** is usually the right trigger.

A C# **value node** feeding a guard can opt in to inheritance by implementing `IDeclaresWatchedKeys`:

```csharp
using System.Collections.Generic;
using ArcaneOnyx.BehaviorTree;

public class TargetDistance : GameplayNode, IDeclaresWatchedKeys
{
    public IReadOnlyList<string> DeclaredWatchedKeys => new[] { "targetPosition" };
    public IReadOnlyList<string> UndeclaredReadKeys => System.Array.Empty<string>();
    public UnityEngine.Object DeclarationOwner => null;
}
```

`Ask(bool fresh)` is virtual, so a guard can replace the whole dirty-flag path when its own change detection
beats the generic one. If you override it, you own honouring `fresh`: entry must recompute.

---

## Authoring from code

`BehaviorTreeAuthoring.GuardOnVariable(...)` builds a reactive guard **by default** and seeds an On Key
Changed trigger from the variable name. Pass `GuardKind.Conditional` for an entry-only doorman:

```csharp
// watchman: aborts, takes over, wakes when hasTarget changes
BehaviorTreeAuthoring.GuardOnVariable(asset, attack, "hasTarget", true, false, 0f, 340f);

// doorman: decides entry once and stops caring
BehaviorTreeAuthoring.GuardOnVariable(asset, taunt, "isElite", true, false, 0f, 340f,
    BehaviorTreeAuthoring.GuardKind.Conditional);

// a named, shared predicate: wakes on every fact IsHurt declares
BehaviorTreeAuthoring.GuardOnFunction(asset, retreat, isHurt, true, 0f, 340f);
```

Prefer `GuardOnFunction` as soon as the condition is more than one variable read. It refuses a Function
whose `Result` is not `bool`, by name, rather than letting the cast fail on the first tick.

---

## Try it

The reference project's [ReactiveGuards demo](https://github.com/platinio/bh3-development/tree/main/Assets/ArcaneOnyx/BH3Demos/ReactiveGuards)
runs three agents on the same facts: one with reactive guards, one with **Stops Its Own Branch** off, and
one with plain Conditional Executions that never leaves Idle. The
[WatchedKeyInheritance demo](https://github.com/platinio/bh3-development/tree/main/Assets/ArcaneOnyx/BH3Demos/WatchedKeyInheritance)
shows a guard waking on keys it inherited from a Function.

## Next

- [Sub-trees](07-sub-trees.md) — where guards usually go
- [Functions](08-functions.md) — the natural home of a guard's condition
