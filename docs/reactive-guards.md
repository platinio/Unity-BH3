# Reactive Guards

A guard is a precondition attached to a node. BH3 has two kinds, and the difference is which question they
answer.

| | Conditional Execution | Reactive Guard |
|---|---|---|
| Question | *May I start?* | *Is this still true?* |
| Evaluated | once, at entry | at entry, and again on a schedule while its owner runs |
| Gates entry | yes | yes |
| Aborts its own branch mid-run | no | `Stops Its Own Branch`, default on |
| Takes over from a lower-priority branch | no | `Takes Over Lower Priority`, default on |

The doorman checks once and stops caring. The watchman keeps looking, and can throw you out — or let someone
else in ahead of you.

## Why the split exists

Before this, every guard re-ran every tick, and the only way a branch could end early was its own guard
turning false. That forced each branch to carry the negated preconditions of everything above it:

```
Selector
├── Attack   hasTarget AND targetInRange
├── Chase    hasTarget AND NOT targetInRange
└── Idle     NOT hasTarget AND NOT lowHP AND NOT ...
```

Every branch had to know about every branch above it, and adding one meant editing all the others. The
responsibility was also backwards — the branch that wants to take over should carry the condition, not the
branch that has to yield.

With reactive guards each branch states only its own precondition, and the fallback carries none at all:

```
Selector
├── Attack   Reactive Guard: targetInRange   (Takes Over Lower Priority on, Stops Its Own Branch OFF)
├── Chase    Reactive Guard: hasTarget       (both on)
└── Idle     no guard
```

When `targetInRange` turns true, Attack takes the slot from Chase **on the same frame**. Idle does not know
Attack or Chase exist.

## Stops Its Own Branch and Takes Over Lower Priority are independent

Turning `Stops Its Own Branch` off is a real authoring move, not a way to disable the guard. It is the **committed
swing**: Attack bids for control the moment the target comes into range, but once the animation has started
it finishes even if the target steps back out. That combination is unreachable with a single switch, which
is why there are two.

## Mixing the two kinds on one node

This is legal and useful. Give Attack a Reactive Guard on `targetInRange` and a Conditional Execution on
`hasAttackToken`, and the reactive guard is the trigger while the conditional is a veto: it votes on whether
the takeover happens, but it can never cause one.

A takeover only fires when **every** guard on the candidate passes. Anything less would evict the running
branch and then fail the candidate's real entry check, restarting the victim from scratch — potentially
every tick.

## Triggers: when a guard may recompute

A reactive guard holds a list of triggers, OR'd together. **A trigger does not evaluate the guard — it marks
it dirty, and the tick decides.** A guard nothing marked dirty costs one bool check instead of a graph run.

| Trigger | Use it for |
|---|---|
| **On Key Changed** | the default — an agent fact like `hasTarget`. Free when nothing changed. |
| **Every Interval** (± deviation) | continuous quantities with no "changed" event: a distance, an angle, a resource level. |
| **Every Frame** | the honest escape hatch. Say it out loud rather than leaving the list empty. |

**An empty trigger list means every tick.** That is the most expensive thing a guard can do, so `bt_verify`
reports it — not because it is wrong, but so the cost is chosen rather than inherited.

### Entry always recomputes

Entry ignores the dirty flag. A stale *false* only costs latency, but a stale *true* enters a branch whose
precondition no longer holds — the animation starts, the token is claimed, and the abort has to unwind it.
Entries are rare next to ticks, so the extra evaluation is a rounding error against a whole class of glitch.

### What On Key Changed can see

Agent-scope variables only — the facts branches react to. A branch's own graph variables are per-call-site
scratch, and a guard watching those would be watching its own noise. Use an interval for anything else.

A key only counts as changed when its **value** actually moved — a fact republished every frame to the same
value costs its watchers nothing.

Which write paths a guard can see:

| Path | Wakes a guard? |
|---|---|
| `AgentVariableWriter.Write` — how a sensor should publish | yes |
| The **Set Variable** *tree node*, Object kind | yes |
| BH3's **Set Behavior Tree Variable** unit, Object kind, targeting an agent | yes |
| The same unit, Object kind, targeting something that is not an agent | no — see below |
| Unity's stock **Set Variable** unit | **no** |
| `Variables.Object(go).Set(...)` from C# | **no** |
| Any kind other than Object, on any of the above | no — nothing can watch those scopes |

The stock unit is the one the fuzzy finder offers first and it cannot be hooked, so a fact written with it
leaves the guard asleep with no error anywhere. `bt_verify` reports it when the write is inside the tree and
the key is spelled out; a computed name, or a write from C# or another asset, is beyond its reach.

Note that BH3's Set Behavior Tree Variable writes the **script graph's** own variables under its `Graph`
kind, not the behavior tree branch's scope, despite the name.

### Writing to something that isn't an agent

Set Behavior Tree Variable has an `@object` port, and an unconnected one means *self* — the agent running
the tree, which BH3 arranges by handing the graph its agent GameObject before the flow starts. That is the
normal case and it is versioned.

Point that port somewhere else and the write still happens, but **it is not versioned**. A version is only
ever *read* on a GameObject that runs a behavior tree, because a guard is a node inside one — so recording
one on a door or a manager is bookkeeping nobody will ever query, and paying for it would attach BH3
components to whatever you targeted.

Worth knowing this is rarely what you want anyway. A branch is conditional by construction, so anything only
a branch produces is unreliable by definition; facts should come from something that runs unconditionally,
which is what a sensor is. A tree reaching over to write another agent's facts is that rule broken with an
extra step.

### Keys a Function supplies for you

A guard reads nothing. It pulls a boolean off a port, so every key in its trigger is really a *claim* about
what the thing feeding that port reads — and a wrong claim is silent, because the guard just never wakes.

When the condition reaches a **Function**, that claim has a source. A Function declares the agent facts it
reads, and a guard whose condition reaches it picks them up automatically:

| | |
|---|---|
| **Where they come from** | The Function's declared watched keys, read live off the asset |
| **When** | Once per node instance, the first time the guard is asked |
| **What happens to your own keys** | Nothing — the effective set is the union. Hand-typed keys stay legal |
| **How far it looks** | Backwards through the whole condition, so a `Not` or any chain between the guard and the Function does not hide it |

Live, not copied. Edit the Function to read a new fact and every guard referencing it is correct on the next
run, with nothing to refresh and nothing to go stale. This is why there is no "refresh keys" command: there
is no copy to repair.

Hand-typed keys remain worth having for a dependency nothing can derive — a variable name computed at
runtime, or a C# node doing its own lookup. Those are invisible to any walk, and a declaration is the only
place they can be stated.

**Inheritance never creates a trigger.** A guard with no trigger recomputes every tick and keeps doing so,
even when its condition declares keys. That is deliberate: creating one would make an existing guard
evaluate *less* often than it does today, and a condition can also depend on things no key can express — a
raycast, a timer — which nothing at runtime can detect. So the schedule is seeded where an author can see
it, and reported where it is missing:

- `bt_guard_on_function` builds the guard and seeds the trigger from the Function's keys.
- `bt_verify` names any guard that has no trigger while its condition declares keys, listing them.

An **embedded** graph declares nothing and so supplies nothing. Only a Function has asset-level metadata to
declare with; walking an embedded graph's units instead would quietly turn "declares" and "happens to read"
into the same word.

## Guards must be pure

**A guard may only read.** This was easy to ignore when a guard only ran while its owner ran. It is not now:
a reactive guard evaluates on its own schedule, including while completely unrelated branches execute.

The tempting mistake is a graph that finds the nearest enemy, **caches it into `currentTarget`**, and returns
whether it is non-null — two operations that feel like one. Under a reactive guard that overwrites
`currentTarget` several times a second while another branch is mid-swing using it.

*Find and cache the target* computes and writes. *Is there a target* reads and answers. `bt_verify` warns
when a guard's graph contains a write, naming the guard and the unit.

## Where preemption works

Preemption is a contract on the composite, not a Selector feature, because each composite answers
differently:

- **Selector** — a higher-priority child becoming eligible takes over. Built.
- **Sequence** — the inverse: its earlier children already succeeded, so a guard there is a sustained
  requirement whose failure should fail the sequence. Specified, not yet built; `bt_verify` reports such a
  guard as defined but not active.
- **Parallel Selector** — no reaction. Children already run concurrently, so there is no resume point to move.

A guard set to `Takes Over Lower Priority` whose owner is not a direct child of a Selector still gates entry and still aborts
— it just has nobody to bid against. `bt_verify` says so.

### Sub-trees are guarded like anything else

A **Run Behavior Tree Graph** node takes guards exactly the way any other node does — only `Entry` refuses
them. In fact it is the *usual* place to put one: branch in its own asset, guard at the call site, which is
what keeps the branch reusable. Every guard in the demo and in the authoring skill's Zombie example is on a
sub-tree call node.

A reactive guard on one does all three things:

- **gates entry** — the branch never starts
- **`Stops Its Own Branch`** — aborts the sub-tree mid-run, and the abort reaches every node *inside* the
  instance, not just the call node
- **`Takes Over Lower Priority`** — makes the call node pollable, so its parent Selector can hand it the slot

What a run node does *not* do is take part in choosing among children — that is a composite's job, and a run
node has no children in its own graph. So there are two independent layers:

```
outer Selector      can take the slot away from the whole sub-tree call
   └── Run Sub-Tree
         └── inner Selector   preempts among its own children, knowing nothing about the outside
```

They only meet when the outer one wins, and then the teardown has to cross the boundary — which is why
aborting a sub-tree exits every node inside it rather than orphaning them mid-run.

## Reading what happened

A takeover is recorded as its own event, `NodeTakenOver`, naming the branch that lost the slot, the guard
that bid, and the branch that took over. It is deliberately distinct from an abort: *"your precondition
stopped holding"* and *"something more important wanted the slot"* are different answers, and only the second
names a cause outside the branch itself.

## Authoring from code

`BehaviorTreeAuthoring.GuardOnVariable(...)` builds a reactive guard **by default**, and seeds an
On Key Changed trigger from the variable name. Pass `GuardKind.Conditional` for an entry-only doorman:

```csharp
// watchman — aborts, preempts, wakes when hasTarget changes
BehaviorTreeAuthoring.GuardOnVariable(asset, attack, "hasTarget", true, false, 0f, 340f);

// doorman — decides entry once and stops caring
BehaviorTreeAuthoring.GuardOnVariable(asset, taunt, "isElite", true, false, 0f, 340f,
    BehaviorTreeAuthoring.GuardKind.Conditional);
```

`GuardOnFunction(...)` is the same thing for a named, shared predicate — and it seeds the trigger from what
the Function declares rather than from a name you pass in, because the Function already knows:

```csharp
// wakes on every fact IsHurt declares, without naming any of them here
BehaviorTreeAuthoring.GuardOnFunction(asset, retreat, isHurt, true, 0f, 340f);
```

Prefer it over `GuardOnVariable` as soon as the condition is more than a single variable read: one asset
fixed once lands in every tree that references it, and the schedule follows the fix. It refuses a Function
whose `Result` is not `bool`, by name, rather than letting the cast fail on the first tick.

## Writing a guard in C#

A Visual Scripting condition costs a graph run every time it is evaluated. For something checked per agent
per frame — a distance, an angle, a cone of vision — you can write the guard in C# instead and pay a method
call.

`ReactiveGuard` is abstract for exactly this reason: it owns the capabilities, the trigger list, the
dirty-flag caching and the preemption behaviour, and leaves only *how do I get my boolean* to the subclass.
`BooleanReactiveGuard` is just the version that reads a port.

```csharp
[GraphCreateMenu("Add Conditional Execution/Target In Range")]
public class TargetInRangeGuard : ReactiveGuard
{
    [DoNotSerialize]
    public ValueInput Range { get; private set; }

    public override string NodeName => "Target In Range";

    protected override void Definition()
    {
        base.Definition();

        Range = ValueInput<float>(nameof(Range), 2.0f);
    }

    public override bool Evaluate()
    {
        if (!VariableScope.TryGet("target", out var value) || value is not GameObject target) return false;

        float range = (float)Range.GetValue();

        return (target.transform.position - gameObject.transform.position).sqrMagnitude <= range * range;
    }
}
```

Everything else arrives for free: `Aborts Owner` and `TakesOverLowerPriority` with their inspector fields, the trigger
list, entry-always-fresh, the preemption poll, `NodeTakenOver` in recordings, and the dump.

Note the example does both things at once — `Range` is a port so a designer can tune it, while the distance
maths is compiled. Mix as suits: declare `ValueInput`s for what should stay wireable, and read
`gameObject`, `VariableScope` or `GetComponent<T>` directly for what should stay fast.

### If you need your own scheduling

`Ask(bool fresh)` is virtual, so a guard can replace the whole dirty-flag path when its own change detection
beats the generic one. If you override it, you own honouring `fresh` — entry must recompute, or the guard
can admit a branch on a precondition that no longer holds.

### Two things the tools cannot check for you

**`bt_verify` cannot see a write in C#.** The impure-guard lint inspects a guard's script graphs for write
units; compiled code is opaque to it. The rule is unchanged and matters more here, because a reactive guard
evaluates while completely unrelated branches are running — but for a C# guard it is enforced by review
rather than by the tool.

**Nothing can derive a C# guard's watched keys.** A guard that reads `hasTarget` directly inside `Evaluate`
has to declare that key on its own On Key Changed trigger, or a missing one means the guard simply never
wakes, silently. Inheritance cannot help here: it walks the nodes *feeding* a guard's port, and a C# guard
reads its value itself rather than being fed. In practice most C# guards read continuous quantities that
have no change event at all, so **Every Interval** is usually the right trigger for them.

A C# **value node** feeding a guard is the different case, and it can opt in. Implement
`IDeclaresWatchedKeys` and return the facts your node reads; a guard fed by it inherits them exactly as it
would from a Function:

```csharp
public class TargetDistance : BaseVisualScriptingNode, IDeclaresWatchedKeys
{
    public IReadOnlyList<string> DeclaredWatchedKeys => new[] { "targetPosition" };
}
```

Declared rather than derived, for the same reason a Function declares: a key your node computes at runtime
is invisible to any walk, so a declaration is the only place it can be stated at all.

## When a plain Conditional Execution is the right answer

Entry-only gating is not a downgrade. It is the correct tool whenever re-evaluation would be wrong or
wasteful:

- **Random or one-shot gates.** `RandomChance(0.25)` on a Taunt branch. A re-rolling guard kills its branch
  within one tick, which made this node class unusable before.
- **Expensive one-shot validation.** "Is there a valid cover point from here?" — a tactical position query is
  unthinkable per frame and correct at the door.
- **Config gates.** `difficulty >= Hard`, `isEliteVariant`. They never change; subscribing to them is waste.
