# One graph, two runtimes: the shared-runtime coexistence demo

**Status:** working demo + design, 2026-08-24. Branch `feature/shared-runtime-demo` in Unity-BH3 and the
superproject. **Nothing here ships in v2.** The demo exists to answer one question the team was split on:
can a shared-structure runtime live beside the classic clone-per-agent runtime *in the same system* — same
asset, same authoring stack — without making the old code noisier or the new code compromised? The
alternative on the table was a fully separate graph tool with its own node types.

The demo's answer, and the evidence, are below. The decision itself is the owner's.

## What the demo is

- `Runtime/Instancing/` — a second runtime, ~500 lines, seven files: `TreePlan` (an asset baked once:
  dense node indices, children in priority order, guards attached per asset), `TreeInstance` (one agent:
  three arrays — memory, status, running), `BTick` (one node's view of one tick: `Config<T>` reads shared
  structure, `Memory<T>` reads this agent), `NodeExecutor` + registry, executors for
  Entry/Selector/Sequence/WaitTime/DebugLog/Rotate, and `SharedBehaviorTreeMachine`.
- `BH3Demos/SharedInstancing/` — a scene where the **same `SI_Parity.asset`** runs on both machines side
  by side, plus spawn benchmarks, plus the spec-07 step-0 measurement on the Sample-FPS Zombie.
- **Zero edits to existing runtime files.** The old runtime on this branch is byte-identical to `main` —
  the coexistence claim is checkable with `git diff`, not taken on faith.

## What it demonstrated

**Parity.** Both machines run the identical asset. After ~14 seconds both agents read the *same rotation
angle to the decimal* (`yRot=310.0`); the guarded branch never entered on either (its `DebugLog` never
fired); the run sequence traced identically: `Wait → Success`, `Log → Success`, `Rotate → RUNNING`,
root `Running` forever.

**The debugger story, in miniature.** The parity check was performed by reading
`instance.LastStatus[i]` / `instance.Running[i]` — one array lookup per node. That is what spec 07 step 5
predicts inspection looks like on the shared path: the recorder taps one block, not fields scattered over a
clone.

**The numbers** (Editor, 200 agents per row, managed memory = `GC.GetTotalMemory(true)` delta — see
caveats):

| Spawn 200 agents | time | per agent | managed | graph-asset clones |
|---|---|---|---|---|
| Parity tree (12 nodes), OLD | 2744 ms | 13.72 ms | +114.2 MB | +200 |
| Parity tree (12 nodes), NEW | **0.7 ms** | **0.003 ms** | **+0.04 MB** | **0** |
| Zombie (38 nodes + Patrol sub-tree), OLD machine only | 5307 ms | 26.5 ms | +72–133 MB (run-to-run GC noise) | +400 |
| Zombie full prefab (meshes, sensors, nav), OLD | 5796 ms | 29.0 ms | +75.5 MB | +400 |

Step-0 readings of those step-0 rows:

- **The tree is ~91% of the whole prefab spawn.** 26.5 of 29.0 ms/agent is the behavior-tree clone, not
  the meshes, sensors or agents. The spec's guess that clone cost dominates realistic spawns is confirmed.
- **Nesting doubles the clones exactly as predicted** — +400 assets for 200 agents is root + Patrol per
  agent, spec 07's "cost compounds with nesting depth" made visible.
- **200 zombies is a 5–6 second main-thread stall** on this machine, in the Editor. Pooling can amortize
  it but cannot make it small; the per-agent retained clone stays regardless.
- On the shared path the same spawn is three array allocations; the plan is baked once per asset, ever.

Caveats, honestly: Editor-run numbers (player builds clone faster, but not orders-of-magnitude faster);
managed-only memory (native side of the clones is *not counted*, so the OLD rows understate); GC deltas are
noisy across runs (the Zombie row swung 72–133 MB); first-row JIT warmup favors no one in particular.

## What the demo deliberately does not cover

Stated so nobody mistakes a demo for the product:

- **Reactive guards and stateful guards.** The runner asks guards at the door only (`Evaluate()`), which
  is honest for the demo's stateless `BooleanConditionalExecution`. `ReactiveGuard`'s cache and
  `GuardTrigger`'s `seenVersions`/interval state are per-agent and must move into instance memory —
  spec 13 (parked seam branch) already maps every field into "moves / stays shared / thrashes" categories,
  and that table transfers unchanged.
- **Connected value ports and Functions.** `BTick.Config<T>` reads serialized defaults; a port fed by a
  value-node chain or a Function needs the agent threaded through evaluation (`ScriptGraphVariable`'s
  per-agent binding is the worked case — also mapped in spec 13).
- **Sub-trees, Parallel, preemption, variable scopes, the recorder, teardown hooks.** All absent. The
  composites implemented mirror `Selector`/`Sequence` for entry-only-guard trees; the preemption walk is
  production work.
- **Editor-time safety.** Nothing yet asserts "no structural mutation while any instance runs the asset" —
  spec 07's dev-build assertion is still owed.

## Design notes for the production version

- **External executors vs. methods on the node class.** The demo externalizes logic
  (`NodeExecutors.Register<WaitTime>(...)`) to prove zero-touch coexistence and because it supports node
  types whose source you do not own. The production shape is probably the same bodies as `virtual`
  methods on the node classes — one file per node, better discoverability — which is exactly the parked
  seam branch's `OnEnter(BTContext)` surface. **The two converge:** an executor body and a ctx-taking
  method body are the same code with a different home. Migrating a node is writing that body once; which
  door it hangs on is a style decision, not a second migration. If the coexistence direction is chosen,
  the parked seam PRs become the first production step rather than competing work.
- **The plan is the arming fix.** Guards attach per asset in `TreePlan.Bake` — spec 06's double-arming
  cannot exist on this path, as spec 07 predicted.
- **`Config` vs `Memory` is the whole authoring rule.** The demo's `BTick` makes the split physical:
  an executor cannot write to shared structure because it was never handed a writable view of it.
- **Baking is read-only by construction** (`ChildrenByParentInPriorityOrder` called for its answer, no
  `OnAwake`, no child-list mutation) — which is what makes one plan safe under every agent.

## Traps hit while building it (so the next run doesn't)

- **`AssetDatabase.SaveAssets()` only writes dirty-flagged assets.** The authoring helpers do not call
  `EditorUtility.SetDirty`, so a tree authored via raw C# helpers + `SaveAssets` writes an *empty* asset
  file, while every in-memory check — including `bt_verify`, whose re-import no-ops on an unchanged file —
  sees the full tree. The first domain reload then reveals the loss. Either call `SetDirty(asset)` before
  saving, or author through the `bt_*` commands. Worth fixing in the helpers themselves.
- Deleting an asset *and its .meta* to re-author it gives the new file a new GUID and silently dangles
  every scene reference to it.

## Where things stand

Spec 07's step-0 numbers are recorded (in spec 07 itself, per its instruction). The parked seam branch
(`feature/lifecycle-context-seam`, three open PRs) is the in-place-migration path; this branch is the
evidence that the two runtimes coexist peacefully in one system. If the coexistence direction is chosen,
the sequencing that follows is: land the seam (the authoring API), then grow the shared runtime under it,
using old-vs-new trace comparison — which this demo just performed by hand — as the parity gate.
