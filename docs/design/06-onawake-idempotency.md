# Make BehaviorTreeGraph.OnAwake idempotent

**Status:** small fix spec for implementation with access to the BH3 source.
Written from BH3's design documentation — verify exact type/member names against the code before building.

## Context

Guards (`ConditionalExecution` / `BooleanConditionalExecution`) are separate nodes that name an owner via
`UpdateOwner(node)`. The link is only armed when `BehaviorTreeGraph.OnAwake` runs
`AddConditionalExecutionNodes`, which walks the graph and **appends** each guard to its owner's guard
list. Multiple guards on one owner are stored as a list and ANDed.

## The bug

`OnAwake` is append-only, so calling it twice registers every guard twice:

- Semantically masked: duplicated guards in an AND list produce the same boolean — so nothing visibly
  breaks, which is exactly why it's a landmine.
- Real costs: every guard evaluates twice per tick (guards are re-evaluated every tick while the owner
  runs — this doubles that cost); any guard whose evaluation has side effects (a visual-scripting graph
  that writes a variable or increments something) runs its side effects twice; debugger/telemetry sees
  phantom duplicate guards.

## When it actually happens

- Tests: the authoring docs instruct test writers to call `graph.OnAwake()` manually after wiring guards
  (guards are inert until armed). A test operating on a graph the machine also awakens double-registers.
- Editor tooling that warms a graph for inspection, then the machine awakens it.
- Any future live-edit / re-initialize feature will call it again by design.

## Fix (preferred: idempotent by construction)

In `AddConditionalExecutionNodes`: **clear every owner's guard list first, then rebuild** from the graph's
guard nodes. Rebuild-from-source makes the call idempotent regardless of how many times it runs, and
survives future guard kinds without bookkeeping. Alternatives considered and rejected:
- Dedupe on insert (guid-keyed set): works, but leaves stale entries if a guard was removed between calls.
- "Already initialized" flag: blocks legitimate re-initialization (live edit) and hides the second call
  instead of making it safe.

While there: audit `OnAwake` for any other append-style registration (event subscriptions, service
attachment if spec 01 lands, child-list bookkeeping) and give them the same clear-then-rebuild treatment.
Note that `SortContainerNodesChildren` (if still present — see spec 05) is naturally idempotent and needs
nothing.

## Regression tests

1. Call `OnAwake` twice; assert each owner's guard count equals the authored guard count.
2. Guard backed by a counting evaluation; two `OnAwake` calls + one tick → evaluation count == 1 per tick.
3. Remove a guard node from the graph between two `OnAwake` calls; assert the stale guard is gone.

---

# Implemented — 2026-08-11

**Status: done.** Built together with spec 05 on branch `feature/explicit-priority`, as its own commit
(`425ea9e`) preceding the 05 work. The two specs rewrite the same method, so splitting them across branches
would have meant rebasing the second onto a changed `OnAwake` for no review benefit.

## What was built

- `Runtime/Graphs/BehaviorTreeGraph.cs` — `AddConditionalExecutionNodes` clears each node's guard list before
  rebuilding; `ConvertTransitionNodesIntoTaskNodeChild` clears each container's child list before rebuilding.
- `Runtime/Nodes/BehaviorTreeNode.cs` — new `ClearConditionalExecutions()`.
- `Runtime/Nodes/ContainerNodes/ContainerNode.cs` — new `ClearChildren()`.
- `Test/EditMode/BehaviorTreeAwakeIdempotencyTests.cs` — **new**, 6 tests.
- `Test/EditMode/BehaviorTreeTestDoubles.cs` — new `CountingGuard` double, which spec 09 will also want.

## The second bug this spec did not name

The spec's audit note mentions "child-list bookkeeping" in passing. That turned out to be a real and more
damaging instance of the same bug: `ConvertTransitionNodesIntoTaskNodeChild` appended to every container's
child list, so a second `OnAwake` gave each composite **a duplicate of every branch**. For a `Selector` that
is a duplicated *priority* list — each branch tried twice before the next is reached, and every side effect
on it performed twice. Fixed in the same commit and covered by two of the six tests.

## Decisions made

- **Clear-then-rebuild, as the spec preferred.** Confirmed against the alternative while writing
  `AGuardRemovedBetweenAwakesIsNoLongerArmed`: a guid-keyed dedupe passes the count assertions but keeps
  arming a guard the designer has deleted, which the rebuild drops for free.
- **Both bugs are invisible in behaviour, so the tests measure counts, not outcomes.** Duplicated guards AND
  to the same boolean and a duplicated child list still picks the right branch. Asserting on behaviour would
  have produced tests that passed with the bug present.
- **Clearing children is safe** because no test mixes direct `AddChild`/`WithChildren` construction with
  `graph.OnAwake()` — verified across the whole test suite before making the change. The two styles are
  fully separate: doubles build containers standalone, graph-level tests build them from transitions.

## Verification

Tests were confirmed to fail without the fix, not merely to pass with it. With the two clear calls removed:
guard count 2 instead of 1, guard evaluations 2 instead of 1, stale guard still armed, child list 4 instead
of 2, failing branch ticked twice. The sixth test (`AwakingOnceStillArmsGuardsAndChildren`) passes either
way by design — it is the control that pins single-awake behaviour as unchanged.

EditMode suite: 331 → 337 tests, all passing except one pre-existing unrelated failure
(`TpsArchitectureTests.All_concrete_PositionEvaluators_are_marked_Serializable`, in the
TacticalPositionSelection module).

## Known gaps

- **No PlayMode baseline.** The PlayMode runner wedged before this work started — `test_status` reported a
  run in flight for ~20 minutes while the Editor sat at 0% CPU — so PlayMode is *unknown*, not green, both
  before and after. EditMode covers everything in this spec.
- The spec's suggestion to audit `OnAwake` for other append-style registration found only the two above.
  `SortContainerNodesChildren` needed nothing, as predicted.
