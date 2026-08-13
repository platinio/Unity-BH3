# Function graphs — named, contracted, reusable Visual Scripting logic

**Status:** design spec for implementation by an AI or engineer with access to the BH3,
VisualScriptingExtension and TacticalPositionSelection sources. Decided with the tool owner (2026-08-11).
Everything marked *verified* was read from the current source; everything else is an assumption the
implementer must check. Spans three submodules — see *Which repo owns what* before branching.

## Problem (short form)

Three systems are missing the same concept: a graph with a name, an interface, and an owner.

**1. Embedded graphs have no identity, so a garbage collector owns their lifetime.** A script graph
authored inline is an anonymous sub-asset welded to its tree (*verified*,
`BTScriptGraphVariablePropertyDrawer.cs:96`, `BehaviorTreeAuthoring.cs:289` — `AddObjectToAsset`).
Nothing deletes it when its node dies, so a **global singleton ledger** (`ScriptGraphAssetsRepository`,
a project asset mapping owner-GUID → graph) exists solely to let a mark-and-sweep run — and that sweep
runs **every canvas OnGUI frame** (*verified*, `BehaviorTreeCanvas.cs:77-86`), `DestroyImmediate`-ing
whatever isn't referenced at that instant, un-undoably. Consequences, all live today:

- Every tree edit dirties one central asset — a version-control chokepoint for any second contributor.
- `Instance` picks "the first repository found" (*verified*, `ScriptGraphAssetsRepository.cs:24`), so a
  duplicate asset silently forks the ledger.
- An element that transiently reports no `scriptGraphAssets` (mid-undo, mid-deserialize) can have its
  graph destroyed by timing.
- Cross-tree references to an embedded graph can be deleted out from under the other tree — spec 03
  already routes around this ("the Graph condition must be a standalone asset") rather than fixing it.

**2. Reuse is manual copying.** The `hasTarget` predicate exists once per tree that needs it, rebuilt by
hand, free to drift. Fifty agents means fifty copies of a three-unit graph.

**3. The existing attempt is the right instinct on a fragile seam.** VisualScriptingExtension's
`RunnableScriptGraph` / `ParameterizedGraphAsset` + `ScriptGraphInput`/`ScriptGraphOutput` is a
proto-Function — a referenced graph with parameters. Its execution layer (`ScriptGraphVariableExtension`)
cannot carry the weight (*all verified*):

- `GetReference()` allocates a **fresh `GraphReference` per call** — VS's `Macro.cs:59` never assigns
  `_reference`, so the null branch runs forever.
- The agent reaches the graph by **reflection into `GraphPointer.gameObject`**, a private property.
- Every call runs **three O(units) scans** to find input/output nodes, plus list/dictionary allocations.
- Output selection uses a **`static int executionOrder`** counter and `Reset()` walks — global mutable
  state deciding which result you get; not reentrant.
- Parameter binding matches `valueOutput.type == parameter.value.GetType()` — exact type only, so a
  subclass silently never binds and a null value throws. The output key `"Result"` is hard-coded.

**4. Specs 03 and 09 have homeless data.** Watched keys (Unity-BH3#13: a wrong list means a guard that
never wakes, silently), the purity flag (spec 09's guard lint), and predicate cards (spec 03) all need to
live on *the graph as an asset* — which doesn't exist. TPS already ships standalone query graphs, but
through the seam above (*verified*, `TacticalPositionSelectionQueryItem.cs:12` holds a
`RunnableScriptGraph`).

## The design: one asset type, declared contract

**Decided by the tool owner.** One asset class, not a family: **`FunctionGraphAsset`**, designer-facing
**"Function"**, create menu *ArcaneOnyx → Function Graph*, CLI prefix `fn_`.

Project vocabulary, stated once: **behaviors** (tree branches), **functions** (these), **facts** (agent
state via `AgentVariableWriter`), **queries** (functions returning a TPS query).

- The asset owns a `FlowGraph`. **The contract *is* the graph's own port definitions**
  (`valueInputDefinitions` / `valueOutputDefinitions`) plus asset-level metadata the graph cannot
  express: **purity** (default true), **watched keys**, description. No duplicate declaration list on
  the asset — so there is no asset-internal drift to manage; the drift pattern applies only where it
  must, on callers' copies.
- **Flavor is the output type**, not a subclass: `bool` → shown as a Predicate,
  `TacticalPositionSelectionQuery` → a Query, anything else → a value function. The library panel gets
  its sections from the type system; verify gets its lints from the same place.
- Rejected names, so they stay rejected: *Blueprint* (wrong referent — UE Blueprints are classes with
  event graphs; this is deliberately only the function half), *ScriptGraph-anything* (the namespace is
  already a collision minefield — see the authoring skill's BREAK-1 — and `FunctionGraph` greps clean),
  *Macro*/*Subgraph* (imply inline expansion; VS took Subgraph), *LogicGraph* (says nothing),
  *Expression* (a lie for queries, which have Enter/Exit control flow).

### Function graphs replace the two existing wrappers — neither is maintained

**Decided by the tool owner.** `FunctionGraphAsset` is not a third option beside `ParameterizedGraphAsset`
and `RunnableScriptGraph`; it is their replacement, and the consumer census makes that cheap
(*verified* — a repo-wide grep of `Assets/ArcaneOnyx` for both types):

- **`ParameterizedGraphAsset` has zero consumers** outside its own property drawer and helper. It is
  deleted outright, drawer and helper with it. Nothing migrates because nothing uses it. (The
  `CreateRunnableScriptGraphVariable` method on BH3's `BaseVisualScriptingNode` is a name coincidence —
  it returns a `BTScriptGraphVariable` and never touches the VSE type.)
- **`RunnableScriptGraph` has exactly one consumer**: `TacticalPositionSelectionQueryItem.generatorScriptGraph`
  (*verified*, `TacticalPositionSelectionQueryItem.cs:12`). It is removed once that field migrates —
  see open question 1 for the mechanics. No deprecation era, no wrapper maintained "just in case": a
  seam with one caller does not earn a compatibility layer.

### Reference by default, embed as convenience

A Function in the project is shareable by anyone. An embedded one-off (the three-unit variable read)
remains legal, owned **structurally** as a sub-asset. The bridge is one action — **Extract to project
asset** — which spec 03 already requires for suggested-guard conditions; this spec generalizes it.

Callers hold a **copy of the contract**, for the same deserialization reason `RunBehaviorTreeGraphNode`
does (`Definition()` runs during deserialize; a connection to a port key that does not exist yet is
dropped silently). Reuse the existing machinery verbatim: `RefreshParameters` /
`DescribeContractDrift`, surfaced by `bt_verify` and the node's context menu. Do not invent a second
staleness story.

### Ownership: the repository dies

Sub-asset containment is the single source of truth. Cleanup becomes: enumerate the tree's own
script-graph sub-assets, diff against what elements reference, destroy orphans — **on save, never on
OnGUI**. Standalone Functions are never candidates, so the cross-tree deletion bug class becomes
unrepresentable.

**Sequencing (decided): never two deleters live at once.**
1. Structural detection ships **report-only** — `bt_verify` reports "orphaned sub-asset" while the old
   sweep still deletes.
2. Deletion moves to save-time structural cleanup; the sweep becomes report-only.
3. The repository asset, its ledger, and `DestroyUnusedScriptGraphAssets` are removed.

## The evaluation seam

One runtime entry — *evaluate this Function with these arguments for this agent* — shared by
`VisualScriptGraphVariable`, guards, TPS, and any remaining `RunnableScriptGraph` call sites.

- **Binding plan resolved once per asset**: input/output units located, ports paired with sources into a
  flat array. Invalidated only when the asset changes. No scans, no name-and-exact-type matching in the
  call path — binding uses assignability, checked at resolve time.
- **Agent context — spike before serializing anything.** `GetVariable` (Object kind) resolves against
  the flow's ambient `gameObject`; that dependency is *why* the reflection hack exists. Recommended v1:
  a **per-agent cached `GraphReference`** with its gameObject set once at cache build — kills both the
  per-call allocation and the per-call reflection while the ubiquitous `GetVariable` idiom keeps
  working. `Self` additionally available as a declared input where a graph wants the GameObject as a
  value. The purer alternative (all agent data through declared inputs, no ambient context) stays open
  as a later tightening; it must not block v1.
- **Stateless and reentrant, stated as a contract**: a Function holds no per-call state; anything
  per-call lives in the `Flow` (which is pooled — *verified*, VS `Flow.cs:105`,
  `GenericPool<Flow>.New`). The `static executionOrder` counter and the `Reset()` walk die with the old
  seam. Pinned by a test that interleaves two agents evaluating one asset.

### Performance contract (testable, v1)

- **Zero steady-state GC allocation per evaluation** after warmup — pinned by an allocation-asserting
  test, so a later per-call addition fails at PR time, not in a profiler capture months later.
- **No reflection in the call path.** Resolve-time only.
- **`ProfilerMarker` named per Function asset**, so cost is attributable — "cost made visible," extended
  to where cost accrues.
- The guard cost display formula (`units × 1/interval × agents`, spec 09) reads Function unit counts.

### Performance posture, beyond v1

Three tiers, so nobody relitigates which fight we are in:

- **Tier 0 — shipped (spec 09):** triggers + dirty flag. Evaluation happens per *change*, not per
  frame. This is where ~90% of the system-level win already lives.
- **Tier 1 — this spec:** wrapper overhead → effectively zero. What this layer adds per call is a
  `for` over a small array plus a marker: nanoseconds.
- **Tier 2 — future, deliberately not v1:** the interpreter itself. Unity VS graph-walks with
  per-port dictionary lookups and struct boxing (slower than Unreal's Blueprint VM, which at least
  compiles to bytecode), and Unity has it in maintenance — no engine-side fix is coming. But Functions
  are **pure, contracted, and small-vocabulary**, which makes them *compilable* where general VS is
  not. UE abandoned nativization because it chased the whole Blueprint surface; this surface is one
  pure dataflow shape. **v1 constraint: nothing may preclude Tier 2** — purity, the declared contract,
  and no ambient state beyond the agent context are exactly what keep it possible.

  **The Tier 2 sketch (C# codegen), recorded so the shape is agreed before anyone builds it:**
  - The contract is the method signature; purity means no state; pure dataflow topologically sorts
    into one assignment per unit. Generation runs at build time into a `Generated` asmdef. A
    **registry** maps Function GUID + graph hash → compiled implementation; the seam consults it and
    falls back to interpretation on miss or stale hash. The editor keeps interpreting (live editing
    stays live); player builds get compiled code.
  - Reflected units — the slowest interpreted construct — are the easiest win: the unit names its
    `MemberInfo`, the emitter writes the direct call. A compiled Function is a C# node whose source of
    truth is a graph.
  - **The effort is equivalence, not emission.** VS semantics (implicit `ConversionUtility`
    conversions, null handling, `GetVariable` fallback) must be reproduced exactly; every divergence
    is a build-only bug. Defenses, both mandatory: **differential testing** (build-time evaluation of
    every Function through both paths on generated inputs, asserting equality) and **whole-graph
    fallback** — any graph containing an unsupported unit is interpreted in full, reported by verify
    ("not compiled: unit X"), never half-compiled. That rule makes unit coverage incremental and safe.
  - Scope warning with a name: Bolt 2 made full C# generation its headline and died under the
    breadth. This is the narrow subset where it stays a bounded exercise.
  - **One piece IS pulled into v1:** the registry lookup and the graph hash — a check that today
    always misses interpretation-ward. Cheap insurance so Tier 2 lands behind an existing interface
    instead of rewriting the evaluator.
- **The escape hatch stays cheapest of all:** a C# value node's `ValueOutput` wired directly to a
  guard is a delegate call — no Flow at all. The workflow remains spec 09's: prototype as a Function,
  let the profiler name the hot few, promote those to C# nodes.

## TPS consumes this

TPS already proved the asset topology (standalone, referenced query graphs); it is missing the contract
and the seam.

- A query graph becomes a Function whose declared output is `Result : TacticalPositionSelectionQuery`.
  The authoring checklist's "spelled exactly, because a wrong key shows up nowhere else" collapses into
  a contract lint with a name.
- Query parameters (`maxRange`, …) become declared typed inputs → ports on the consuming node, with
  drift reporting. Third consumer of the sub-tree parameter muscle.
- Queries appear in the spec 03 library as their own card section, classified by output type.

## BH3 integration

- `VisualScriptGraphVariable` re-points at the seam. Dump, why-panel, and `GuardTraceCapture` read the
  declared contract instead of scanning units.
- **Watched keys move onto the Function** — declared beside the graph that reads them, verified against
  it by the derivation walk (`GuardTraceCapture`'s walk, different collector). This closes the sharp
  half of Unity-BH3#13: a *wrong* list becomes detectable, not just an empty one. A guard whose
  condition is a Function inherits its keys; hand-added keys on the guard remain legal and are compared.
- **Purity is declared on the asset** (default pure); spec 09's write-walk becomes the lint that
  catches a declared-pure Function containing a write unit. Declaration is what callers rely on;
  derivation keeps the declaration honest.
- Suggested guards (spec 03) reference Functions by asset — the "must be standalone" constraint is now
  the natural shape rather than a warning.
- **C# interop:** a C# node feeding a guard directly is the promoted fast path. Its caveats are not
  performance: purity still binds (guard tracing re-pulls `ValueOutput`s on flips — the node runs twice
  per transition), and its watched keys are hand-declared and opaque. Future line item: an attribute
  letting a C# node declare watched keys the way a Function does.
- **CLI:** `fn_create`, `fn_describe`, `fn_list`, `fn_extract` (embedded → project asset), plus
  `bt_set_value` learning to connect a Function to a value port. Same "ask the project" philosophy.

## Which repo owns what

`VisualScriptingExtension` is already upstream of both consumers (*verified*: BH3's
`CreateVariableReadGraph` uses its `ScriptGraphInput`; TPS's query item holds its
`RunnableScriptGraph`). The asset class, contract types, and evaluation seam land there — this is the
module's promotion from helper grab-bag to owner of graph-as-function. BH3 and TPS consume. Three
submodule branches plus a superproject pointer bump; commit order per repo rules (submodules first).

## Locked decisions (do not re-litigate)

1. One asset type + declared contract; flavor by output type. Name: **Function** (`FunctionGraphAsset`).
2. Contract = the graph's own port definitions + asset metadata (purity, watched keys, description).
3. Lives in **VisualScriptingExtension**; BH3 and TPS depend on it, never the reverse.
4. Reference by default; embedding stays legal; **Extract to project asset** is the bridge.
5. Caller-side contract copies reuse `RefreshParameters` / `DescribeContractDrift` — no second
   staleness mechanism.
6. The repository is removed via the three-step sequencing above; never two deleters at once.
7. Purity: declared, default true, lint on mismatch (warn, matching spec 09's choice).
8. Performance contract as specified; Tier 2 (codegen) not built in v1 but never precluded — and v1
   ships its registry seam and graph hash, so the emitter lands behind an existing interface.
9. Vocabulary: behaviors / functions / facts / queries.
10. **`ParameterizedGraphAsset` is deleted** (zero consumers, *verified*) and **`RunnableScriptGraph`
    is removed** once its single consumer (the TPS query item) migrates. Neither is wrapped,
    deprecated, or maintained.

## Open questions

Blocking — decide before serializing anything:

1. **Migration mechanics for `TacticalPositionSelectionQueryItem`.** Replacement is decided (see
   *Function graphs replace the two existing wrappers*); what remains is how the one serialized field
   moves. Options: an editor migration utility that rewrites existing query items in place (reads the
   old `RunnableScriptGraph`'s asset reference and argument list, writes the Function reference and
   args), or regeneration — query items are authored via `tps_create_query_item` and the demo content
   is documented as regenerable. Recommended: the migration utility, plus a verify report naming any
   unmigrated item, so hand-authored items in downstream projects are caught rather than silently
   broken.
2. **Agent context mechanism** — per-agent cached reference (recommended) vs declared-inputs-only.
   Resolved by spike 2 below, then locked.
3. **Do new embedded one-offs become embedded `FunctionGraphAsset`s** (uniform tooling forever) **or
   stay raw `ScriptGraphAsset`** (less churn)? Recommended: new ones are Functions, existing raw ones
   keep working with a verify nudge ("promote available").

Revisit after use, not before:

4. Tier 2 compilation target (flat op array vs C# codegen) — decide when the profiler demands it.
5. Attribute for C# nodes to declare watched keys.

## To prove before building (spikes)

1. **A standalone Function evaluates against agent variables identically to an embedded graph** —
   inherited from spec 03, still the foundation stone. Same type, same call; ten minutes.
2. **Per-agent cached `GraphReference` is safe**: two agents, one asset, interleaved evaluations —
   correct isolated results, ambient `gameObject` right for each, zero cross-contamination; measure
   allocations.
3. **The zero-alloc claim survives contact with VS internals**: pooled `Flow` is verified, but measure a
   full evaluation — if `Flow`'s own dictionaries allocate per run, the performance contract's wording
   changes from "zero" to "zero from our layer," and that must be known before the test is written.
4. **Contract-copy necessity**: confirm by symmetry with `BehaviorTreeGraphParameter` that reading the
   Function asset live during caller `Definition()` loses wiring on unresolved loads.

## Tests

1. Predicate authored once, referenced from two trees; editing it changes both; deleting either tree
   leaves it untouched.
2. Two agents, one Function, interleaved: isolated correct results (kills the static-counter class).
3. Zero steady-state allocation per evaluation (per spike 3's wording).
4. Assignability binding: a subclass-typed argument binds; a wrong type fails at resolve with a named
   error, not silently at runtime.
5. Wrong output key/type on a query Function → named verify error (the "spelled exactly" test).
6. Caller drift: change a Function's inputs → every caller reports drift; refresh repairs.
7. Orphan cleanup: delete a node owning an embedded graph, save → sub-asset gone; standalone Function
   referenced by the same tree → never touched.
8. Repository removal: after step 3, editing a tree dirties only that tree's asset.
9. Declared-pure Function containing `SetVariable` → verify warns, naming asset and unit.
10. Watched-keys mismatch: Function whose declared keys omit a key its graph reads → verify reports;
    keys it declares but does not read → verify reports.
11. Reentrancy under guard tracing: a Function pulled twice on a guard flip returns consistent results.
12. Backward compatibility: existing trees, samples, and TPS query items load and run untouched.

## Files touched (expected)

- **VisualScriptingExtension:** `FunctionGraphAsset.cs` (new), `FunctionEvaluator.cs` (new seam),
  binding-plan types; `ParameterizedScriptGraph/` folder deleted (asset, drawer, helper);
  `RunnableScriptGraph/` folder deleted after the TPS migration (open question 1);
  `ScriptGraphVariableExtension` removed with them; editor: create menu, contract inspector, Extract
  action.
- **BH3:** `VisualScriptGraphVariable` re-point; `BehaviorTreeDump` / why-panel / `GuardTraceCapture`
  read contracts; verify lints (orphans, purity, watched keys, drift); save-time cleanup;
  `ScriptGraphAssetsRepository` + `DestroyUnusedScriptGraphAssets` + canvas sweep removed (step 3);
  authoring helpers + `fn_*` CLI.
- **TacticalPositionSelection:** query item binding per open question 1; authoring lints for the query
  contract.
- **Docs:** authoring skill (Function vocabulary, fn_ commands), spec 03 (cards consume contracts),
  `zombie-example.json` unaffected.

## Acceptance criteria

1. A designer creates a Function in the Project window, names it, and references it from two trees; a
   fix in the Function lands in both agents with no other action.
2. Assigning a Function to a guard or variable node grows declared typed ports; a missing required
   input is reported by `bt_verify` and visible on the node.
3. `ScriptGraphAssetsRepository.asset` no longer exists; concurrent edits to unrelated trees produce no
   shared-file merge conflicts.
4. The zero-allocation test and the two-agent reentrancy test are green and run in CI.
5. A TPS query Function with a misdeclared output is refused at verify with a named error.
6. Every existing tree, sample, and TPS query item in the repo loads and behaves identically before
   migration begins.

***

## Implementation status — foundation landed 2026-08-12

**Scope of this pass, agreed with the tool owner: foundation, then review.** The asset, the evaluation
seam, BH3's consumption of it, the `fn_` commands and docs. Deliberately **not** in this pass: deleting
`ParameterizedGraphAsset` or `RunnableScriptGraph`, migrating the TPS query item, or repository-removal
steps 2 and 3. Nothing is deleted yet, so acceptance criterion 6 holds by construction — the two seams
coexist and the legacy path is untouched.

Branch `feature/function-graphs` in the superproject and all three submodules.
Commits: VisualScriptingExtension `0a0fe1a`, BH3 `c1b8135`. TacticalPositionSelection unchanged so far.

### Spikes — all four resolved

1. **Standalone evaluates identically.** Confirmed. A Function created on disk read each of two agents'
   own variables correctly; asset ownership does not affect variable resolution.
2. **Per-agent cached `GraphReference` is safe.** Confirmed over 400 interleaved evaluations across two
   agents on one asset: zero cross-contamination, references stayed valid, and a variable written *after*
   caching was still observed — a live binding, not a snapshot. **Open question 2 is locked on the
   recommended option.** Reflection happens once at bind time; the call path has none.
3. **The zero-allocation claim survives — and is stronger than the spec expected.** The spec anticipated
   weakening "zero" to "zero from our layer" if `Flow`'s dictionaries allocated per run. They do not.
   Pooled `Flow`, graph interpretation, and value reads (boxed *and* generic) are all allocation-free.
   **The only allocating operation in an evaluation is looking a port up by string key.** So the contract
   keeps the word "zero", now conditional on something testable: the binding plan must hold direct port
   references, and no call-path code may look a port up by name. This promotes the binding plan from an
   optimization to the mechanism the contract rests on.
4. **Contract-copy necessity.** Confirmed from source without an Editor run — `BehaviorTreeGraphParameter`
   already documents the exact import-order failure. Symmetry holds; Functions get a caller-side copy.

### Decisions taken while implementing

- **The per-agent cache is owned by the caller, not a global static.** A global cache keyed by agent would
  hold destroyed GameObjects alive and need a reaper. A `FunctionBinding` held by the node dies when the
  node does, which also makes reentrancy structural rather than a rule to remember.
- **Plan invalidation is editor-time, not per-call.** Re-hashing the graph each evaluation would reintroduce
  the O(units) walk the plan exists to remove. Assets cannot change in a player build.
- **A Function declares exactly one `ScriptGraphOutput`.** Supporting several means per-call "which output
  fired" state, which is precisely the `static executionOrder` reentrancy bug being removed. A second output
  unit is now a named error. The legacy path still supports multiple, and is untouched.
- **`fn_` commands live in BH3's editor assembly**, per this spec's own *Files touched* list, and because
  that is where the pipeline command surface already is.
- **Flavor classification stays generic in VisualScriptingExtension.** The module exposes `ResultType`;
  classifying a query flavor by naming a TPS type would reverse the dependency arrow this spec locks.

### Corrections to this spec

- **The *Files touched* list omits a caller.** BH3's own `ScriptGraphVariable` base class calls
  `ScriptGraphVariableExtension` directly — a second real consumer of the execution seam, independent of
  `RunnableScriptGraph`. Re-pointing `VisualScriptGraphVariable` alone would have left half the callers on
  the reflection path. It has been re-pointed too.
- Both consumer-census claims **hold**: `ParameterizedGraphAsset` has zero real consumers,
  `RunnableScriptGraph` exactly one. The `CreateRunnableScriptGraphVariable` hits are the predicted name
  coincidence.
- The citation `BehaviorTreeAuthoring.cs:289` has drifted to **299**. Re-verify line numbers rather than
  trusting them.

### Two defects found in the old seam, beyond those the spec lists

- `ScriptGraphOutput.executionIndex` is `[Serialize]`, so **per-call execution bookkeeping is written into
  the asset** — transient state persisted to disk, dirtying assets simply by running.
- The `Run` paths in `ScriptGraphVariableExtension` never `Dispose()` their `Flow`, while the
  `GetScriptGraphOutput` paths do, so pooled flows are not returned on that path.

Both die with the old seam; neither was fixed in place, since that code is scheduled for removal.

### Test state

385 → 400 tests. EditMode is 393 total, 392 passing, with the **single pre-existing** failure
(`TpsArchitectureTests.All_concrete_PositionEvaluators_are_marked_Serializable`) unchanged. No new
failures. The 15 new tests cover spec tests 2, 4, 5 and 6 plus the contract and hash invariants.

**Note for anyone writing the remaining perf tests:** `GC.GetAllocatedBytesForCurrentThread()` returns 0
always on this Mono runtime and `GC.GetTotalMemory` is too coarse — both produce a test that passes
vacuously forever. Only `Is.Not.AllocatingGCMemory()` works, and it needs a known-allocating control beside
it. This cost two wrong measurements before it was caught.

### What shipped in this pass

| Area | State |
|---|---|
| `FunctionGraphAsset`, contract types, binding plan, evaluation seam | done, in VisualScriptingExtension |
| VisualScriptingExtension test assembly (its first) | done, 15 tests |
| BH3 `ScriptGraphVariable` + `VisualScriptGraphVariable` on the seam | done, legacy path untouched |
| `fn_create` / `fn_list` / `fn_describe` / `fn_set_metadata` / `fn_extract` | done |
| Verify lints: contract, purity, watched keys, ambiguity, orphans (report-only) | done, 11 tests |
| Demo scene | done, verified on Play with a game-view capture |
| Docs in both modules, registered in their indexes | done |

**409 EditMode tests, 408 passing.** The single failure is pre-existing and unrelated
(`TpsArchitectureTests.All_concrete_PositionEvaluators_are_marked_Serializable`). PlayMode unchanged at
7/4/3, also pre-existing. Baseline before the work was 378/377/1.

***

## Context for whoever picks this up next

**Where things live.** The asset, contract types, binding plan and evaluator are in
`Modules/VisualScriptingExtension/Runtime/Function/`. Everything BH3-facing — the `fn_` commands, the verify
lints, the import postprocessor — is in `BH3/Editor/Authoring/`, in namespace
`ArcaneOnyx.BehaviorTree.Authoring`. The demo is in the **superproject** at `BH3Demos/FunctionGraphs/`.

**The one rule that is not negotiable.** Nothing in the evaluation call path may look a port up by string
key. That is not a style preference: measurement showed pooled `Flow`, graph interpretation and value reads
are all allocation-free, and keyed port lookup is the *only* allocating operation. `FunctionBindingPlan`
exists to resolve ports once; indices are the currency afterwards. Two tests pin this, each with a
known-allocating control case beside the real assertion.

**Instruments that lie here.** `GC.GetAllocatedBytesForCurrentThread()` returns 0 always on this Mono
runtime, and `GC.GetTotalMemory` is too coarse to see one evaluation. Both produce a test that passes
vacuously forever. Use `UnityEngine.TestTools.Constraints.Is.Not.AllocatingGCMemory()` and always keep a
control that must fail.

**Traps that cost time in this pass, in order of how much:**

- `BehaviorTreeVerification.Reload` returns `Object.Instantiate(onDisk)` — a **clone with no asset path**.
  Any lint needing the path must be handed it, not derive it from the asset. A lint that derived it
  silently reported nothing.
- Inside `ArcaneOnyx.BehaviorTree.*`, `ValueInputDefinition` / `ValueOutputDefinition` resolve to BH3's own
  types, not Visual Scripting's. Qualify them. (Authoring skill, BREAK-1.)
- `FunctionGraphAuthoring.cs` deliberately does **not** `using Unity.VisualScripting` for that reason, so
  extension methods from that namespace must be called as plain statics.
- `save_all` on a freshly launched Editor sitting on an *Untitled* scene raises a modal `Save Scene` dialog
  and blocks the whole pipeline. The precaution and the hazard are the same call.
- Running the editor at all dirties `Assets/BehaviorTree.Generated/ScriptGraphAssetsRepository.asset`. It
  accumulated six rows for one owner GUID, five of them null, during this pass. Revert it; do not commit it.

***

## Remaining work, in order

Each step is independently pickup-able. They are ordered because later ones depend on earlier ones, and the
repository-removal steps are ordered for a safety reason stated in *Locked decisions* 6: **there must never
be two things deleting graphs at once.**

### Step 1 — `bt_set_value` learns to connect a Function

Small, self-contained, no dependencies. `bt_set_value` can currently give a port a literal; it should also
accept a Function asset path and wire it to a value port. Follow the existing resolve-by-asset-path pattern
in `BehaviorTreeAuthoring`. Test alongside the existing `bt_set_value` tests.

### Step 2 — Contract copies on the caller, and drift reporting for Function callers

`FunctionParameter` and `FunctionParameter.DescribeDrift` exist and are tested, but **no BH3 node stores a
contract copy yet**, so the drift lint currently has nothing to check on a node. Give
`VisualScriptGraphVariable` a serialized `List<FunctionParameter>`, declare its ports from that copy (never
from the live asset — see spike 4), and add `RefreshParameters` / `DescribeContractDrift` mirroring
`RunBehaviorTreeGraphNode`. Then hang the drift lint off the existing loop in `BehaviorTreeVerification`.
This is what makes acceptance criterion 2 fully true.

### Step 3 — Migrate `TacticalPositionSelectionQueryItem` (open question 1)

Its single serialized `RunnableScriptGraph` field moves to a Function reference plus arguments. Recommended
mechanics, per this spec: an editor migration utility that rewrites existing query items in place, **plus a
verify report naming any unmigrated item**, so hand-authored items in downstream projects are caught rather
than silently broken. `TacticalPositionSelectionAuthoring.SetGeneratorScriptGraph` is the touch point; it
reaches the field through `SerializedObject` because both are private. Note the port-key constants there
(`Result`, `_Evaluator`) — a query Function must match them, and the contract lint should say so by name.
TacticalPositionSelection is already branched (`feature/function-graphs`) and currently unchanged.

### Step 4 — Delete `ParameterizedGraphAsset` and `RunnableScriptGraph`

Only after step 3. `ParameterizedGraphAsset` has **zero** consumers (verified twice) — delete it, its
drawer and its helper outright. `RunnableScriptGraph` has exactly one consumer, which step 3 removes.
`ScriptGraphVariableExtension` goes with them. Neither is wrapped or deprecated: locked decision 10.

Two defects die with that code and should not be fixed in place: `ScriptGraphOutput.executionIndex` is
`[Serialize]`, so per-call bookkeeping is persisted into the asset, and the `Run` paths never `Dispose()`
their `Flow`.

### Step 5 — Move deletion to save time (repository removal, step 2 of 3)

Orphan detection already ships **report-only** and is tested. Now move actual deletion from the per-OnGUI
canvas sweep to a save-time structural cleanup: enumerate the tree's own script-graph sub-assets, diff
against what its elements reference, destroy orphans. At this point the canvas sweep becomes report-only.
**Never both deleting at once.**

### Step 6 — Remove the repository (repository removal, step 3 of 3)

Delete `ScriptGraphAssetsRepository`, its `.asset`, `BehaviorTreeGraph.DestroyUnusedScriptGraphAssets`, and
the canvas sweep. Acceptance criterion 3 becomes checkable: editing unrelated trees produces no shared-file
merge conflicts.

### Step 7 — Open question 3

Do new embedded one-offs become embedded `FunctionGraphAsset`s, or stay raw `ScriptGraphAsset`s?
Recommended: new ones are Functions; existing raw ones keep working with a verify nudge
("promote available"). `fn_extract` already provides the promotion path.

### Not scheduled

Tier 2 compilation. The v1 registry seam (`FunctionEvaluator.TryGetCompiled`) and the graph hash ship and
always miss, so the emitter can land behind an existing interface. Do not start it before the profiler asks.
The `NewAssembly` asmdef rename in VisualScriptingExtension is also unscheduled — it changes every by-name
reference, so it wants its own change.

### Unrelated finding worth scheduling

VisualScriptingExtension's editor assembly definition is named **`"NewAssembly"`**, not
`ArcaneOnyx.VisualScriptingExtension.Editor`. Renaming it changes every by-name reference to it, so it was
left alone rather than folded into this feature.
