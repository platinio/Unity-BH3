# Facts & Services — solving produced-state without "declared writes"

**Status:** design spec for implementation by an AI or engineer with access to the BH3 source.
Written from BH3's design documentation — verify exact type/member names against the code before building.

## BH3 context you need

BH3 is a Unity behavior tree system built on visual scripting. Key existing mechanisms this spec builds on:

- Branches are separate `BehaviorTreeGraphAsset`s run via `RunBehaviorTreeGraphNode`. A branch declares
  **Required/Optional variables** which become input ports on the calling node — parameters passed at the
  call site. Deliberately **no output ports**.
- Variable scopes: reads walk outward (branch → caller → root), writes with `VariableKind.Graph` stay
  local to the branch instance. Agent-wide state uses `VariableKind.Object` on the agent's Variables
  component.
- `BooleanConditionalExecution` / `ConditionalExecution`: guard nodes that name an owner via
  `UpdateOwner(node)`, armed during `BehaviorTreeGraph.OnAwake` by `AddConditionalExecutionNodes`,
  re-evaluated every tick while the owner runs; a false guard aborts the owner immediately.
- Verification: `bt_verify` / `BehaviorTreeVerification` reloads assets and reports unset ports, orphans,
  contract drift. `BehaviorTreeDump.ToJson` emits the full tree with guids.

## The problem

A tree has Chase and Attack branches. Chase writes `lastKnownPosition` (agent state,
`VariableKind.Object`). Attack reads it. But the selector can enter Attack directly — the agent spawned
next to an enemy — so Chase never ran and `lastKnownPosition` was never written. The designer assumed the
value would be there. It wasn't.

## Rejected solution: "declared writes" in the branch contract

A branch declaring "I write `lastKnownPosition : Vector3`" was considered and rejected. It is
unenforceable in both directions, and even perfect enforcement wouldn't fix the bug:

1. **Writes are not statically visible.** A write can happen inside a C# node's `OnUpdate` body (opaque to
   graph analysis), inside a visual-scripting graph with a dynamically computed key, or conditionally
   (only on some code paths). A checker can neither prove a declared write happens nor prove an undeclared
   write doesn't.
2. **Even a true declaration is temporal, not guaranteed.** "I write X *when I run*" says nothing about
   *whether I ran before you read*. Enforcing "X is written before any read of X" is model-checking over
   all tick interleavings and world states — not feasible in an editor tool.

So a declared write is documentation wearing a contract costume. Correct to reject it.

## The reframe: the bug is misassigned responsibility, not missing enforcement

`lastKnownPosition` is not a behavior's output. It is **knowledge about the world** — "where did I last
perceive the target." Behaviors are *conditional by construction*: whether a branch runs depends on
priorities and guards, so anything only-a-branch produces is unreliable by definition. Knowledge must be
produced by something that runs **unconditionally**: a sensor.

This is how shipped AAA systems work: Unreal's AIPerception + BT *services* write blackboard facts
regardless of which branch is active; GOAP working memory is populated by sensors, consumed by planners.
Behaviors mostly consume facts; they rarely produce them.

### The design law (adopt this as a BH3 rule)

> **A branch may never depend on a sibling branch having run.**
> Test: "Would this branch still work if it were the only branch in the tree?"
> If no — either the state it reads is knowledge (move production to a sensor/service), or the two
> branches are one behavior pretending to be two (merge them into one sub-tree that owns both halves).

Chase/Attack passes the test once perception owns `lastKnownPosition`: spawn next to an enemy, the sensor
sees it on the first perception update, the fact exists, Attack works — Chase was never needed.

## Taxonomy of agent state (each row has a different owner and a different enforcement)

| Kind | Examples | Produced by | Consumed by | Enforcement |
|---|---|---|---|---|
| **Facts** (knowledge) | lastKnownTargetPos, visibleEnemies, alertLevel | Sensors (MonoBehaviours) and Services (new node kind, below) — always-on relative to their scope | Root-tree wiring, which feeds branch **parameters** | Fact-provider registry: compose-time + spawn-time check (below) |
| **Resources** (claims) | attack token, reserved slot, cover point claim | Acquire/release nodes inside branches | The resource system | Scope-bound auto-release on branch exit/abort (below) |
| **Scratch** | timers, retry counters | The branch itself, `VariableKind.Graph` | Same branch | Already solved — scoped, cannot leak |
| **Parameters** | attackRange, moveSpeed | Caller, at the call site | The branch | Already solved — Required port unset = `bt_verify` error |

With this taxonomy, **no legal branch-to-branch state handoff remains**, so nothing needs "declared
writes." The two new mechanisms:

## Mechanism 1: Service nodes (the guard's missing sibling)

`ConditionalExecution` *gates* a node while attached to it. A **Service** *produces* while attached to it.
Same attachment pattern, opposite data direction.

- New node kind `Service` (and `ScriptGraphService` — visual-scripting-backed, mirroring how
  `BooleanConditionalExecution` is fed by a graph).
- Attached to an owner node with the existing `UpdateOwner(node)` pattern; armed in
  `BehaviorTreeGraph.OnAwake` alongside guards (see spec 06 — that registration must be idempotent).
- Lifecycle: `OnOwnerEnter`, `OnOwnerTick` (with an optional tick interval + random deviation, like
  Unreal services, so a 0.2s perception poll doesn't run at frame rate), `OnOwnerExit`.
- **Ordering rule (load-bearing):** within a machine tick, services tick **before** guards are evaluated,
  so guards always see fresh facts. Document and test this.
- A service attached to the **root** is effectively an always-on sensor owned by the tree. A service
  attached to the Combat sub-tree ("refresh best cover point") runs only while Combat is active — facts
  that are expensive to maintain get a natural scope.
- Agent-global, tree-independent sensing (vision cone, hearing) stays in plain MonoBehaviour sensors on
  the agent prefab. Services are for tree-scoped fact maintenance. Both are fact providers (next section).

## Mechanism 2: Fact providers + registry (the enforceable contract)

Facts keep living where agent state lives today — `VariableKind.Object` on the Variables component. The
registry is **metadata + validation**, not a new runtime store.

- `[ProvidesFact("lastKnownTargetPos", typeof(Vector3))]` — attribute on sensor MonoBehaviours and on
  Service node classes / service script-graph assets. One class can provide several facts.
- Branches **do not read facts by name**. A branch that needs a fact takes it as a Required/Optional
  parameter (the existing contract). The **root tree** wires a fact read into the port at the call site.
  This moves every fact read to the composition layer — the one place where reads are statically visible
  (the wiring is in the dump; keys there are literals).
- **Compose-time check** (runs in `bt_verify` and in the requirements panel, spec 04): collect every fact
  key read in the root tree's wiring; collect every fact provided by components on the target
  prefab + services in the tree; report the set difference. "Attack's `targetPos` is fed from fact
  `lastKnownTargetPos` — nothing on this prefab provides it. Fix: add a `TargetingSensor`."
- **Spawn-time assert** (dev builds): `BehaviorTreeMachine.Awake` validates the same set difference on the
  live GameObject and fails loudly with the fact name — replacing a mid-combat
  `MissingValuePortInputException` with a named error at spawn.
- **Honest absence:** some facts are legitimately empty ("last known position" when the target was never
  seen). Convention: perception facts publish value + validity together (a `Fact<T>` struct: `value`,
  `hasValue`, `lastUpdatedTick`), with `GetFact` / `HasFact` visual-scripting units. Branch entry is then
  gated by the existing guard pattern ("has target"), which is where BH3 already handles world-state
  absence. Absence stops being a bug and becomes a modeled state.

## Mechanism 3: Abort-safe resources

For state like attack tokens, the danger isn't reads-before-writes, it's **leaks on abort** (branch
acquired a token, a guard aborted the branch, the token is never released).

- Acquisitions made by a branch are registered with the branch's scope.
- Scope teardown (branch exit **and** abort) auto-releases anything still held — RAII semantics.
- Audit the existing `RequestAttackToken` node: verify what happens today when its branch is aborted by a
  `ConditionalExecution`; wire it into scope teardown if it isn't already.

## Acceptance criteria

1. Service node kind exists, attaches like a guard, ticks before guards, supports tick intervals, and is
   visible in `BehaviorTreeDump` output (owner guid + provided facts).
2. `[ProvidesFact]` scanning works for MonoBehaviours and services; `bt_verify --against <prefab>` reports
   missing providers by fact name and consuming branch.
3. `BehaviorTreeMachine` spawn-time assert names the missing fact and the node that needs it.
4. The Chase/Attack scenario from this spec, rebuilt with a TargetingSensor: spawning inside attack range
   enters Attack successfully with a valid target position, with Chase never having run.
5. A branch aborted mid-acquisition releases its resources (test with a guard flipping false between
   acquire and use).
6. Documentation: the design law ("a branch may never depend on a sibling having run") added to the
   authoring skill/manual, with the merge-or-promote remedy.

## Non-goals

- No output ports on branches (unchanged, correct).
- No new runtime variable store — facts are Variables-component entries plus metadata.
- No attempt to statically verify writes inside branch internals — the design makes that unnecessary
  instead of pretending it's possible.
