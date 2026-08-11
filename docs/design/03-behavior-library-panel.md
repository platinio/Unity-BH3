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

**Derived fields** (regenerated on save / on demand — never hand-editable, so they cannot lie):
- Parameter contract: each Required/Optional variable with name, type, default (from the branch's own
  declarations).
- Requirements manifest (from spec 04): components, animator parameters, facts, subsystems this branch
  needs.
- Sub-tree dependencies (which library assets this one calls), and the reverse index: "used by N trees."
- Verification status: last `bt_verify` result (clean / N issues).

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
  its Required ports highlighted as unfilled.
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
