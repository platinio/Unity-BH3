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

### Reference by default; embedding is being retired

**Amended 2026-08-21 by the tool owner: we do not want any more embedded graphs.** The original decision
below let an embedded one-off stay legal indefinitely. That is what the rest of this section was written
against, and it is the source of every lifetime problem in *Problem* above — an anonymous sub-asset with no
identity needs a deleter, and a deleter is what the repository, the OnGUI sweep and the orphan lint all
exist to be. Keeping embedding alive means keeping that machinery alive forever, for graphs whose only
distinction is that nobody named them.

So the target state is: **a node references a Function, and that is the only way a node gets a graph.**

- **No new embedded graphs are created by any editor surface.** Step 2c removes the last UI that created
  one (the drawer's *Open Graph* button, which minted a sub-asset whenever nothing was assigned).
  **`bt_add_variable_read` and `bt_guard_on_variable` still create them**, through
  `BehaviorTreeAuthoring.CreateVariableReadGraph`; migrating those two is item 2 of *Step 7*. Until then the
  CLI is the looser surface, which is the inversion of what 2c set out to fix and should not be left
  unstated.
- **Existing embedded graphs keep working and stay openable**, with **Extract to Function** as the
  one-click way out — the action spec 03 already requires for suggested-guard conditions.
- **The remaining embedded graphs are migrated and the field deleted** in the step that follows the
  repository removal. See *Step 7*, which is no longer an open question.

A Function in the project is shareable by anyone; an embedded graph is shareable by no one, which was
never a feature.

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
  **What compiles well, and what merely compiles.** Not every Function benefits equally, and the difference
  is decided by how its data arrives — this is the strongest argument for the "purer alternative" this spec
  parked, and a better one than purity-as-aesthetics:

  - **Declared inputs become method parameters.** A Function whose data all arrives through its declared
    inputs compiles to a clean static method — `static bool ShouldRetreat(float threshold, float hp)`.
    Nothing ambient, nothing looked up.
  - **Ambient `GetVariable` stays a lookup.** `GetVariable(Object, "hp")` has to emit as
    `Variables.Object(agent).Get("hp")`: a string-keyed lookup on a component, once per read.
  - **It cannot be hoisted into a parameter, and that is not an oversight.** The interpreter reads the
    variable at the moment the unit runs; a hoisted parameter would read once at call time. Any graph that
    writes a key and reads it back, or reads the same key either side of a write, would then disagree
    between the compiled and interpreted forms — a build-only divergence, exactly what the mandatory
    differential testing exists to catch. The emitter must keep ambient reads where they are.

  So ambient variables do **not** block Tier 2; they cap what it buys. The graph-walking, per-port
  dictionary lookups and boxing all still go, which is most of the cost. Each ambient read simply stays a
  lookup instead of becoming a parameter.

  **Authoring rule that follows:** a Function fed through declared inputs compiles materially better than
  one that reads its data off the agent. Identical behaviour today, different generated code later.

  **This is already measurable.** `FunctionGraphAsset.DeriveReadKeys()`, added for watched-key verification,
  counts exactly the ambient reads — so Tier 2 readiness is computable now and `fn_list` could surface it
  without new machinery.

  **On the wrapper being deleted:** `ScriptGraphVariableExtension`'s dictionary overload
  (`GetScriptGraphOutput<T>(asset, Dictionary<string, object>, gameObject)`) was **the declared-inputs
  path**, not the ambient one, and it is superseded rather than dropped — `TrySetArgument` does the same
  thing with indices resolved at bind time instead of names matched per call. Nothing is lost by removing
  it except passing a whole dictionary in one call, which allocated one per evaluation. Worth stating
  because the overload looks like a capability going away, and it is the *good* half of the old API that
  the new seam kept.

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
4. Reference by default. ~~Embedding stays legal~~ — **amended 2026-08-21: embedding is retired.** No
   surface creates a new embedded graph; existing ones keep working and are migrated out via **Extract to
   project asset**, after which the field is deleted. See *Reference by default; embedding is being
   retired* and *Step 7*.
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
11. **Tier 2's compilation target is C# codegen, not a flat op array.** Locked 2026-08-13, closing
    open question 4 ahead of the "revisit after use" schedule, because building multi-exit produced
    the deciding evidence rather than the profiler:
    - A Function's control flow is already C#'s control flow. Several exits are several `return`
      statements; an early return out of a loop is `foreach` + `return`. Those emit directly. In a
      flat op array the same shapes become branches and jumps, which means an emitter that has to
      linearise control flow before it can emit anything.
    - The hard part of Tier 2 was never emission, it is **equivalence** with the interpreter
      (implicit `ConversionUtility` conversions, null handling, `GetVariable` fallback). C# keeps the
      generated form readable and steppable, so a divergence found by differential testing can be
      diffed against the graph by a human. An op array cannot be read that way, which makes the
      expensive part of the work harder for no gain.
    - The Tier 2 sketch in this document was already written for C# codegen; this makes that explicit
      rather than incidental.

    **The cost, stated so it is not discovered later:** this narrows Tier 2's design space before any
    profiling has been done. If measurement later favours an op array — for instance if C# compile
    times at build become the bottleneck — reversing this is legitimate, but it is a deliberate
    reversal of a locked decision, not a free choice.

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
3. ~~**Do new embedded one-offs become embedded `FunctionGraphAsset`s** (uniform tooling forever) **or
   stay raw `ScriptGraphAsset`** (less churn)?~~ — **closed 2026-08-21 by the tool owner: neither. There
   are no new embedded one-offs.** A one-off becomes a standalone Function like any other; existing raw
   ones keep working, get a verify nudge, and are migrated out. See *Step 7*.

Revisit after use, not before:

4. ~~Tier 2 compilation target (flat op array vs C# codegen)~~ — **closed 2026-08-13, see locked
   decision 11: C# codegen.** Decided ahead of schedule because multi-exit support supplied the
   evidence: several returns and early exits express directly in C# and awkwardly in an op array.
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
- **Reaching an exit unwinds every enclosing loop**, so an exit is a real `return` rather than a dead end in
  one branch, and a Break node is no longer needed to return early from a loop. Visual Scripting tracks
  loops as a stack and exposes both `BreakLoop()` and `currentLoop`, so draining the stack unwinds any
  nesting depth in two lines. Output values are captured before the unwind, while the loop context is still
  intact. Existing graphs that break first are unaffected. This was initially dismissed on the assumption
  that one call only unwound one level — an assumption made without reading the mechanism.
- **A Function may have several `ScriptGraphOutput` units** — several `return` statements, including an early
  return out of a loop. This was briefly restricted to one, wrongly: the reentrancy bug was never the
  multiplicity, it was that "which output fired" lived in a `static int` shared process-wide and a field on
  the unit shared by every agent. Moving that state onto the `Flow`, which is pooled per evaluation, makes
  several exits safe. All exits share the graph's port definitions, so the contract stays single. The
  restriction was removed on 2026-08-13 after the tool owner pointed out it broke a pattern already in use.
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

### Two things that read as done and are not

Both surfaced in PR review, and both are declarations the docs previously described as if they were
mechanisms:

- **Purity is never enforced.** A Function declared pure can contain a write unit and will execute it.
  `bt_verify` warns, naming the unit that writes. That warning is the entire mechanism — there is no runtime
  restriction and none is planned, matching spec 09's choice for guards.
- ~~**Watched keys are declared and verified, but nothing inherits them.**~~ — **closed 2026-08-14, see
  *Step 2a landed* below.** A reactive guard now picks up the keys declared by whatever Function its
  condition reaches. Purity remains as described above: declared, warned about, never enforced.

The derivation that backs both checks (`DeriveReadKeys`, `DeriveWrites`) lives on `FunctionGraphAsset`,
beside the declarations it checks, rather than in whichever module runs the lint.

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

### Step 2 — What a caller learns from the Function it references

Three separable pieces, grouped because each is a caller learning something from the Function it references.
Any can be picked up alone, though 2c is most useful after 2b, since a picker that filters by contract is
worth more once the contract is visible on the node.

**2a — Watched-key inheritance.** ✅ **Done, 2026-08-14** — see *Step 2a landed* below.

**2b — Declared inputs become ports.** ✅ **Done, 2026-08-15** — see *Step 2b landed* below. The design
section *Step 2b — Declared inputs become ports* at the end of this document is what was built; both of its
open questions were settled by the tool owner and are recorded there.

**2c — Picking a Function by contract.** ✅ **Done, 2026-08-21** — see *Step 2c landed* below. A dropdown
that offers only the Functions that can legally fill the port being wired, the way Unreal offers Blueprint
functions matching a signature. The inspector field was a plain object field offering every Function in the
project, which made the UI looser than `bt_guard_on_function`; it is now a searchable, contract-filtered
dropdown, and the property drawer behind it is deleted in favour of a registered Visual Scripting
`Inspector`.

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

### Step 7 — Retire embedded graphs (open question 3, closed)

**Decided 2026-08-21 by the tool owner: no more embedded graphs.** The open question asked what a *new*
one-off should be; the answer is that there are no new one-offs. What remains is finishing the job.

Step 2c does the first half by removing the last creating surface. This step does the second:

1. **Migrate what exists.** A batch extract over the trees that still hold script-graph sub-assets —
   `fn_extract` is already the per-node path, so this is a loop plus an asset-path convention, and the
   demo content is documented as regenerable. `bt_verify` names anything left, so a hand-authored tree in
   a downstream project is caught rather than silently broken.
2. **Delete the field and its seam.** `ScriptGraphVariable.scriptGraphAsset`, the ambiguity state it
   creates with `function` (and the warning row that exists only to explain it), and the
   `AddObjectToAsset` calls in `BehaviorTreeAuthoring.CreateVariableReadGraph` and the old drawer.
3. **What dies with it, for free:** the orphan lint, the save-time structural cleanup built in step 5, and
   the cross-tree deletion bug class — all of them answer a question that can no longer be asked. Steps
   5 and 6 stay worth doing first regardless: they are what makes the intermediate state safe while trees
   still hold sub-assets, and *never two deleters at once* still applies.

**Ordering:** after steps 5 and 6. Doing it before them would leave the repository deleting graphs that
the migration is concurrently re-pointing, which is exactly the two-deleters state locked decision 6
forbids.

**The cost, stated so it is not discovered later.** Every one-off variable read becomes a project asset
with a path, so a tree that used to be self-contained now depends on a folder of small Functions. That is
the trade being taken deliberately: a named asset a designer can find, reuse and delete, in place of an
anonymous sub-asset that only a garbage collector knew about.

### Not scheduled

Tier 2 compilation. The v1 registry seam (`FunctionEvaluator.TryGetCompiled`) and the graph hash ship and
always miss, so the emitter can land behind an existing interface. Do not start it before the profiler asks.
The `NewAssembly` asmdef rename in VisualScriptingExtension is also unscheduled — it changes every by-name
reference, so it wants its own change.

### Unrelated finding worth scheduling

VisualScriptingExtension's editor assembly definition is named **`"NewAssembly"`**, not
`ArcaneOnyx.VisualScriptingExtension.Editor`. Renaming it changes every by-name reference to it, so it was
left alone rather than folded into this feature.

***

## Step 2a landed — watched-key inheritance, 2026-08-14

Branch `feature/watched-key-inheritance` in BH3, VisualScriptingExtension and the superproject.
TacticalPositionSelection is untouched. VisualScriptingExtension carries **documentation only** — the
`watchedKeys` tooltip and the `WatchedKeys` docstring both asserted that nothing consumed the list, which
this step makes false in the two places a designer and a maintainer are most likely to read it. No code
there changed, and the dependency arrow is unchanged: BH3 consumes VSE, never the reverse.

**What a guard now does.** On its first ask it walks backwards through its condition, collects the watched
keys declared by everything it reaches, and hands them to its On Key Changed triggers. Those keys are then
scanned alongside the hand-authored ones on every evaluation. Nothing is copied and nothing needs
refreshing: edit the Function and the next run is correct.

### Decisions taken while implementing

- **The guard's key list was never the guard's.** A guard reads nothing — `Evaluate()` pulls a bool off a
  port — so a trigger's keys are a *claim* about what the condition reads. That reframing decided everything
  else: the correct key set is not a thing an author chooses, it is a thing the condition knows.
- **Declared, not derived.** The walk collects what a node *declares*, never what a walk of its innards
  finds. An embedded graph therefore contributes nothing, which is deliberate — deriving there would make
  "declares" and "happens to read" the same word, and a runtime-computed key is invisible to any walk
  regardless. `DeriveReadKeys()` stays what it was: the check that keeps a declaration honest.
- **A capability interface, not a type test.** `IDeclaresWatchedKeys` (BH3 runtime) is implemented by
  `VisualScriptGraphVariable`, returning its Function's keys live. This follows the rule
  `ConditionalExecution` already states — everything that walks guards filters on capability, never on type
  — and the second implementer is already specified: open question 5's C# node attribute lands here without
  touching the walk. It is documented in `reactive-guards.md` as available today.
- **Resolved once per node instance, not per evaluation.** The walk allocates and runs on the path the whole
  trigger economy exists to keep cheap. Same editor-time-invalidation reasoning as `FunctionBindingPlan`: a
  graph cannot change in a player build. **Consequence to know:** rewiring a condition during Play Mode
  needs a re-enter to be picked up. `AddTrigger` resets the flag so authoring order does not matter.
- **Seeded at authoring time, inherited at runtime, reported where neither applies.** This was the tool
  owner's call between three options. `bt_guard_on_function` / `BehaviorTreeAuthoring.GuardOnFunction` seeds
  a real trigger from the Function's keys — visible and editable in the asset, exactly as `GuardOnVariable`
  already seeds from a variable name. Inheritance then keeps that trigger correct as the Function evolves.
- **Inheritance never creates a trigger, and this is the load-bearing constraint.** Creating one would make
  an existing guard evaluate *less* often than it does today, and less-often is the direction that turns a
  working guard into a silently stale one — a condition can depend on a raycast or a timer that no key can
  express, and nothing at runtime can detect that it does. So a guard with no trigger keeps its every-tick
  behaviour and `bt_verify` names it. Acceptance criterion 6 holds by construction: no existing asset
  changes behaviour.
- **A guard condition must be a `bool` Function.** `GuardOnFunction` refuses anything else by name at
  authoring time rather than letting the cast fail on the first tick.

### Verification changes, including one loosening

- Lint 4 (*no triggers, re-checks every tick*) now names the inherited keys when the condition declares any,
  and points at `bt_guard_on_function`. This is the safety net for the one path seeding cannot cover: a
  Function assigned through the inspector runs no authoring code.
- Lint 3 (*watches no keys*) was **loosened** — it no longer fires when the condition supplies keys, because
  authoring an empty key trigger and letting the Function fill it is now a legitimate shape. A test pins
  that it still fires where it always did, so the loosening did not silently delete the lint.

### A defect found and fixed on the way

`BehaviorTreeGraphTopology.Collect` — the why-panel's "which variable did this guard read" walk — was
**blind to every Function**. `FunctionGraphAsset` derives from `Macro<FlowGraph>` directly (because
`ScriptGraphAsset` is sealed), so it can never arrive through `node.scriptGraphAssets`, and the panel
silently fell back to weaker wording for any guard reading one. Fixed by consulting the same capability
interface. Widening `scriptGraphAssets` would have been the wrong fix twice over: it is also what the
repository sweep enumerates, and a standalone Function must never become a deletion candidate.

### Known gaps, stated rather than discovered

- **No lint for a hand-typed key the condition does not read.** The original design says hand-added keys are
  "compared against the inherited set", but that comparison is only meaningful against a *complete*
  declaration and there is no such thing: an embedded graph declares nothing, so every guard built by
  `GuardOnVariable` would be reported. Implementing it as specified would produce false positives on most
  existing content. The valuable half — *the condition declares keys nobody is watching* — cannot occur,
  since inheritance covers it automatically.

  **Still true on a guard, and spec 11 holds the same line there.** But the reasoning does not carry to the
  Function itself: a Function's declaration *can* be compared against its own graph, and the case that
  cannot be derived — a key computed at runtime — is the exception rather than the norm. So the Function
  inspector does offer to remove a declared key its graph never reads, behind a confirmation naming exactly
  that exception. The guard still reports nothing in this direction.
- **Fan-in through the condition is defensive, not exercised.** The walk is DAG-safe with a visited set,
  matching the two existing guard walks. But BH3 ships no two-input boolean combinator — AND is expressed by
  several guards naming one owner — so no stock content can currently build a condition with two Functions
  in it. Depth is exercised (a `Not` between guard and Function is half of what the authoring helpers
  produce); breadth is not.
- **Play-Mode rewiring needs a re-enter**, per the caching decision above.

### Tests

437 EditMode (was 416), 436 passing — the one failure is the same pre-existing
`TpsArchitectureTests.All_concrete_PositionEvaluators_are_marked_Serializable`. PlayMode 49 (was 47), 42
passing, the same 7 pre-existing failures. 21 new EditMode tests plus 2 new PlayMode tests.

The two PlayMode tests are deliberately a pair, because either alone proves nothing: one asserts the guard
wakes on the key its Function declares *with a trigger naming no keys of its own*, the other that it stays
asleep while an undeclared fact is written every frame. Passing both is only possible if inheritance is
doing specific work rather than making the guard permanently due.

### Demo

`Assets/ArcaneOnyx/BH3Demos/WatchedKeyInheritance/` (superproject), reusing the existing `IsHurt.asset`.
The tree is a real asset, `HurtOrIdle.asset`, authored through `bt_guard_on_function` — so the seeded
`on hp changed` trigger is visible on the guard in the canvas, which is the half of this feature a designer
actually touches. Three agents run that one asset, each clearing or removing something on its own running
copy.

Measured on a ~1000-frame run: the inheriting guard ran its condition graph 79 times (8% of frames) while
holding the guarded branch; the agent receiving only undeclared writes ran it **once** and sat on the
fallback; the control with no trigger ran it every frame. The first and third agree on the decision and
differ 13× in cost. No key is typed anywhere in the demo.

### Embedded trees got the coverage they never had

Building a tree in code — `CreateInstance`, `graph.Nodes`, `SetupTransition` — is public API used by every
play-mode fixture, and was entirely untested. `Test/PlayMode/EmbeddedTreeTests.cs` (6 tests) pins it: that
such a tree runs at all, that guards and literal-fed ports survive the machine's `Instantiate`, and both
halves of the sibling-priority rule.

**That rule is documented in only one half, and the missing half misleads.** `bt_add_node` says execution
order is canvas X. `BehaviorTreeGraph.SortIntoPriorityOrder` actually uses **transition indices when they
form exactly `0..n-1`, each used once**, and falls back to canvas X only when they do not — a gap means
something was removed without renumbering, a duplicate means two children claim one priority, and neither
order is trustworthy. So a tree built in code with `CountTransitionsFromNode` has well-defined priority
regardless of where its nodes sit, which is why the demo's original all-at-origin layout was a readability
problem and not a correctness one. Both directions now have a test.

***

## Step 2b — Declared inputs become ports

**The problem, stated plainly.** A Function declares typed inputs. A `VisualScriptGraphVariable` node has no
input ports at all, so the only way to supply one is to declare an agent variable that happens to share the
input's name — `StageArguments` matches by string against the node's flattened variable scope. Nothing about
that is visible on the node: not the input's existence, not its type, not whether anything is feeding it.
2a's demo hit it immediately — `IsHurt` declares a required `threshold`, and the Function throws
`KeyNotFoundException` on its first evaluation, naming the key but not the node.

**Decided: ports replace name matching entirely.** Name matching is invisible to a designer, which is the
whole objection — a contract nobody can see is not a contract. The window to remove it cleanly is now: it
applies only to the Function path (`StageArguments`), embedded graphs go through the untouched legacy seam,
and Functions are days old, so the only content relying on the name coincidence is this feature's own demo.
That window closes as soon as anyone authors with Functions in earnest.

### The mechanism already exists in this codebase

**Dynamic input ports on a BH3 node are possible, and `RunBehaviorTreeGraphNode` has been doing it all
along** (`Definition()`). This is worth stating because the variable-matching workaround was written on the
belief that they were not — the belief is wrong, and the reason it looked true is the third bullet:

- **The non-generic overload.** `ValueInput(Type, string)` and `ValueInput(Type, string, object)`, not
  `ValueInput<T>(...)`. The port's type comes from data rather than from a compile-time type argument.
  Reached for with the generic form, this genuinely is impossible.
- **`RefreshParameters()` → `Define(); PortsChanged();`** rebuilds the ports when the contract changes.
- **Ports are declared from a serialized copy on the node, never from the referenced asset.** `Definition()`
  runs *during deserialization*, and connections are resolved by port key — a connection to a key that does
  not exist yet is dropped **silently**. Declaring ports by reading the Function live therefore loses wiring
  on any load where the asset is not resolved yet: an import-order failure, so it appears on one machine and
  not another. This is spike 4, already confirmed, and it is why the copy is not redundant.

So 2b is `RunBehaviorTreeGraphNode`'s pattern applied to `VisualScriptGraphVariable`, and the half that
usually costs the most already ships and is tested: `FunctionParameter`, `FunctionParameter.ReadContract`
and `FunctionParameter.DescribeDrift`.

### Defaults are the Function's, overridden at the call site

No new mechanism. A Function's port definition carries `hasDefaultValue` / `defaultValue`, `ReadContract`
copies them onto the `FunctionParameter`, and the node picks the overload accordingly:

- **Optional** (the Function declares a default) → `ValueInput(type, name, default)`. Safe to leave
  unconnected.
- **Required** (no default) → `ValueInput(type, name)`. Leaving it unconnected becomes the unset-port case
  `bt_verify` already reports — replacing today's `KeyNotFoundException` at first evaluation with a named
  finding before anything runs.

A call site that wants a different value connects a node or sets an inline value, which is what
`bt_set_value` already does. The Function states the sensible default once; call sites disagree with it
explicitly rather than by coincidence of naming.

### Node sizing — decided, and it is an existing defect

**Decided: the editor writes the size when the contract changes.** `ResizeToFitPorts` runs where a contract
changes — assigning a Function, `RefreshParameters` — and writes `Position`: height from the port count,
width from the longest port label measured with `GUI.skin`, which only editor code can do. The result is
serialized like any other layout, so an author can still drag-resize afterwards and it holds until the
contract changes again.

**Decided: one mechanism, both nodes.** This is not new breakage introduced by 2b — it is live today on
sub-trees. Nothing in BH3 overrides `StartingSize`, so every node is created at `BaseGraphNode`'s
`150 × 100`, and a sub-tree node with six parameters is drawn at that size. The canvas does not grow to fit;
it **squeezes**, and says so:

```csharp
// clamped so a node with many ports does not spill past its box
float step = Mathf.Min(size + spacing, (p.height - size) / count);
```

The number needed is already computed — `BehaviorTreeNodeElementWidget.GetPortSectionHeight()` — and simply
never reaches `Position`. So the work is plumbing an existing measurement into an existing field, applied to
both nodes that declare ports from a contract. Two nodes with dynamic ports and only one that sizes
correctly is the asymmetry that gets copied rather than fixed.

Rejected: overriding `StartingSize` on the node (read only at creation, so it never reacts to a contract
change, and runtime code cannot measure text), and growing the drawn box at draw time (`Position` is what
hit-testing and connection routing read, so the drawn box and the stored rect would disagree).

### What this makes true

Acceptance criterion 2 — *"assigning a Function to a guard or variable node grows declared typed ports; a
missing required input is reported by `bt_verify` and visible on the node"* — is currently false in every
part. This is what makes it true.

It also makes the spec's own authoring rule followable for the first time: *a Function fed through declared
inputs compiles materially better than one that reads its data off the agent.* Until a node has ports, there
is no way to feed a declared input from a call site, so every BH3 Function is pushed into the ambient style
— the one that caps what Tier 2 can buy.

### Work, in order

1. `VisualScriptGraphVariable`: serialized `List<FunctionParameter>`, ports declared from it in
   `Definition()`, plus `RefreshParameters` / `DescribeContractDrift` mirroring the sub-tree node.
2. `StageArguments`: read from the ports instead of from the variable scope. Name matching is deleted, not
   deprecated — the two coexisting is exactly the "which one wins" ambiguity ports exist to remove.
3. Drift lint hung off the existing `FunctionProblems` loop in `BehaviorTreeVerification`.
4. `ResizeToFitPorts` in the editor, applied to `VisualScriptGraphVariable` and `RunBehaviorTreeGraphNode`.
5. Migrate the step 2a demo, which currently supplies `threshold` through a same-named agent variable and is
   the one piece of content that relies on the mechanism being removed.
6. `bt_refresh_sub_tree_ports` gains a Function equivalent, or is generalised to both.

### Open, and worth settling before building

- **What happens to a connection when a refresh removes its port?** The sub-tree node has the same question
  and answers it by silence — the port disappears and the connection with it. Drift reporting names it
  first, which is the mitigation, but a `bt_verify` finding is not the same as an undo.
- **Whether `ResizeToFitPorts` should ever shrink a node an author widened by hand.** Growing to fit is
  clearly right; discarding a deliberate manual size is less obviously so.

***

## Step 2b landed — declared inputs become ports, 2026-08-15

Branch `feature/function-graph-ports`, **stacked on `feature/watched-key-inheritance`** rather than cut from
`main`, because 2a was still in review. BH3 and the superproject carry changes.
**VisualScriptingExtension is untouched** — `FunctionParameter`, `ReadContract` and `DescribeDrift` already
shipped complete in the foundation pass, so this step only had to call them. TacticalPositionSelection is
untouched (step 3 is not in this pass).

Acceptance criterion 2 — *"assigning a Function to a guard or variable node grows declared typed ports; a
missing required input is reported by `bt_verify` and visible on the node"* — was false in every part and is
now true.

### The trap that decided the implementation

`FunctionBindingPlan.Resolve` **skips any declared input with no live port** on the graph's
`ScriptGraphInput` unit (`FunctionBindingPlan.cs:181`), while `FunctionParameter.ReadContract` lists **every**
declared input. The two lists can therefore disagree in length *and* order, so the obvious implementation —
stage the node's argument *i* into plan input *i* — silently feeds the wrong argument to the wrong parameter.
No error anywhere; the Function just returns a wrong number.

It is not a corner case: it is what ordinary drift looks like from the inside, and it is reachable any time a
caller has not been refreshed. So arguments resolve **by name to an index once**, and by index thereafter —
the same resolve-time/call-time split the binding plan itself uses. `FunctionPortTests.AStaleLeadingPort_…`
pins it, and it fails if anyone replaces the map with positional staging.

### Decisions taken while implementing

- **The node owns its arguments; `ScriptGraphVariable` owns the binding.** They meet at a new
  `IFunctionArguments` (BH3 runtime) that the node implements over its own ports. Pushing ports down into
  `ScriptGraphVariable` would put Visual Scripting port types into a helper with no business knowing them;
  handing the binding up would let a caller stage against a plan that had since been rebuilt. The interface
  is read positionally rather than as a dictionary because every allocation here is one per evaluation.
- **It lives in BH3, not VisualScriptingExtension.** Its only implementers are BH3 nodes, and step 3's TPS
  query item will carry serialized arguments rather than ports, so it would not implement this. Promoting it
  to VSE later is a move, not a redesign.
- **The argument map is invalidated by three things**: the plan changing (compared by reference, one check
  per evaluation), the argument count changing, and an explicit `InvalidateArgumentMap()` from `SetFunction`
  and `RefreshParameters`. The third exists because renaming a parameter changes neither of the first two.
  All three are editor-time; nothing in a player build can rename a port.
- **Assigning a Function refreshes the ports as part of assigning**, rather than leaving it as a second step
  to remember. A node pointed at a Function whose inputs it does not declare cannot be fed at all.
- **The `Variables` / `VariableDeclarations` overloads now stage nothing** for a Function. They describe the
  ambient scope, which is no longer how a Function is fed. `VisualScriptingNode`'s four lifecycle graphs are
  the only other callers — see the next section, where the reasoning about them was initially wrong.
- **A required input's default lives on the port, not in the evaluator.** An input nothing stages has no
  value at all and the graph reads it as a missing key — that *is* the `KeyNotFoundException` this step's
  problem statement cites.
- **The two drift directions are not symmetrical, and only one of them breaks anything.** A call site
  holding a port for an input the Function has *dropped* is harmless: nothing inside the graph reads it any
  more, so the argument is skipped. An input the Function declares that the call site has *no port for* is
  the opposite — nothing supplies it, and the graph throws the bare `KeyNotFoundException` above, naming the
  key and nothing else, which points at the Function when the thing to fix is the caller's stale copy. That
  case is now **refused where the argument map is resolved**, naming the Function, the input, the call site
  and the repair — so it costs nothing per evaluation, and it does not depend on anyone having run
  `bt_verify` first. `IFunctionArguments.CallSiteName` exists for that message: an unsupplied input is the
  call site's debt, so the call site is what gets named.

### The two open questions, settled by the tool owner

- **A refresh that removes a connected port still removes it, but now names what it dropped and what was
  feeding it.** Applied to `RunBehaviorTreeGraphNode` as well, so the two contract-driven nodes agree; the
  sub-tree node's previous answer was silence. `RefreshParameters` returns those lines on both nodes, and the
  canvas logs them as warnings — losing a wire is the one part of a refresh nobody asked for.
- **`ResizeToFitPorts` writes the exact fit**, discarding a manual resize, rather than growing only. That is
  safe *because* the trigger is a discrete authoring action — assigning a Function, or refreshing either
  node's parameters. It deliberately never runs from the draw path, on selection, or on load; an exact fit
  stamped every frame would fight an author mid-drag.

### A wrong assumption, caught in review, and what it changed

The first version of this step was written believing that **a Function can never reach
`VisualScriptingNode`'s four lifecycle graphs**, because all four early-out on `?.ScriptGraphAsset == null`.
That is wrong, and the way it is wrong matters:

- `scriptGraphAsset` and `function` are independently settable and never clear each other.
- The inspector drawer carrying the Function field is registered for **`BTScriptGraphVariable`** — which is
  exactly what all four lifecycle fields are. It already renders an "assigned both" warning and calls that
  "a state a designer can now reach by dragging".
- With both assigned, the early-out does not fire, the Function *does* run — and after this step it runs
  **with no arguments**, where previously it received the ambient name-matched ones.

So the change quietly removed the only mechanism those fields had, in a state reachable by dragging, with
nothing reporting it. The lesson is narrow and worth keeping: *"the guard clause makes this unreachable"* is
only true if the guard's condition cannot be satisfied alongside the state you are dismissing.

**What was done about it.** Not a behaviour change — those fields having no port mechanism is a design gap
larger than this step. Instead the state is now **reported**: `VisualScriptingNode` exposes its
`LifecycleGraphs`, and `bt_verify` names any Function assigned to one, saying it cannot be passed arguments
and to read it from a Script Graph Variable node instead. That covers both silent shapes at once — the
Function that never runs, and the Function that runs unfed. Pinned by
`Verify_ReportsAFunctionAssignedToALifecycleGraph`.

**Still open, and deliberately not taken here:** giving those four fields a real port story. It is four
contracts on one node, which needs designing rather than deciding in passing.

### An existing defect fixed on the way

Node sizing was already broken for sub-trees and had nothing to do with Functions: nothing overrides
`StartingSize`, so every node is created at `BaseGraphNode`'s 150×100, and the canvas responds to too many
ports by **clamping the spacing between them** rather than growing. `ContractPortLayout.ResizeToFitPorts`
serves both nodes.

Its measurement has a wrinkle worth knowing: `GUI.skin` throws outside `OnGUI`, and the callers that matter
most (`fn_refresh_ports`, `bt_refresh_sub_tree_ports`) are pipeline commands that never run inside one. So
the real measurement is attempted and a per-character estimate is used when it is unavailable. That affects
width only, and only until the next refresh made from the canvas.

### What grew out of this step, and now lives in spec 11

Step 2b's drift lint was reported by `bt_verify` and nowhere else, which the tool owner pointed out is an
agent surface: a designer never runs it. Making drift visible on the canvas turned into a general mechanism —
any node reporting what is wrong with it, drawn as a badge — and then pulled in guard scheduling and the
Function's own inspector.

That is **[spec 11 — node problems](11-node-problems.md)**, implemented on the branch stacked directly on
this one. It closes Unity-BH3#22 and supersedes two things written below:

- The step 2a note that a wrong watched-key list is *"detectable, not just an empty one"* was true only of
  `bt_verify`. It is now on the canvas, with the repair attached.
- Verification's name-based `PortsSafeToLeaveUnset` allowlist is gone; whether an unconnected port is a
  defect is declared at the port.

### Known gaps, stated rather than discovered

- **`ContractPortLayout` reads the widgets' own styles** for port spacing and port chrome, rather than
  copying the numbers, so those cannot drift. **One number is still a literal**: the `70` header allowance,
  which is a bare literal inside `BehaviorTreeNodeElementWidget.CachePosition` and so has nothing to
  reference. Naming it there is a change to the widget layer rather than to this feature.
- **`VisualScriptingNode`'s lifecycle graphs still have no port story** — now reported rather than silent,
  per the section above.
- **The `Unfed` case is covered by tests and verify, not by the demo.** An agent whose required port is
  unconnected throws every tick, so demonstrating it live means either console spam or a disabled machine
  that is not really running. `FunctionPortTests.AnUnconnectedRequiredPort_FailsNamingTheNodeAndTheInput`
  covers the message instead.
- **End-to-end evaluation through a node's `Output` is not EditMode-testable** without a running machine, so
  the coverage splits: the node's port→argument half in `FunctionPortTests`, the argument→Function half in
  `FunctionBackedScriptGraphVariableTests` with an `IFunctionArguments` double, and the whole path in the
  demo.

### Work item 6, and a deviation from the design section

The design says *"`bt_refresh_sub_tree_ports` gains a Function equivalent, or is generalised to both."* The
tool owner chose a **separate `fn_refresh_ports`**; `bt_refresh_sub_tree_ports` keeps its name, its node type
and its behaviour, and gained only the dropped-connection report and the resize.

### Tests

**468 EditMode, 467 passing** at the point step 2b merged `main`. The single failure is the long-standing
`TpsArchitectureTests.All_concrete_PositionEvaluators_are_marked_Serializable`, unrelated to Functions.
PlayMode unchanged at 56/49/7.

> Later work on the same stack took this to **483 EditMode, 482 passing**, and then added further changes
> that have not been run against the suite yet. See **spec 11** for what followed; treat the number here as
> the 2b figure rather than the current one.

25 new EditMode tests — 16 in `BH3/Test/EditMode/FunctionPortTests.cs` and 9 in `NodeProblemTests.cs` —
plus four rewritten in `FunctionBackedScriptGraphVariableTests.cs`, which exercised the name-matching path
this step deleted and now stage through an `IFunctionArguments` double.

### A failure this branch inherited, since fixed upstream

While this work was in progress,
`WatchedKeyInheritanceTests.AGuardWithNoTriggers_WhoseConditionDeclaresKeys_IsReportedWithThoseKeysNamed`
failed on the base branch — 2a's own section above reported that suite green, and it was not. It was left
alone here rather than fixed, because the fix was a design call for 2a's author. **It is fixed on `main`
(commit `25ad8c4`) and green after the merge.** Recorded because the *reason* it failed is worth keeping:

`GuardScheduleSeeder` (`OnWillSaveAssets`) re-seeds any guard that has no triggers from what its condition
declares. The test cleared the triggers and then saved — so the seeder put one back, lint 4 never fired, and
the test was quietly asserting against the seeder rather than the lint. The fix reproduces the situation that
actually happens instead: a Function gains a watched key *after* the trees referencing it were saved, so
nothing re-seeds them and the guard is genuinely unscheduled.

The design question underneath it still stands and is not closed by that fix: lint 4's Function branch
justifies itself with *"a Function assigned through the inspector runs no authoring code"* — but the seeder
hooks **save**, and the inspector path saves too. The overlap is real; the fixed test just no longer depends
on it.

### Demos

- **`BH3Demos/FunctionPorts/`** (new). One Function and one tree asset, four agents, four different
  `threshold` arguments — 80, 15, 95, and the Function's own declared default of 50. At the same `hp` they
  give different answers, which is the thing that was impossible before: previously the argument came from a
  same-named agent variable, so a second opinion needed a second copy of something. The left panel derives
  the contract from the asset with `FunctionParameter.ReadContract` and shows the node's live drift state, so
  it cannot disagree with `fn_describe` or the verify lint. Verified on Play with a game-view capture.
- **`BH3Demos/WatchedKeyInheritance/`** (migrated). It supplied `IsHurt`'s required `threshold` by declaring
  an agent variable of the same name — the one piece of content relying on the mechanism this step removed.
  It now feeds a real port from a Float Literal of 60 in `HurtOrIdle.asset`, and the demo script no longer
  declares `threshold` at all. Confirmed at runtime: port connected, value 60, no `threshold` variable on any
  agent, evaluation unchanged.

***

## Step 2c — Picking a Function by contract

Requested by the tool owner 2026-08-14, by analogy with Unreal: you choose a Blueprint function from a
dropdown that only offers functions matching the signature you are filling. Here the signature is the
declared contract, and for a guard condition it is *returns `bool`*.

**What is wrong today.** The inspector field added in step 2a is a plain object field, so its picker lists
every `FunctionGraphAsset` in the project — a query Function, a float Function, a Function with no `Result`
at all. Assigning one that cannot work is a click away, and the failure arrives later as a cast exception at
the first tick. Note the asymmetry this creates: `bt_guard_on_function` **refuses** a non-boolean Function
by name at authoring time, so the CLI is currently stricter than the UI. That is backwards — the UI is where
the mistake is easiest to make.

### The filter is the target port's type, and it has to be found rather than declared

The obvious implementation — ask the node what type it wants — does not work, and knowing why saves
somebody an afternoon:

- `VisualScriptGraphVariable.Definition()` builds its graph with `typeof(object)` and declares
  `ValueOutput<object>`. The node is deliberately untyped, which is what lets one node type serve
  predicates, floats and queries alike.
- So the constraint lives **downstream**: it is the type of the `ValueInput` this node's `Output` is
  connected to. A guard's `Value` is `ValueInput<bool>`, and that is the only thing in the graph that knows
  `bool` is required.

So the picker resolves its filter by following `Output`'s connections, not by asking the node:

| Node's Output | Offered |
|---|---|
| connected to a `bool` port | Functions whose `ResultType` is assignable to `bool` |
| connected to a `Component` port | Functions returning `Component` **or a subclass** — assignability, matching the rule `FunctionBinding.TrySetArgument` already uses for arguments |
| connected to several ports | the intersection; empty means the wiring itself is contradictory and should say so |
| unconnected | everything, grouped by flavor — there is no constraint to apply, and inventing one would stop an author wiring the node up afterwards |

**Assignability rather than exact type is not a detail.** The old seam compared
`valueOutput.type == parameter.value.GetType()` and so silently never bound a subclass; the evaluation seam
replaced that with assignability on purpose. A picker that filtered on exact equality would reintroduce the
same wrongness one layer up, hiding a `Transform` Function from a `Component` port.

### Classification is shared with the library panel, not reimplemented

`FunctionGraphAuthoring.DescribeFlavor` and `FunctionGraphAsset.ResultType` already classify a Function as
predicate / query / value, and `FindFunctions` already enumerates them. Spec 03's card sections are built
from the same two things. **The dropdown must read them rather than grow its own rules** — two surfaces that
disagree about what counts as a predicate is precisely the drift a single derived classification exists to
prevent.

The two surfaces are complementary, not alternatives, and 2c does not depend on 03 shipping:

- **Spec 03's library panel** — browse and search everything in the project, drag onto a canvas. A
  discovery surface.
- **2c's dropdown** — pick, at the port you are filling, from what can legally go there. A completion
  surface.

### Shape

A searchable dropdown (`AdvancedDropdown`) rather than an `EditorGUI.Popup`: a project with fifty Functions
is the case this feature exists for, and a flat popup of fifty entries is worse than the object field it
replaced. Each entry shows the Function's name, its flavor, and its required inputs, so the thing a designer
is about to owe the node is visible before they choose. Plus **None** to clear, and — worth considering —
**Create new Function…**, which is the moment an author most often discovers the one they want does not
exist yet.

### Open, and worth settling before building — all settled 2026-08-21

- ~~**Reaching the owning node from a `PropertyDrawer`.**~~ — **spiked 2026-08-16, answered below.**
- ~~**Whether an unconnected node should offer everything or nothing.**~~ — **everything, grouped by
  flavor.** Authors wire up in whatever order they like, and a picker that refuses to list anything until
  the node is connected teaches them that the feature is broken. The counter-argument — that an
  unconstrained node is where a wrong choice hides longest — is answered by the grouping and by
  `bt_verify`, not by an empty list.
- ~~**Whether the drawer should also refuse a mismatch already assigned.**~~ — **no: it keeps the
  reference and draws it in error, naming the mismatch.** A picker that silently dropped an assignment
  because a `Result` type changed underneath it would destroy authored work to enforce a rule the author
  cannot yet see. Showing it broken is what lets them fix it. **`bt_verify` reports it too** — it did not
  when this was written, and the claim that it did was corrected by making it true: `FunctionProblems` now
  compares the Function's `ResultType` against the ports the node feeds, per node. Nothing else can catch
  it, because the node's `Output` is `object` and the port-level lint therefore sees a legal wire whatever
  the Function returns.
- **Where a new Function goes** (decided with the tool owner, 2026-08-21) — **a save-file dialog**
  (`EditorUtility.SaveFilePanelInProject`), defaulting to the folder beside the tree. A naming convention
  such as `<TreeFolder>/Functions/<Tree>.<Node>.asset` would have to be shared with `bt_add_variable_read`
  and step 7's migration to be worth anything; inventing it here, in the one surface where the author is
  present and can just say where, is the wrong place to start.
- **Whether this step still offers to create an embedded graph** (decided with the tool owner, 2026-08-21)
  — **no.** The old drawer's *Open Graph* button minted a sub-asset whenever the node had nothing
  assigned, which is how most embedded graphs in this project came to exist. It is replaced by *Create new
  Function…*. A node that **already** holds an embedded graph keeps *Open Graph* and gains *Extract to
  Function*, so nothing existing becomes unreachable. This is the first half of *Step 7*; see
  *Reference by default; embedding is being retired*.

### The dropdown is Unity's `AdvancedDropdown`, not this project's module

The project has a first-party `Assets/ArcaneOnyx/Modules/AdvancedDropdown`, and it was the obvious
candidate — `ShowDropdown<T>(List<DropdownItem<T>>, Action<T>)` with a search field is close to the
required shape. **It is the wrong choice here, for a reason that is about BH3 rather than about the
control:** `ArcaneOnyx.BehaviorTree.Editor` does not reference that module today (*verified* — its asmdef
lists BehaviorTree, GraphCore, GraphCore.Editor, UnityExtensions and VisualScriptingExtension, and nothing
else first-party). BH3 is a tool other projects consume, so every asmdef reference it grows is a submodule
its consumers must also take. Paying that for a picker, when Unity ships
`UnityEditor.IMGUI.Controls.AdvancedDropdown` in the editor itself, is a dependency bought with nothing.

Three things follow from using Unity's, and they shape the entries rather than merely permitting them:

- **Grouping is native.** `AdvancedDropdownItem` nests, so *predicate / query / value* become parent nodes
  and the flavor is read off the group the entry sits in. The project module is a flat list, so the same
  grouping would have had to be faked into every row's text.
- **It is IMGUI**, like the Visual Scripting inspector that hosts it. The project module is a UIElements
  `EditorWindow`, which is a second UI framework opened from inside an `OnGUI` call.
- **Search is built in** and matches the item name, so putting the required inputs in the row makes
  `Agent` a usable query for free.

An entry therefore reads `HasTarget — needs Agent, Radius` under a `Predicates (bool)` group: name,
flavor and required inputs, which is what this step promises to show, without a custom row renderer.

### Spike: how a Function field reaches its owning node — resolved 2026-08-16

Run against the live editor, because two plausible readings of the code gave opposite answers and only the
running inspector-resolution settles it.

**How BH3 node fields are actually drawn.** Visual Scripting resolves an inspector **per type**, and the
order it resolves in is what matters here — confirmed by asking `InspectorProvider.instance` directly:

| Type | Resolves to |
|---|---|
| `GuardTrigger` | `ArcaneOnyx.BehaviorTree.GuardTriggerInspector` — a registered VS `Inspector` |
| `BTScriptGraphVariable` | `Unity.VisualScripting.CustomPropertyDrawerInspector` |

So a registered VS `Inspector` wins; failing that, **VS bridges to a Unity `CustomPropertyDrawer`**. That
bridge is the part worth knowing, because it is easy to conclude from the source that the drawer is dead
code — `BTScriptGraphVariable` only ever appears as a `[Serialize] [Inspectable]` member of a VS unit, never
as a `[SerializeField]` on any `MonoBehaviour` or `ScriptableObject`, and nothing in BH3's editor assembly
constructs a `SerializedObject` at all. `GuardTriggerInspector`'s own doc states the general rule that way.

It is nonetheless wrong for this type. **The drawer does run, and the Function field added in step 2a is
live.**

**Why it still cannot see the node.** `CustomPropertyDrawerInspector` derives from `Inspector`, so *it* has
`metadata`. But it hands the drawer only a `SerializedProperty`, and that property does not belong to the
tree asset: VS synthesises a host, `SerializedPropertyProvider<T> : ScriptableObject`, copies the value onto
it, lets the drawer edit it, and copies back. So inside `OnGUI`,
`property.serializedObject.targetObject` is a throwaway provider object, and the owning
`VisualScriptGraphVariable` is **not in that object graph at all**.

This is structural, not an oversight to work around. A `PropertyDrawer` for this type can never see the
node, and therefore can never see `Output`, its connections, or the port type the picker must filter on.

**The route that does work.** A VS `Inspector` registered for `BTScriptGraphVariable` takes precedence over
the bridge, and receives `Metadata` — whose public surface includes `parent`, `root`, `value`, `path` and
`definedType` (verified on the live type). `metadata.parent` is the containing node's metadata, so the
picker can reach `Output` from there. `GuardTriggerInspector` is the working precedent for exactly this
shape: a registered VS inspector drawing a nested type held inside a BH3 node.

**Consequence for 2c: the picker is a VS `Inspector`, and it replaces the property drawer** rather than
extending it. That also folds in the drawer's existing job — the Function field and the Open button — since
registering an inspector takes the bridge out of the picture entirely.

**One correction this spike forces elsewhere.** `BehaviorTreeVerification.FunctionProblems` carries a
comment justifying its lifecycle-graph lint with *"the inspector drawer is registered for
`BTScriptGraphVariable` and so offers the Function field on all four of these too."* The conclusion is
right — the field is offered on all four lifecycle graphs — but it is right by way of the bridge, not
because a drawer binds directly. Worth correcting when the inspector replaces the drawer, since the sentence
will otherwise describe machinery that no longer exists.

**The chain, executed rather than assumed.** Run against a real node in `HurtOrIdle.asset`; nothing here is
inferred:

```
Metadata.Root().StaticObject(node).Member("ScriptGraphVariable", Instance | NonPublic)

  path                     Root.VisualScriptGraphVariable.ScriptGraphVariable
  definedType              BTScriptGraphVariable
  parent  (one hop)        ObjectMetadata, value is the VisualScriptGraphVariable — reference-equal
```

**`metadata.parent` is exactly one hop, and its `value` is the node.** No intermediate member metadata to
step over.

From there the filter type resolves the whole way:

```
node.Output.connectedPorts
  -> ArcaneOnyx.BehaviorTree.ValueInput { key = "Value", Type = System.Boolean }
     owner: BooleanReactiveGuard
```

So a guard condition yields `System.Boolean`, which is precisely the constraint 2c filters on. **Nothing
blocks building it.**

Three practical notes for whoever writes it:

- `ScriptGraphVariable` is a **private** field, so the `metadata["Name"]` indexer is not enough —
  `Member(name, BindingFlags.Instance | BindingFlags.NonPublic)` is.
- The port type lives on `Type` (capital) of **BH3's** `ArcaneOnyx.BehaviorTree.ValueInput`, not on a
  lowercase `type` as Visual Scripting's own ports use. Guessing the VS spelling silently returns nothing.
- `Output.connectedPorts` is the direct route; walking `connections` and reading `destination` works too but
  yields nulls for the control-flow entries mixed into the same list.

***

## Step 2c landed — picking a Function by contract, 2026-08-21

Branch `feature/function-picker-by-contract`, cut from `main` in **BH3** and the **superproject**.
VisualScriptingExtension and TacticalPositionSelection are untouched — the filter reads
`FunctionGraphAsset.ResultType` and `FunctionGraphAuthoring.DescribeFlavor`, both of which already shipped.

Acceptance: a Script Graph Variable node now offers only the Functions that can legally fill the port it
feeds. The UI is no longer looser than `bt_guard_on_function`.

### What shipped

| File | What it is |
|---|---|
| `Editor/Function/FunctionPortConstraint.cs` | **new** — what a node's value is required to be, read off what its `Output` feeds |
| `Editor/Function/FunctionPickerCatalog.cs` | **new** — which Functions satisfy that, and how each row reads |
| `Editor/Inspector/BTScriptGraphVariableInspector.cs` | **new** — the VS `Inspector`, the dropdown, and the buttons |
| `Editor/VisualScripting/BTScriptGraphVariablePropertyDrawer.cs` | **deleted** — replaced outright |
| `Editor/Authoring/BehaviorTreeVerification.cs` | comment correction the spike required |
| `Test/EditMode/FunctionPickerTests.cs` | **new** — 16 tests |
| `docs/functions.md`, `docs/ports-and-wiring.md` | documentation |
| `BH3Demos/FunctionPicker/` *(superproject)* | showcase tree, six Functions, builder, window, README |

### Decisions taken while implementing

1. **The dropdown is Unity's `AdvancedDropdown`, not the project's `AdvancedDropdown` module.** The design
   section originally said the opposite and was corrected during implementation: `ArcaneOnyx.BehaviorTree.Editor`
   does not reference that module (*verified* — its asmdef lists BehaviorTree, GraphCore, GraphCore.Editor,
   UnityExtensions, VisualScriptingExtension and nothing else first-party). BH3 is consumed as a tool, so an
   asmdef reference it grows is a submodule its consumers must take. Unity's also nests, giving the
   flavor grouping for free, and is IMGUI like the inspector hosting it.

2. **`Satisfies` uses convertibility; `SuggestedResultType` uses strict assignability.** Deliberately
   different rules for different questions. `Satisfies` answers *may an author wire this up*, so it calls the
   same `IsConvertibleTo(type, false)` that `ValueInput.CanConnectToValid` uses — anything narrower would hide
   Functions that demonstrably work. `SuggestedResultType` answers *what should a Function created right now
   declare*, where a permitted downcast is a coin flip taken on the author's behalf. **A test caught this**:
   `Requiring(Component, Transform)` first suggested `Component`, which fills the `Transform` port only when
   the value happens to be one.

3. **A Function with no `Result` is refused everywhere, including at an unconnected node.** The spec's table
   says an unconstrained node offers "everything". This is the one exception: the node exists to read a value,
   and a Function with no result has none to give at any wiring.

4. **Two Functions sharing a name are qualified by folder, and only then.** The project already has two
   `IsLowHealth.asset` (*verified* — `BH3Demos/FpsTeams/Trees` and `BH3Demos/FunctionPorts`), which rendered as
   two identical rows. Qualifying every row would lengthen the common case to solve a problem it does not
   have. Disambiguation runs over the rows actually shown, not the project.

5. **Sorting falls back to the label when names tie.** `List.Sort` is not stable, so two same-named entries
   could swap places between openings of the same dropdown.

6. **"Create new Function…" declares the `Result` the port needs.** `DefaultGraph()` declares Enter and Exit
   and no result, so a Function created without this would fail the very filter that offered to create it.

7. **No new embedded graphs, per the tool owner (2026-08-21).** The deleted drawer minted a sub-asset
   whenever a node had nothing assigned. A node that already holds one keeps *Open Graph* and gains *Extract
   to Function*. This closes open question 3 and is the first half of step 7; locked decision 4 is amended.

8. **A save-file dialog, not a naming convention.** A convention like `<TreeFolder>/Functions/<Tree>.<Node>.asset`
   only pays off if `bt_add_variable_read` and step 7's migration share it; inventing it in the one surface
   where the author is present to say where is the wrong place to start.

### The defect the self-review caught, and why it mattered

**`UndoUtility.RecordEditedObject` does nothing when called from a dropdown callback.** It resolves the
object to record from `LudiqEditorUtility.editedObject`, an override stack populated only inside
`GraphContext.BeginEdit()/EndEdit()` — which brackets canvas and inspector draws frame by frame. Unity's
`AdvancedDropdown` raises `ItemSelected` from its own popup window, outside that bracket.

Verified against the live editor rather than argued: from outside the bracket `editedObject.value` is
`null`, and `RecordEditedObject` leaves a tree's dirty flag `false`.

The failure was the quiet kind. `SetFunction` still mutates the in-memory node, so the assignment looked
applied and survived the session — but the asset was never dirtied, so it had **no undo entry, never showed
as unsaved, and would be gone after a domain reload** unless some unrelated edit happened to dirty the same
tree. Assigning a Function is the one thing this feature exists to do.

Fixed by naming the asset explicitly: `RecordTreeEdit` resolves the tree via
`BehaviorTreeCanvas.GetBehaviorTreeGraphAsset()` and calls `Undo.RegisterCompleteObjectUndo`, with
`MarkDirty` after the mutation. Both verified live.

**This generalises.** Any BH3 editor code that mutates from a callback raised outside the canvas's own
event loop has the same hole. `Widget.OnContext` avoids it by deferring through `canvas.delayCall`, which is
why `VisualScriptGraphVariableWidget`'s "Refresh Function Ports" works. Anything reached from a Unity popup,
an `EditorApplication.delayCall`, or an async import does not get that for free.

### Two findings reported rather than fixed

1. **The repository accumulates duplicate null entries and never dedupes.** Rebuilding the demo content a
   few times added **64 entries to `ScriptGraphAssetsRepository.asset`, 51 of them duplicates of 13 keys, all
   `{fileID: 0}`** — the residue of sub-assets the sweep destroyed, left in the ledger. `RemoveInvalid()`
   then removed **109** entries, 45 of which were already committed, so the ledger has been carrying dead
   weight for some time. The working copy was reverted so this change does not touch a generated file
   unrelated to it. **Principle: make bad states unrepresentable** — a ledger that can hold a key twice with a
   null value is a data structure permitting a state nothing wants. **Cost:** the file grows without bound,
   every tree edit dirties it, and it is a version-control chokepoint for a second contributor. **This is
   evidence for steps 5 and 6, not a separate task** — structural containment cannot express the state at all.

2. **`BaseVisualScriptingNode.CanCopy/CanCut/CanDuplicate` are all `false`,** with a comment saying copies
   would share a script graph asset that a deletion could take away. That reasoning applies only to *embedded*
   graphs; a node referencing a Function has no such problem. **Principle: one reason to change** — the
   restriction is applied to the node type rather than to what it holds. **Cost:** designers cannot duplicate a
   Function-backed node today, which is a routine operation, and the workaround is rebuilding it by hand.
   **Fix:** make the three properties conditional on holding an embedded graph, or delete them with step 7.
   Left alone here because it changes canvas behaviour for existing trees and belongs with the migration.

### Known gaps, stated rather than discovered

- **The dropdown itself has no automated test.** The filter does — 16 tests over
  `FunctionPortConstraint` and `FunctionPickerCatalog`, which is where every decision lives. What is untested
  is the IMGUI on top: `GetHeight`/`OnGUI` agreement, the button row, and that `ItemSelected` reaches
  `Assign`. Verified by hand and by the demo window instead. **The undo defect above lived exactly in that
  untested gap**, so this is the real risk area of the change, not a formality.
- **A mismatched assignment is shown in error but not auto-repaired.** Deliberate (see the settled open
  question), but there is no one-click "pick a valid one" from the error box.
- **`Extract to Function` leaves the original sub-asset**, as `fn_extract` does, and `bt_verify` reports it
  as an orphan. Correct under locked decision 6, and it will look like a leak until step 5 lands.
- **The demo is an editor window, not a Play-mode scene.** 2c changes what a dropdown lists; there is no
  runtime behaviour to press Play on. The window calls `FunctionPortConstraint` and `FunctionPickerCatalog`
  directly — the same two calls the dropdown makes — so it cannot flatter the filter.

### Tests

16 new, all passing. EditMode went 648 → 664 with no new failures.

Pre-existing failures, unchanged and unrelated (both in TacticalPositionSelection):
`TpsArchitectureTests.All_concrete_PositionEvaluators_are_marked_Serializable` (EditMode), and three
`TacticalPositionSelectionPlayModeTests` failing on an NRE in `GameEntity.Awake`.

### Verified against the live editor, not assumed

- `InspectorProvider` now resolves `BTScriptGraphVariable` to `BTScriptGraphVariableInspector`, where it
  previously resolved to `CustomPropertyDrawerInspector`. The bridge is gone, which is the load-bearing claim
  of the 2026-08-16 spike.
- The filter was run over **all 49** Script Graph Variable nodes in the project: boolean ports offer the four
  predicates; `Vector3`, `Transform`, `GameObject` and query ports offer nothing (none exist yet); `object`
  ports offer everything.
- The undo defect and its fix, both directions.
