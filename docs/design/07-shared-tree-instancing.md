# Shared immutable trees + per-agent instance memory

**Status:** design spec for implementation by an AI or engineer with access to the BH3 source.
Written from BH3's design documentation — verify exact type/member names against the code before building.
**This is the largest refactor in the set — measure first (step 0), and only proceed if the numbers
justify it.**

> ## Implementation status — 2026-08-23
>
> **Migration steps 1 and 2 are DONE and shipping in v2. Steps 0 and 3–5 are NOT started.**
> The seam is in; the instancing flip is not. `BehaviorTreeMachine.Awake` still calls
> `Instantiate(macro)` and every node still runs on a per-agent clone — behaviour is unchanged by design.
>
> Branches: `feature/lifecycle-context-seam` in **Unity-BH3** and in **graph-core-library**.
> See "What step 1–2 actually shipped" at the bottom of this file for the decisions, the deviations from
> this spec, and what the next person needs to know.

## Current model (the cost)

- `BehaviorTreeMachine.Awake` calls `Object.Instantiate` on the tree macro — a full deep clone of the
  graph (nodes, ports, transitions) **per agent**. Unity's Instantiate on a ScriptableObject graph is a
  serialize/deserialize round trip — expensive in time and allocations.
- `RunBehaviorTreeGraphNode` instantiates its sub-tree asset **per call site**
  (`BehaviorTreeGraphAssetInstance` clones lazily on first access), so cost compounds with nesting depth.
- Why cloning is currently required: mutable runtime state (timers, current-child indices, statuses,
  cached components) lives **in fields on the node instances** — structure and state are one object.

Scaling shape: memory and Awake time ~ agents × total nodes across the nesting tree. Fine at 10 agents;
the question is 200.

## Step 0 — measure (do this before any refactor)

Profile a scene spawning 200 machines of a realistic nested tree (e.g. the Zombie with its three
sub-trees):
- Awake time per machine, total spawn hitch, GC allocations (Profiler / `ProfilerRecorder`).
- Retained memory per agent (Memory Profiler snapshot diff).
Record the numbers in this file. If spawn hitch and memory are acceptable at the game's real agent count
and spawn pattern (pooling may already hide it), **stop here** — this refactor is not free and the
migration cost is real.

## Target model (the Unreal shape)

One **shared immutable graph** per asset — nodes, ports, transitions, defaults, guard attachments — loaded
once, never mutated at runtime. Per agent: a compact **instance memory** block holding only mutable state,
indexed by node.

- At asset load: assign every node a stable dense index (topological order). Build guard-owner attachment
  lists once here (this also structurally fixes the double-registration issue in spec 06 — arming happens
  per asset, not per agent).
- Per agent at spawn: allocate `NodeMemory[] memory = new NodeMemory[nodeCount]` from a per-tree template
  (one array, no graph clone). C# version of Unreal's node-memory-offset scheme; per-node-type memory
  classes/structs (e.g. `WaitTimeMemory { float elapsed; }`, `CompositeMemory { int currentChild; }`).
- Node callbacks take a context instead of using instance fields:
  `OnUpdate(BTContext ctx)` where `ctx` carries the agent, the machine, and
  `ctx.Memory<WaitTimeMemory>(this)` (lookup = `memory[node.index]`, O(1), no boxing if memory entries
  are class instances pooled per type, or a byte-block with offsets if you want the full Unreal design —
  start with class-per-node instances in one array; optimize to a byte block only if profiling demands).
- Variable scopes: already per-instance semantically (per call site). They move into the context — a
  scope stack owned by instance memory, not by cloned graph objects.
- Sub-trees: `RunBehaviorTreeGraphNode` keeps only the asset reference in shared structure; its instance
  memory owns the child scope + a child memory block. Recursion checks unchanged.

## Migration path (do not rewrite every node at once)

1. **Introduce the seam:** add `BTContext` and context-taking virtual overloads
   (`OnEnter(ctx)/OnUpdate(ctx)/OnExit(ctx)/OnAwake(ctx)`) whose default implementations forward to the
   legacy parameterless methods. The machine calls the ctx versions everywhere. Behavior unchanged; both
   node styles coexist.
2. **Enforce the direction:** BH3 already has `BehaviorTreeArchitectureTests` (they enforce declaration
   rules like `MaxChildrenLimit`). Add a test: any node overriding the legacy methods, or declaring
   non-serialized mutable instance fields, is flagged as "not yet migrated" against an explicit shrinking
   allowlist. The allowlist going to zero is the progress bar.
3. **Migrate nodes by traffic:** composites and decorators first (they hold the child-index state that
   forces cloning), then the hot leaves (waits, movement, animation), then the long tail.
4. **Flip instancing:** when the allowlist is empty, replace `Instantiate(macro)` in
   `BehaviorTreeMachine.Awake` and the per-call-site sub-tree cloning with shared-graph + memory-block
   allocation. Keep the old path behind a project setting for one release as a fallback.
5. **Update the tooling contract:** the debugger (spec 02) and any live inspection must read state from
   instance memory, not node fields — cleaner for them anyway, since the recorder taps one memory block
   instead of scattered fields. Authoring/dump/verify are untouched (they operate on serialized assets).

## Traps

- **Node authors' habits:** instance fields are the natural C# thing to write; without the architecture
  test (step 2) the codebase regresses immediately. The test is not optional.
- **Closures and events:** any node caching delegates that capture `this` state, or subscribing to events
  in `OnAwake`, needs explicit per-agent handling in memory (and unsubscription on despawn).
- **Editor-time mutation:** authoring tools mutate node objects legitimately (they edit the asset).
  Immutability is a *runtime* contract: assert no structural mutation while any machine is running the
  asset (dev builds).
- **`BehaviorTreeGraphAssetInstance` / `BehaviorTreeGraphInstance`:** these clone-on-access properties
  become obsolete on the new path; keep them functional for the legacy fallback, delete after.

## Payoff (beyond spawn cost)

- Memory per agent: state array instead of full graph clone.
- Spawn: one array allocation instead of deep Instantiate — pooling-friendly.
- Live edit becomes reachable: structure is shared, so an asset edit can propagate to all running agents
  (their memory re-maps by node index).
- Ticking many agents over one immutable structure is a step toward job-friendly batch evaluation later.

## Acceptance criteria

1. Step 0 numbers recorded before and after; the after shows the win (target: >5× less retained memory
   per agent on the Zombie tree, spawn hitch amortized to near-flat with pooling).
2. All BH3 tests green on the new path, including guard behavior tests (which arm per-asset now).
3. A soak scene: 200 agents running the nested Zombie tree, spawning/despawning in waves — no growth in
   memory, identical behavior traces (spec 02 recordings) between old and new paths on a scripted
   scenario.
4. Architecture test allowlist at zero; writing a new node with mutable instance fields fails CI with a
   message pointing at the memory pattern.

---

# What step 1–2 actually shipped (2026-08-23)

Scope was deliberately limited to the **authoring seam**: freeze the API node authors write against, so the
instancing flip later is not a breaking change for them. Nothing about instancing changed.

## Files

**graph-core-library** (`Modules/GraphCore`), branch `feature/lifecycle-context-seam`
- `Runtime/Nodes/BaseGraphNode.cs` — four `protected virtual` dispatchers `InvokeAwake/InvokeEnter/
  InvokeUpdate/InvokeExit`, each defaulting to exactly the call it replaced; the four internal call sites
  now go through them.

**Unity-BH3** (`Assets/ArcaneOnyx/BH3`), branch `feature/lifecycle-context-seam`
- `Runtime/Nodes/BTContext.cs` — new. The context type.
- `Runtime/Nodes/BehaviorTreeNode.cs` — `Context` property, the four sealed dispatcher overrides, the four
  `OnX(BTContext)` virtuals forwarding to the legacy hooks, `AwakeNode()`, and the `nodeMemory` slot.
- `Runtime/Graphs/BehaviorTreeGraph.cs` — the awake fan-out calls `AwakeNode()` instead of `OnAwake()`.
- `Test/EditMode/NodeContextSeamTests.cs` — new, 13 tests.
- `Test/EditMode/NodeContextConventionTests.cs` — new, 5 tests, holds the two allowlists.
- `docs/custom-nodes.md`, `docs/api-reference.md`, `docs/best-practices.md` — teach only the ctx style.

## Decisions made, and why

1. **The dispatch indirection lives in GraphCore, not BH3.** `BaseGraphNode` owns what "entering a node"
   means (`isRunning`, colour state, the rule that a node whose `OnEnter` threw never entered). Routing the
   context from BH3 alone would have meant reimplementing that bookkeeping in `BehaviorTreeNode` — a second
   copy of a decision that would silently miss the next fix to it, exactly like graph-core-library PR #2.
   Cost: two submodule PRs instead of one.

2. **`BTContext` lives in BH3, and the GraphCore dispatchers take no parameter.** GraphCore cannot see BH3
   (asmdef direction), so a context declared on `BaseGraphNode`'s signature would have had to live in
   GraphCore, dragging BT vocabulary into a generic graph library for a second consumer that does not exist
   (`BehaviorTreeNode` is the only subclass of `BaseGraphNode` in the repo).

3. **Each node builds its own context; nothing is threaded through composites.** `Context` is one property
   on `BehaviorTreeNode`. No composite, container, decorator or the machine changed. When the flip lands,
   a context stops being derivable from a node and starts carrying the agent's memory block — that is a
   change to **one property**, not to every container that would otherwise be threading a parameter down.

4. **`ctx.Memory<T>()` SHIPPED, contradicting this spec's "start with class-per-node instances in one
   array".** It is backed by a single `[DoNotSerialize] object` on the node today. This was not in the
   original plan for step 1 and was added because **without it the step-2 architecture test is
   unsatisfiable**: the rule forbids per-agent instance fields, and with no memory accessor a node author
   who needs a timer has no legal way to write one. The semantics are identical before and after the flip
   — "storage private to this node on this agent" — so this is a seam, not a stub. `[DoNotSerialize]` is
   what makes each agent's clone start with its own null slot.
   Signature deviates from the spec's `ctx.Memory<T>(this)`: the context already knows its node, which
   removes the one way that call could be got wrong.

5. **One memory type per node, enforced by throwing.** A second type on the same node means two bodies
   disagree about what the node's state is, and the flip would have no way to size the slot.

## Deviations from this spec worth knowing

- Step 2 says to flag "any node overriding the legacy methods, **or** declaring non-serialized mutable
  instance fields". Both rules shipped, as two separate allowlists, because they shrink independently —
  a node can be migrated off the legacy hooks before its state moves, and usually will be.
- The allowlists have a second guard this spec does not mention: a test fails if a listed name **no longer
  offends**. Without it the list rots into a permanent opt-out and stops being a progress bar. This is the
  mechanism that makes "the allowlist going to zero" true rather than aspirational.

## Where the debt actually is (measured, not estimated)

78 node types scanned. **49 still override a legacy hook. 30 still hold per-agent instance state.**
Both numbers are in the allowlists in `NodeContextConventionTests.cs` and are the remaining step-3 work.
The heaviest are the composites/containers (`currentExecutingChildIndex`, `childrenTaskStatus`,
`callOnEnter`, `children`) and `RunBehaviorTreeGraphNode`.

## Known gaps — read before doing step 3

- **Guards and conditions are not seamed at all.** `Condition.Evaluate()`,
  `ConditionalExecution.Evaluate()` and `ReactiveGuard.Ask(bool)` take no context, and `ReactiveGuard` /
  `GuardTrigger` hold the densest per-agent state in the codebase (`hasCachedResult`, `cachedResult`,
  `lastEvaluatedAt`, `seenVersions`, `cachedAgent`, `cachedWriter`). This is the largest un-seamed public
  surface, and a third-party guard written against v2 has nothing to migrate to. Doing this is the natural
  step 2.5.
- **No test asserts on `ctx.Machine` / `ctx.gameObject` / `ctx.transform`.** The dispatch path itself *is*
  covered against a live machine — the existing PlayMode suite runs real trees through real machines, and
  those runs go `BehaviorTreeMachine.Update -> OnUpdateInternal -> InvokeUpdate -> OnUpdate(BTContext) ->
  TickChild -> InvokeEnter -> OnEnter(BTContext) -> <legacy node body>`, which is the whole seam plus the
  forwarding. What is not covered is the agent-facing *members* of the context: the new edit-mode tests
  tick nodes directly, so `Machine` is null throughout and those three accessors are never read. Closing
  that needs a machine-backed fixture, which the PlayMode suite deliberately avoids standing up today.
- **`ValueOutput` lambdas capture `this`** (`ValueOutput<T>(key, () => Compute())`). That is the
  "closures and events" trap named above, it is untouched, and it will need the same treatment.
- **`BaseGraphNode.OnNodeAwake()` is dead code** — nothing calls it; the graph calls the node's awake body
  directly. It was left alone rather than revived, because routing awake through it would have added
  `isRunning = false` and exception capture to a path that has neither today, which is a behaviour change.
  Worth deleting or adopting deliberately.
- **The instance-state rule can be evaded with `[Serialize]`.** It asks whether a field is serialized, not
  whether the node writes it while running, so `[Serialize] private float timer;` passes and is *worse*
  than the failure it silences — a serialized field is part of the shared tree, so after the flip it is one
  value every agent writes, and nothing reports it. Closing it means checking assignment sites, not
  declarations: an IL scan of the four lifecycle bodies for `stfld` against a serialized field, or a source
  scan in the style of `PortReadConventionTests`. Deliberately not built for v2 — a naive IL byte scan
  produces false positives from operand bytes, and a wrong rule here is worse than a documented one.
  `custom-nodes.md` warns authors off it explicitly.
- **`private static` mutable state is not checked at all** and would be a sharper version of the same bug.
  Same fix would cover it.
- **The canvas colour-lerp state** (`currentColor`, `startLerpColor`, `colorTimer`) lives on
  `BaseGraphNode` and is per-agent too. It is outside BH3's allowlists because the scan only covers the BH3
  runtime assembly. The flip has to deal with it.
