# Shared immutable trees + per-agent instance memory

**Status:** design spec for implementation by an AI or engineer with access to the BH3 source.
Written from BH3's design documentation — verify exact type/member names against the code before building.
**This is the largest refactor in the set — measure first (step 0), and only proceed if the numbers
justify it.**

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

### Step 0 numbers — measured 2026-08-24

Editor run, 200 agents per row, `BH3Demos/SharedInstancing` scene (branch `feature/shared-runtime-demo`).
The Zombie is the Sample-FPS Zombie: 38 nodes, Patrol sub-tree, 2 reactive guards with OnKeyChanged
triggers, 5 function-graph nodes. Managed memory is a `GC.GetTotalMemory(true)` delta — it under-counts
the clones' native side and is noisy run to run; times are Stopwatch over the activation loop.

| Spawn 200 agents | total | per agent | managed | graph-asset clones |
|---|---|---|---|---|
| Zombie, machine only (the tree's own cost) | 5307 ms | **26.5 ms** | +72–133 MB | **+400** (root + Patrol per agent) |
| Zombie, full prefab (meshes, sensors, nav) | 5796 ms | 29.0 ms | +75.5 MB | +400 |
| 12-node demo tree, old runtime | 2744 ms | 13.7 ms | +114 MB | +200 |
| 12-node demo tree, shared-plan demo runtime | 0.7 ms | 0.003 ms | +0.04 MB | 0 |

Readings: **the behavior tree is ~91% of the entire prefab spawn cost** (26.5 of 29.0 ms/agent); nesting
doubles the clones exactly as this spec predicted; 200 zombies is a 5–6 s main-thread stall that pooling
can amortize but not shrink. The verdict this section asks for: **the numbers justify proceeding** — the
question is no longer whether, but via which path (see
[14-shared-runtime-coexistence.md](14-shared-runtime-coexistence.md) for the measured shared-path demo).

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
