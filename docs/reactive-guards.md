# Reactive Guards

A guard is a precondition attached to a node. BH3 has two kinds, and the difference is which question they
answer.

| | Conditional Execution | Reactive Guard |
|---|---|---|
| Question | *May I start?* | *Is this still true?* |
| Evaluated | once, at entry | at entry, and again on a schedule while its owner runs |
| Gates entry | yes | yes |
| Aborts its own branch mid-run | no | `Aborts Owner`, default on |
| Takes over from a lower-priority branch | no | `Preempts`, default on |

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
├── Attack   Reactive Guard: targetInRange   (Preempts on, Aborts Owner OFF)
├── Chase    Reactive Guard: hasTarget       (both on)
└── Idle     no guard
```

When `targetInRange` turns true, Attack takes the slot from Chase **on the same frame**. Idle does not know
Attack or Chase exist.

## Aborts Owner and Preempts are independent

Turning `Aborts Owner` off is a real authoring move, not a way to disable the guard. It is the **committed
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
| **On Signal** | world events that are not agent state — an alarm, a door, a wave starting. |
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

A key only counts as changed when its **value** actually moved. Publish facts through `AgentVariableWriter`,
or from a tree through Set Variable / Set Behavior Tree Variable. Unity's stock **Set Variable** unit cannot
be observed, so a fact written that way will never wake a guard — `bt_verify` flags it.

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

A guard set to `Preempts` whose owner is not a direct child of a Selector still gates entry and still aborts
— it just has nobody to bid against. `bt_verify` says so.

## Reading what happened

A takeover is recorded as its own event, `NodePreempted`, naming the branch that lost the slot, the guard
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

## When a plain Conditional Execution is the right answer

Entry-only gating is not a downgrade. It is the correct tool whenever re-evaluation would be wrong or
wasteful:

- **Random or one-shot gates.** `RandomChance(0.25)` on a Taunt branch. A re-rolling guard kills its branch
  within one tick, which made this node class unusable before.
- **Expensive one-shot validation.** "Is there a valid cover point from here?" — a tactical position query is
  unthinkable per frame and correct at the door.
- **Config gates.** `difficulty >= Hard`, `isEliteVariant`. They never change; subscribing to them is waste.
