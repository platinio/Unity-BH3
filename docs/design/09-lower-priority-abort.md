# Reactive guards — preemption, scheduling, and the doorman/watchman split

**Status:** design spec for an implementing agent with full access to the BH3 source
(`Assets/ArcaneOnyx/BH3` submodule). **Supersedes the earlier `AbortScope` enum design** — see
*What changed and why* at the end for the trail. Everything marked *verified* below was read from the
current source; everything else is an assumption the implementer must check. Needs its own branch and
tests — this is a real behavior change, including one deliberate breaking change to existing assets.

## Problem (short form)

`Selector.OnUpdate()` resumes at `currentExecutingChildIndex` and never re-checks higher-priority
siblings while a lower one runs (*verified*, `Selector.cs:34`). So the only interrupt is the running
branch's own guard, which forces every branch to carry the negated preconditions of everything above it —
Idle's guard is `Not(hasTarget) AND Not(lowHP) AND …`, edited every time a branch is added above it.
O(n²) coupling, responsibility inverted: the branch that wants to take over should carry the condition,
not the branch that must yield.

The shipped `zombie-example.json` demonstrates the failure directly: Chase is guarded by `hasTarget`, so
when `targetInRange` flips true the zombie **keeps chasing** — Attack is eligible and never gets a turn.

A second problem, discovered while designing the first: **guards already run every frame and nothing says
so.** `BehaviorTreeNode.OnUpdateInternal` walks every guard on the running node every tick (*verified*,
`BehaviorTreeNode.cs:447`), and for a `BooleanConditionalExecution` fed by a `VisualScriptGraphVariable`
that means executing a flow graph per guard per frame. The flight recorder — the one system that sees
every evaluation — **deliberately discards the repeats** (*verified*, `BehaviorTreeFlightRecorder.cs:220`:
guards evaluate every tick, the transitions are the information). Correct for debugging behavior; it
means nothing anywhere reports the cost.

## The design: two node types, no enum

**Decided by the tool owner.** Not an `AbortScope` field on one node. Two nodes that ask different
questions, because the cost model differs and the cost model is the whole point.

| | `ConditionalExecution` (**changed**) | `ReactiveGuard` (**new**) |
|---|---|---|
| Question | *May I start?* | *Is this still true?* |
| Evaluated | once, at entry | when a dependency changes or an interval elapses |
| Condition source | any port — any graph, any cost | any port, but its leaves are declared so it can be subscribed |
| Gates entry | yes | yes |
| Aborts its own branch mid-run | **no** (this is the breaking change) | `AbortsOwner`, default true |
| Preempts a lower-priority sibling | no | `Preempts`, default true |

**The doorman and the watchman.** The doorman checks once and stops caring. The watchman keeps looking,
and can throw you out — or let someone else in ahead of you.

The split falls on the axis that matters: **evaluated once may be arbitrary; evaluated repeatedly must be
declared.** The expensive unanalyzable thing runs at the door where its cost is irrelevant; the cheap
declared thing runs often, where it must be.

`ConditionalExecution` keeps real uses and gains one it never had:

- **Random / one-shot gates.** `RandomChance(0.25)` on a Taunt branch. Today this node class is
  *unusable* — a re-running guard re-rolls every frame and the branch dies instantly. Entry-only gating
  fixes it.
- **Expensive one-shot validation.** "Is there a valid cover point from here?" — a TPS query, unthinkable
  per frame, correct at the door.
- **Config gates.** `difficulty >= Hard`, `isEliteVariant`. Never change; subscribing would be waste.

### Class structure

`ReactiveGuard` **derives from** `ConditionalExecution`. Everything about attachment is already shared and
must not be duplicated: `owner`, `UpdateOwner`, arming in `AddConditionalExecutionNodes`, the
`conditionalExecutions` list, `BeforeRemove`. `BooleanConditionalExecution` is unchanged.

Two virtuals on the base drive every walk — **filter on capability, never on type**:

```csharp
public virtual bool AbortsOwner => false;   // ConditionalExecution: entry-only
public virtual bool Preempts    => false;
```

`ReactiveGuard` overrides both with serialized fields defaulting to true. The entry-only change is then
expressed as `=> false` on the base rather than as a behavioral edit — self-documenting, and impossible to
misconfigure.

The three walks:

| Walk | Evaluates |
|---|---|
| `OnNodeEnter` | **all** guards. Unchanged. |
| `OnUpdateInternal` | only guards where `AbortsOwner`. |
| Preemption poll (new) | children with any `Preempts` guard; then **all** guards on the candidate. |

*Verified:* both `OnNodeEnter` and `OnUpdateInternal` are `sealed override` on `BehaviorTreeNode`, so no
subclass can intercept them — the filter must live inside those methods. They are also currently
duplicated verbatim (same `foreach`, same `EvaluateInternal`, same `GuardEval` recording, differing only
in `NodeSkipped` vs `NodeAborted` and an `IsRunning` check). The shared "evaluate all guards, recording
each" helper is therefore not new abstraction — it collapses an existing copy-paste into the three call
sites above.

## Evaluation model: triggers invalidate, the tick evaluates

A `ReactiveGuard` holds a **list of triggers, OR'd together** — composable, so new trigger kinds are
additive rather than a redesign:

- **`OnKeyChanged`** — the default. Keys are **auto-derived** by walking the guard's graph and collecting
  literal variable keys at the leaves, then **shown on the node and editable**: the designer can add or
  remove entries, and hand-edits are marked so a later reader knows the list was tuned. Auto-derivation is
  right most of the time and silently wrong on a dynamically computed key; showing it makes that visible
  instead of mysterious. The machinery mostly exists — `Runtime/Debugging/Traces/GuardTraceCapture`
  already walks a guard's graph capturing per-wire values for the why-panel; this is the same walk with a
  different collector.
- **`EveryInterval(t, ±deviation)`** — for continuous quantities. Random phase per agent at spawn;
  200 agents on a 0.2s timer all landing on one frame is a spike, which is why Unreal's services carry
  `RandomDeviation`.
- **`OnSignal("name")`** — a push with no value attached, for world events that aren't state.
- **`EveryFrame`** — the honest escape hatch, and just `EveryInterval(0)`.

**Triggers do not cause evaluation. They mark the guard dirty.**

```
key changed / interval elapsed  ->  guard.dirty = true
tree tick asks the guard        ->  if dirty: recompute, clear; else: return cached
```

Nothing outside the machine tick ever evaluates a guard. This is the load-bearing decision and it was
chosen over two alternatives:

- *Evaluate whenever the timer fires, on an independent loop* — nondeterministic. Whether a frame's
  decision sees a new value depends on Unity component execution order. Intermittent one-frame
  differences that reproduce on one machine and not another. **Rejected.**
- *Evaluate all due guards at the top of the tick* — deterministic, but pays for guards on branches the
  tree never considers this frame.

The dirty-flag model is deterministic *and* only evaluates what is asked for. A guard whose inputs have
not changed costs **one bool check** instead of a graph run.

Three consequences to implement deliberately:

1. **Entry always evaluates fresh**, ignoring dirty state. This matches Unreal: `CalculateRawConditionValue`
   reads the blackboard live, and the observer mechanism never supplies the answer — it calls
   `RequestExecution`, which only says *"something changed, go decide again."* The asymmetry justifies the
   cost: a stale *false* costs latency, a stale *true* **enters a branch whose precondition no longer
   holds** — the animation starts, the token is claimed, and the abort has to unwind it. Entries are rare
   compared to ticks, so the extra evaluation is a rounding error against an entire class of visible glitch.
2. **The preemption scan runs before the Selector ticks the running child.** Decide who should run, then
   run them.
3. **A node writing a watched key mid-tick** is seen this frame by guards asked after the write and next
   frame by those asked before. Order-dependent but *deterministically* so — identical every frame for a
   given tree shape. Document it; do not try to fix it.

### Guards must be pure

A guard evaluation may only **read**. No variable writes, no resource claims, no spawning, no counters.

This was tolerable before because a guard only ran while its owner ran. It is not now: a guard evaluates
on its own schedule whether or not its owner is running, including while unrelated branches execute. A
`SetVariable` buried in a guard graph would fire for the life of the agent.

The failure is one a designer would build naturally — a graph that finds the nearest enemy, **caches it
into `currentTarget`**, and returns whether it is non-null. That fuses two operations that feel like one.
Under this design the Idle branch's guard overwrites `currentTarget` five times a second while the Attack
branch is mid-swing using it.

This is the guard/service split from the other side: *"find and cache the target"* computes and writes;
*"is there a target"* reads and answers. **Enforce it in `bt_verify`** — the key-derivation walk already
visits every unit, so spotting write units (`SetVariable`, `SetBehaviorTreeVariable`, anything flagged
side-effecting) is free. Warn, naming the guard and the offending unit. Not a hard block — a designer may
have a reason — but never silent.

## The eligibility trap (do not skip this)

The naive poll — "evaluate the `Preempts` guards on higher-priority children; first one that passes wins"
— has a correctness hole when a child carries a *mix* of guards. Suppose Attack has `targetInRange`
(`ReactiveGuard`) and `hasAttackToken` (a `ConditionalExecution`). If only the reactive guard is polled:
`targetInRange` flips true while the token is unavailable → Idle is aborted → Attack's entry check
(`OnNodeEnter` evaluates **all** guards) fails → the selector falls through and restarts Idle. Idle was
killed for nothing, losing its state, potentially every tick.

**Rule:** a child is *pollable* iff it has at least one guard with `Preempts`; preemption fires only when
**every** guard on that child evaluates true — the poll is a full entry-feasibility check using the same
all-guards walk as `OnNodeEnter`. One poll, one answer: "would this child enter right now?" Abort the
victim only on yes.

Mixing guard kinds on one node is therefore legal and useful: the reactive guard is the takeover trigger,
the conditional is a veto that participates in the decision without being able to trigger it.

## Architecture: a Composite-level contract, not a Selector feature

**Decided by the tool owner.** Preemption is the general question "does a guard change on one child alter
this composite's resume decision?", and each composite answers differently.

- **Guard/node layer (composite-agnostic):** the capability virtuals, the filtered walks, the shared
  "evaluate all guards, recording each" helper, and a "has pollable guards" query. Nothing here knows what
  a Selector is.
- **`Composite` base:** a protected reactive hook — `TryReact(out int newChildIndex)` plus the shared scan
  utility ("walk these children in order, return the first whose full guard set passes") — default no-op,
  so non-reactive composites pay nothing. *Verified:* `Composite` is currently 11 lines
  (`currentExecutingChildIndex` and an `OnAwake`), so this is genuinely free for the others.
- **Each composite defines what a guard change means for it:**
  - `Selector` (v1, built now): a higher-priority pollable child becoming fully eligible **takes over**.
  - `Sequence` (**specified, not built in v1**): the inverse. Its earlier children already *succeeded*, so
    a guard there isn't a takeover bid — it is a sustained requirement. An earlier child's `Preempts`
    guard flipping **false** while a later child runs aborts the running child and fails the sequence.
    Reuses the same hook and scan. Until then `bt_verify` lints such a guard as "defined but not yet active".
  - `ParallelSelector`: no reaction — children already run concurrently, there is no resume point to move.
    Out permanently unless a concrete need appears.

## The Selector implementation (v1)

Current structure (*verified*): a `while` loop from `currentExecutingChildIndex`, a `callOnEnter` flag
distinguishing "resuming a running child" (false) from "entering the next child" (true),
`task.OnNodeExit()` called on Success and on Failure before advancing.

`Selector.OnUpdate()` invokes the reactive hook **before the loop**, and only in the resume case:

```
if (!callOnEnter && currentExecutingChildIndex > 0)        // a child is mid-run, others outrank it
    for i in 0 .. currentExecutingChildIndex - 1:           // priority order, first winner takes it
        if child[i] is pollable and all its guards pass:    // eligibility rule above
            children[currentExecutingChildIndex].OnNodeExit();  // same call the Failure path makes —
                                                                 // teardown parity is automatic
            record NodePreempted(victim, preemptor: child[i], guard)
            currentExecutingChildIndex = i;
            callOnEnter = true;
            break;                                          // fall into the existing loop
```

Falling into the existing loop means the preemptor is entered and ticked **on the same frame**. If it
immediately returns Failure, normal loop semantics take over (advance, possibly re-enter the victim
fresh); that restart is inherent to preemption, not a bug.

The scan must not run on the frame the selector itself is entered (`callOnEnter == true` — there is no
victim yet). Evaluating in index order guarantees the highest-priority eligible child wins.

## Does the guard travel with the branch?

**Decided by the tool owner: yes, as a default, never as a contract.**

A branch asset may declare a **suggested guard**. Dropping it from the library instantiates that guard at
the call site, where the designer can retune or delete it. If it were binding it would break the rule that
makes branches reusable — a branch must not know its caller.

Prior art: Unreal Behavior Trees do **not** do this (decorators live on the caller, same as BH3 today).
UE5 **State Trees** do — a state carries its Enter Conditions as part of its definition. This is the State
Tree idea imported into a behavior tree, which is what makes an interrupt library
(Flinch / Stagger / Dodge / Flee, stacked by priority) actually work as drag-and-drop.

## Cost, made visible

Opt-in keeps the default at zero: a selector with no pollable children adds nothing beyond one cheap check
(cache "which children are pollable" — guard attachment happens once at `OnAwake`, so compute lazily on
first tick; no LINQ in the tick path).

Beyond that, **the fix for cost is not to forbid it, it is to show it**:

- **Static, at author time, no play mode:** the guard node shows its graph's unit count and its evaluation
  frequency. With an explicit interval this is computable rather than estimated:
  `units × (1 / interval) × agents`.
- **Play mode:** evaluation counts and time per guard. The recorder is already the choke point — it needs
  a counter alongside the dedupe, not a new pathway.
- **`bt_verify`:** flag guards whose graphs are large *and* whose derived key list is incomplete (an
  opaque leaf — a raycast, `Time.time`, a random). Those are the ones that can never become event-driven,
  which makes them the actionable set.

This produces the intended workflow: prototype with a guard that does the math directly and polls, let the
cost display identify the few that matter, then promote those to a service publishing a fact. **Progressive
optimization instead of upfront discipline.** Forty-eight of fifty guards will never need it.

On oscillation: a flickering trigger produces preempt → finish → restart cycles. The interval already
provides some damping. Real hysteresis (enter at range X, hold until Y) belongs in whatever produces the
value, not in the guard. The flight recorder's oscillation detection
(`RepeatedAbortsAreCalledOutAsOscillation`) should count preemptions too.

## Recorder & tooling surface

- **New recorder event** `NodePreempted(victim, preemptor, guard)` — distinct from `NodeAborted` (own
  guard) and `NodeSkipped` (never started). Wire into the why-inspector: *"aborted at tick T: preempted by
  'Attack' (guard targetInRange flipped true because visibleEnemies changed)."*
- **Coordination with spec 02 (runtime debugger, in progress):** per-guard evaluation counters are wanted,
  and the event schema should make room before it is finalized — retrofitting a counter into a ring buffer
  that dedupes is annoying. Also tell that author the missing sub-tree exits were an upstream bug (fixed,
  below), not a recorder fault.
- **Editor:** the trigger list on the guard inspector, the derived key list with hand-edits marked, and a
  canvas affordance distinguishing preemption-capable guards — a `Preempts` guard acts at a distance and
  that must be visible at a glance.
- **Dump/CLI:** `BehaviorTreeDump` emits the guard kind, its triggers, and its derived keys;
  `bt_describe_tree` shows them; `bt_guard_on_variable` / `BehaviorTreeAuthoring.GuardOnVariable` gain an
  optional guard-kind parameter (default `ConditionalExecution`, preserving today's authoring).
- **`bt_verify` lints:** impure guard (writes) · `Preempts` guard whose owner is not the direct child of a
  reactive composite · Sequence-owned reactive guard ("defined but not yet active") · derived-key list
  stale against the graph.
- **Migration:** five assets in the repo use guards, all samples/demos (`FR_Demo_Sentry`, `Soldier`,
  `Zombie`, `TimelineDemo_Agent`, `WhyDemo_Agent`). Hand-convert them; `bt_verify` should report a
  `ConditionalExecution` that looks like it was relied on for interruption.
- **Docs debt (real):** the authoring skill's **rule 5** explicitly teaches *"prefer Conditional Executions
  over Condition nodes for interruptions"* — that inverts under this design. `zombie-example.json` ships
  the O(n²) pattern (Idle guarded by `Not(hasTarget)`). Both are the first thing any future agent reads
  before authoring a tree, so stale versions actively cause wrong output. Rewrite to: Attack carries
  `targetInRange` (`ReactiveGuard`, `AbortsOwner` **off** — the committed swing), Chase carries `hasTarget`
  (`ReactiveGuard`, both on), Idle carries **no guard at all**.

## Dependencies on other specs

- **Spec 06 (`onawake-idempotency`) — hard prerequisite.** Guards are armed in `BehaviorTreeGraph.OnAwake`
  via `AddConditionalExecutionNodes`, which **appends**: call `OnAwake` twice and every guard registers
  twice. Today that is duplicated evaluation. Once guards hold *subscriptions*, it is two live listeners
  on one key with one orphaned, for the life of the agent. A cosmetic bug becomes a leak. Fix first.
- **Spec 01 (facts & services) — demoted to an optimization, not a prerequisite.** An earlier draft made
  services load-bearing because continuous quantities needed discretizing at a controlled rate. The
  per-guard interval does that at the call site with no new node kind. Services remain the right answer for
  three cases: **fan-in** (five guards needing the same derived value compute it five times), **always-on**
  facts other systems read, and **stateful derivation** like hysteresis, which a guard cannot hold because
  a guard must stay a pure predicate.
- **Spec 03 (behavior library panel)** — the suggested-guard mechanism above is what makes library drops
  useful. Worth building the two together.

## Resolved during design (do not re-litigate)

**Sub-tree teardown was broken and is now fixed.** *Verified and repaired:* `RunBehaviorTreeGraphNode`
overrode seven propagations into its sub-graph (`OnAwake`, `OnEnter`, `OnUpdate`, `SetMachine`,
`SetVariableScope`, `SetFlightRecorder`, `CanvasUpdate`) but **not `OnExit`**, and `BehaviorTreeGraph` has
no `OnExit` at all. `ContainerNode.OnExit` cascades to children correctly *within* a graph, so the cascade
stopped dead at the sub-tree boundary.

Normal completion was fine — the inner containers exit their children on the way out. **Abort was not:** a
guard returns Failure from the `OnUpdateInternal` walk *before* `base.OnUpdateInternal()`, so
`RunBehaviorTreeGraphNode.OnUpdate()` never ran that frame, the inner graph was never ticked, and every
node inside was left with `IsRunning == true` and its `OnExit` never called. Anything acquired on enter and
released on exit leaked permanently, and the recorder showed branches that entered and never left.

This was **live before this feature** — any guard on a `RunBehaviorTreeGraphNode` flipping false hit it.
Preemption would have converted a rare occurrence into a constant one. Fixed by overriding `OnExit` to
loop the instance's nodes, guarded by `HasBehaviorTreeGraphInstance` so exiting a never-entered branch does
not clone the asset. Safe to call on every node because `BaseGraphNode.OnNodeExit` early-returns on
`!isRunning` — the same blanket call `ContainerNode.OnExit` makes.

**Still owed:** a test. Enter a sub-tree, abort it from the parent, assert every node inside reports
`IsRunning == false` and ran its `OnExit`. `Test/EditMode/SubTreeParameterTests.cs` is the natural
neighbour.

## Open design questions

Blocking — decide before serializing anything:

1. **Suggested-guard mechanics.** Where does a branch declare it (beside required/optional declarations?),
   what happens on re-drop, and does contract drift apply the way it does for parameters? The
   `RefreshParameters` / `DescribeContractDrift` pattern is the obvious model, but a guard is not a port
   and may not want the same "report, never auto-apply" rule.
2. **Derived-key staleness.** The key list is a cached copy of what the graph reads, so it can drift when
   the graph changes — the same failure `BehaviorTreeGraphParameter` already solves for sub-tree contracts.
   Re-derive on save, on verify, or report drift and require an explicit refresh?
3. **`EvaluateInternal` writes `LastExecutionStatus`** (*verified*, `ConditionalExecution.cs:18`). Polling
   a non-running node's guard therefore mutates state the editor visualization and why-panel read. Either
   the poll uses a non-recording variant, or a guard shows live status while its owner is idle — arguably
   better for debugging, but it is a visible change nobody asked for. Pick one deliberately.
4. **Separate triggers per role?** Watching your own running branch and bidding to take over someone
   else's need not share a rate — continuing a dead behavior for 0.1s is worse than starting a new one
   0.3s late. A cheaper `Preempts` rate would cut the cost that multiplies across every branch above the
   running one. One trigger list or two?
5. **Purity: warn or error?** Recommendation is warn. Confirm.
6. **Does `CanUseConditionalExecutions`** (*verified*, `BehaviorTreeNode.cs:61`) gate `ReactiveGuard` too,
   or does it need its own opt-out?

Revisit after playtesting, not before:

7. **Hysteresis.** Is the interval enough damping, or is a per-guard preemption cooldown needed? Do not add
   the field speculatively.
8. **Scheduler.** Per-agent or global evaluation budget, guards round-robining within it, so cost stops
   scaling with agent count and overload degrades as latency rather than framerate. **Design the interface
   now even if unimplemented** — "the guard asks a scheduler whether it may run" versus "the guard checks
   its own timer" is cheap on day one and painful to retrofit.
9. **LOD.** A distance/significance multiplier applied to every interval, so an enemy 80m away does not get
   0.2s reactions. Who owns the multiplier — the machine, an agent component, a global significance
   manager?
10. **Sequence reactivity.** Semantics are specified above; build on the same hook when there is a
    concrete need.

## Tests

1. **Basic preemption:** Idle running; Attack's `Preempts` guard flips true → same-frame takeover; victim
   got `OnNodeExit`; recorder shows `NodePreempted` with the right guard.
2. **Eligibility rule:** preemptor's reactive guard true but a `ConditionalExecution` on the same node
   false → no preemption; victim untouched.
3. **Committed branch:** a branch whose reactive guard has `AbortsOwner` off keeps running when the guard
   flips false mid-run; the same graph with `AbortsOwner` on aborts.
4. **Entry is always fresh:** a guard marked clean with a stale cached answer still evaluates on entry.
5. **Dirty-flag economy:** a guard whose keys did not change and whose interval did not elapse is not
   evaluated; its graph unit does not run.
6. **Determinism:** identical tick sequences produce identical decisions regardless of when triggers fired
   within the frame.
7. **Entry-frame exclusion:** no preemption scan on the selector's own entry frame.
8. **Priority among preemptors:** two eligible preemptors → lower index wins.
9. **Sub-tree victim:** preempting a running `RunBehaviorTreeGraphNode` leaves state identical to a
   self-abort of the same branch (now that the teardown cascade is fixed — see above).
10. **Purity lint:** a guard graph containing `SetVariable` is reported by `bt_verify`, naming guard and unit.
11. **Backward compatibility:** existing suite green; a `ConditionalExecution`'s *entry* behavior
    byte-identical. Its loss of self-abort is the intended breaking change and is covered by the migration
    of the five sample assets.
12. **Oscillation:** flickering trigger flagged by the recorder's oscillation detection.

## Files touched (expected)

- `Runtime/Nodes/Decorator/ConditionalExecution.cs` — capability virtuals, both `=> false`.
- `Runtime/Nodes/Decorator/ReactiveGuard.cs` — **new**; triggers, derived keys, `AbortsOwner`, `Preempts`.
- `Runtime/Nodes/BehaviorTreeNode.cs` — shared "evaluate all guards, recording each" helper (collapsing the
  existing duplicate); capability filter in the `OnUpdateInternal` walk; "has pollable guards" query.
  **Note both walks are `sealed override`** — the filter cannot be added by subclassing.
- `Runtime/Nodes/Composites/Composite.cs` — reactive hook + shared scan utility, default no-op.
- `Runtime/Nodes/Composites/Selector.cs` — implements the hook with the pre-loop scan.
- `Runtime/Graphs/BehaviorTreeGraph.cs` — idempotent guard arming (spec 06).
- Recorder — `NodePreempted` event + evaluation counters + oscillation counting.
- Editor inspector for guard nodes; `BehaviorTreeDump`; authoring helpers + CLI commands; verify lints.
- Skill doc + `zombie-example.json` (docs debt above).
- ~~`Runtime/Nodes/Gameplay/RunBehaviorTreeGraphNode.cs`~~ — **done**, teardown cascade fixed.

## What changed and why

The first draft of this spec put an `AbortScope { Self, LowerPriority, Both }` enum on
`ConditionalExecution` and polled arbitrary guards every tick. Three things moved it:

1. **A guard reading an arbitrary port is unobservable by construction.** You cannot subscribe to
   "whatever that graph computes." Freezing that into the asset format would have made every tree
   authored under it un-migratable when subscription arrived. Hence a separate node whose *inputs* are
   declared — while the computation stays an arbitrary visual graph, because `hasTarget` is a **decision**
   designers retune constantly, not a raw fact, and it must never require a recompile.
2. **The cost problem was visibility, not magnitude.** Guards already ran every frame; the recorder
   deliberately hid the repeats. Making cost legible turns per-frame evaluation into a designer's choice
   rather than a hidden tax — and once it is a choice, most of the enum's justification disappears.
3. **A separate node type collapses the enum.** `Self` is a reactive guard, `LowerPriority` is one with
   `AbortsOwner` off, `Both` is the default, and `None` — which BH3 cannot express today and Unreal
   defaults to — is what `ConditionalExecution` becomes. Every cell reachable, none of it a dropdown that
   can be set to a combination nobody tested.
