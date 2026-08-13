# Behavior Library panel — the shop for behaviors

**Status:** design spec for implementation by an AI or engineer with access to the BH3 source.
Written from BH3's design documentation — verify exact type/member names against the code before building.

## Goal

A designer opens one dockable panel, browses/searches every reusable behavior in the project as **cards**,
reads what each needs, and drags one into a tree or onto a prefab. The library is how "pick a few
behaviors → new AI" starts feeling like a product instead of a folder of assets.

## BH3 context you need

- Reusable behaviors are `BehaviorTreeGraphAsset`s run via `RunBehaviorTreeGraphNode`. A branch declares
  Required/Optional variables; those become input ports on the calling node. Ports are created from a
  **copy** of the contract on the caller — after assigning the asset you must call
  `RefreshParameters()` (CLI: `bt_refresh_sub_tree_ports`) or the node has no ports.
  `DescribeContractDrift()` reports stale copies.
- `bt_verify` reloads a tree and reports unset ports, orphans, unassigned sub-trees, recursion.
- `BehaviorTreeDump.ToJson` emits contracts, sub-tree references, and guids — the raw material for the
  derived card data below.

## Data model: the BehaviorCard

Sidecar metadata per library asset (embedded serialized block on the asset, or a sidecar ScriptableObject
— prefer whichever survives BH3's asset pipeline cleanly; sidecar avoids touching the runtime type).

**Authored fields** (hand-written once):
- Display name, one-line description, long description (markdown), category
  (Combat / Movement / Perception / Utility / …), tags, author, version, changelog notes.
- Thumbnail: auto-captured canvas render by default; optional custom image.
- **Suggested guards** — the preconditions this branch is usually gated on. See the section below.

**Derived fields** (regenerated on save / on demand — never hand-editable, so they cannot lie):
- Parameter contract: each Required/Optional variable with name, type, default (from the branch's own
  declarations).
- Requirements manifest (from spec 04): components, animator parameters, facts, subsystems this branch
  needs.
- Sub-tree dependencies (which library assets this one calls), and the reverse index: "used by N trees."
- Verification status: last `bt_verify` result (clean / N issues).

## Suggested guards — a branch brings its own preconditions

A branch asset may declare **suggested guards**. Dropping the card instantiates them at the call site,
where the designer can retune or delete them. This is what makes an interrupt library
(Flinch / Stagger / Dodge / Flee, stacked by priority) work as drag-and-drop instead of "drop, then hand-wire
the same guard for the fiftieth time". Decided in spec 09; the mechanics are here because the library is
what consumes them.

**A default, never a contract.** If a suggested guard were binding, a branch would know something about its
caller and reuse would be gone. Two things keep that true:

- The **runtime never reads a suggested guard.** It exists only at drop time.
- It therefore lives on the **card sidecar, not on `BehaviorTreeGraphAsset`.** Keeping the field off the
  runtime type is what stops someone later making `RunBehaviorTreeGraphNode` consult it. The cost is that a
  branch dragged from the Project window, or wired up in code, gets no suggestion — correct rather than
  unfortunate, since this is a library affordance and not a property of the behaviour.

### What is stored

```
SuggestedGuard
  kind        : Reactive | Conditional
  condition   : VariableRead { key, expected, fallback }   // the common case, no asset needed
              | Graph        { ScriptGraphAsset }          // arbitrary, referenced not copied
  abortsOwner : bool
  preempts    : bool
  triggers    : List<GuardTrigger>
  note        : string      // why this guard; shown on the card
```

A list, because guards on one owner AND together.

The drop is the authoring API that already exists: `AddNode<BooleanReactiveGuard>` + `UpdateOwner` +
`SetCapabilities` + `AddTrigger`, with the condition half being either
`BehaviorTreeAuthoring.GuardOnVariable` (VariableRead) or a `VisualScriptGraphVariable` whose
`SetScriptGraph` points at the shared asset and whose `Output` connects to the guard's `Value` (Graph).

### The Graph condition must be a standalone asset

BH3's guard graphs are normally **embedded**: `CreateVariableReadGraph` produces a `ScriptGraphAsset`
registered in `ScriptGraphAssetsRepository` against the owning tree, and
`BehaviorTreeGraph.DestroyUnusedScriptGraphAssets` deletes any registered graph no element still references.
Two trees pointing at one tree's sub-asset means the owner's cleanup can delete it out from under the other.

That cleanup only ever considers assets the repository holds **for that tree**, so a standalone
`ScriptGraphAsset` living in the project is never a candidate and is safe to reference from anywhere. Hence:

- A suggested guard's Graph condition **must** reference a standalone asset.
- The library needs an **"extract condition to project asset"** action, because a designer who authored the
  condition inline has an embedded sub-asset that cannot be suggested safely. Small, and useful on its own.

This is the better shape anyway: a condition worth suggesting across many call sites is a condition worth
being a named, shared, reusable asset. The library ships behaviours; this ships the predicates they are
gated on.

### Consequences to design for

- **A shared condition edits everywhere at once.** Fixing `hasTarget` in one place is the upside; changing it
  under fifty agents by surprise is the hazard. The card needs a **"used by N guards"** reverse index beside
  its "used by N trees".
- **The requirements manifest must union the condition's variable reads**, so dropping onto an agent that
  never publishes a watched fact is caught by the same fix-it checklist as a missing Animator parameter.
- **Derived keys become load-bearing.** A shared graph's watched keys have to be derivable from the graph, or
  one mistake propagates silently to every drop rather than to one tree — a guard watching a key its graph
  does not read never wakes, with no error. Tracked as Unity-BH3#13.
- **No drift tracking, deliberately.** Parameters need `RefreshParameters` / `DescribeContractDrift` because
  the branch *reads* those values at runtime. A guard is evaluated by the caller, on the caller's node, and
  the branch never sees it — so once instantiated there is no copy to go stale. Re-dropping the same branch
  produces a second, independent call site with its own guards.

### To prove before building

That a standalone `ScriptGraphAsset` evaluates correctly through `ScriptGraphVariable.GetValue<T>(Variables)`
with the agent's variables flowing in, exactly as an embedded one does. It should — same type, same call —
but the whole design rests on it and it is a ten-minute spike.

## Library membership

- A project setting lists **library folders**. Only assets under them appear in the panel — keeps WIP and
  scratch trees out. Moving an asset into a library folder is publishing it.
- Assets in library folders missing authored metadata appear in an "uncataloged" section as a nudge.

## Panel UX

- Dockable EditorWindow: search box + category chips + tag filter; card grid (thumbnail, name, one-line
  description) with a compact list toggle.
- Card badges: parameter count, requirement icons (nav / animator / perception / subsystem), verification
  dot (green = clean, red = issues, gray = never verified), drift warning if any caller reports contract
  drift.
- Selecting a card opens a detail pane:
  - Full parameter table — Required rows visually distinct (they must be fed at every call site).
  - Requirements with human explanations ("needs Animator trigger 'Attack'").
  - Dependency tree (this branch calls X, X calls Y) and "used by" list — both clickable.
  - Long description + changelog.

## Actions (this is where it must feel professional)

- **Drag card → tree canvas:** creates a `RunBehaviorTreeGraphNode`, assigns the asset, calls
  `RefreshParameters()` immediately (forgetting this is today's #1 papercut), and selects the node with
  its Required ports highlighted as unfilled. If the card declares **suggested guards**, they are
  instantiated on the run node at the same moment and selected alongside it, so the designer sees what the
  branch expects to be gated on rather than discovering it later.
- **Drag card → scene GameObject / prefab:** if no `BehaviorTreeMachine`, offer to add one plus a
  generated root tree containing just this branch; then run requirements validation (spec 04) against the
  target and show the fix-it checklist.
- Double-click → open the branch for editing. Right-click → find usages / verify now / copy contract as
  markdown (for docs and for pasting into AI prompts).

## Governance & health

- A library health report (menu + CLI): assets failing verification, assets with drifted callers, assets
  missing metadata, orphaned assets (in library folders, used by nothing).
- Contract changes: when a branch's Required/Optional set changes, the panel badges every caller with
  drift (data already available via `DescribeContractDrift`) and offers "refresh all callers."

## CLI mirror (keep the "ask the project" philosophy)

- `bt_library_list [--category X] [--tag Y]` — cards as JSON.
- `bt_library_describe --asset <path>` — full card: contract, requirements, dependencies, used-by.
These make the same library shoppable by authoring agents, which is how AI-assisted "compose an AI from
the library" stays consistent with what designers see.

## Acceptance criteria

1. A branch asset dropped into a library folder appears in the panel within one refresh, with derived
   contract data correct against `bt_describe_tree` output.
2. Dragging a card onto a canvas produces a run node whose ports exist without any manual refresh step.
3. Changing a branch's Required variables flags every caller in the panel within one refresh.
4. Search over name + tags + description returns in < 100 ms on a project with 500 library assets.
5. Derived fields cannot be edited by hand anywhere in the UI.
6. Dropping a card that declares a suggested guard produces the guard node, owned by the new run node, with
   no manual wiring — and deleting it afterwards leaves a tree that verifies clean.
7. A suggested guard whose condition is an embedded script graph is refused at authoring time, with the
   "extract to project asset" action offered.
