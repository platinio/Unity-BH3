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
