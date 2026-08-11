# Explicit priority — position is the gesture, index is the truth, badge is the feedback

**Status:** design spec for implementation by an AI or engineer with access to the BH3 source.
Written from BH3's design documentation — verify exact type/member names against the code before building.

## Current behavior (the problem)

At `OnAwake` the runtime calls `SortContainerNodesChildren()`, which orders every container's children by
canvas `Position.x`. Left-to-right **reading** of priority is correct BT convention (Unreal reads the same
way) — the problem is that pixel coordinates are the **serialized truth**:

- Tidying the canvas (moving a node for readability, avoiding crossed lines) can silently change runtime
  behavior.
- Programmatic authoring must get X coordinates right or produce a wrong tree that *looks* right
  (`bt_connect` already takes an index argument, but X wins at runtime).
- Nothing on the canvas shows effective priority, so a mis-ordering is invisible until playtest.

**This is not a proposal to remove the canvas.** The canvas stays the editing surface and left-to-right
stays the mental model. Three changes:

## 1. Index is the truth (runtime)

- Sibling order is serialized explicitly — transitions already carry an index from
  `SetupTransition(parent, child, index)`; make that index authoritative and contiguous per parent.
- Runtime consumes the stored index. Delete the sort-by-X in `OnAwake`
  (`SortContainerNodesChildren` becomes a no-op or is removed).
- Programmatic authoring becomes correct by construction: `bt_connect --index 0` means priority 0,
  regardless of where layout puts the node.

## 2. Position is the gesture (editor)

- Dragging a child horizontally past a sibling **updates the stored indices** on drag-end — the designer
  keeps the exact interaction they have today, but the gesture now *writes the truth* instead of *being*
  the truth.
- Optional affordance: the container's inspector shows its children as a reorderable list (drag handles);
  reordering there nudges canvas X to match. Same truth, two editing surfaces.

## 3. Badge is the feedback (editor)

- Every child of a container renders a small priority badge (its index, 1-based) — Unreal-style execution
  order numbers. The designer always sees effective priority without simulating the sort in their head.
- If canvas X order disagrees with stored order (possible after external edits, merges, or generated
  trees), the badge still shows the truth, the affected nodes get a warning chip
  ("layout ≠ priority"), and a one-click **Re-layout** action re-spaces the children to match the indices.
  The disagreement is never silently resolved in either direction.

## Migration

- On first load of a pre-change asset: adopt current X order as the initial stored indices (behavior
  preserved exactly), mark the asset dirty.
- `bt_verify` gains a lint: "canvas order disagrees with stored priority" (warning, not error — the badge
  and chip make it visible; verify makes it CI-visible).
- Update the authoring helpers/docs: `AddNode` X placement no longer carries semantics; `Connect`'s index
  is the semantic input. Keep laying out left-to-right anyway for readability.

## Acceptance criteria

1. Runtime child order matches stored indices with sort-by-X removed; a tree whose layout contradicts its
   indices runs by indices.
2. Dragging a child across a sibling on canvas swaps badges immediately and persists after save/reload.
3. Reordering via the inspector list updates both indices and (after Re-layout or nudge) canvas
   positions.
4. A generated tree built with out-of-order X but correct `bt_connect` indices runs correctly and shows
   the warning chip in the editor.
5. Old assets load with identical runtime behavior to pre-change (X order adopted as indices).

---

# Implemented — 2026-08-11

**Status: partial — part 1 and part 3 done, part 2 done in the form that matters, deferred pieces listed
below.** Branch `feature/explicit-priority`, spanning **two submodules**: `Unity-BH3` and
`graph-core-library`. Built after spec 06 on the same branch — both rewrite `BehaviorTreeGraph.OnAwake`.

Scope was agreed with the tool owner as "runtime + priority badges" rather than the full spec.

## The spec's central assumption was wrong, and this is the most important thing to carry forward

> *"transitions already carry an index from `SetupTransition(parent, child, index)`; make that index
> authoritative"*

The field exists and is serialized, but **the editor was writing garbage into it.**
`BaseCanvas.CalculateTransitionIndex` counted transitions running between the same *pair* of nodes. Any
ordinary graph connects a pair at most once, so the answer was always **0** — every transition the canvas has
ever created stored index 0. Only `BehaviorTreeAuthoring.Connect` wrote a real value, and it defaulted to 0.

Making the index authoritative as literally specified would therefore have pinned every child of every
editor-authored tree to priority 0 — each `Selector` taking its first branch forever. This is why the
fallback below exists and is not optional.

## What was built

### GraphCore (`graph-core-library`)

- `Runtime/Nodes/Transitions/BaseGraphTransition.cs` — new `SetTransitionIndex(int)`. Separate from
  `SetupTransition` so reordering cannot accidentally repoint an edge.
- `Editor/Canvases/VisualScriptingCanvas.cs` — new `protected virtual OnDragEnded()` hook, called once a drag
  commits. A hook rather than a virtual `EndDrag` so the drag bookkeeping stays in one place.
- `Editor/Canvases/BaseCanvas.cs` — `CalculateTransitionIndex` fixed to return the next free sibling ordinal;
  new `ReindexChildrenFromPosition()` invoked from `OnDragEnded`, rewriting each affected parent's indices
  from left-to-right position.
- `README.md` — **new**; the module had no documentation at all.

### BH3 (`Unity-BH3`)

- `Runtime/Graphs/BehaviorTreeGraph.cs` — `ChildTransitionsInPriorityOrder` / `ChildrenInPriorityOrder`
  (public, the single ordering rule) and `RecordsAPriorityOrder` (the contiguity test).
  `ConvertTransitionNodesIntoTaskNodeChild` now builds children in priority order.
- `Runtime/Nodes/ContainerNodes/ContainerNode.cs` — `SortChildren()` is now a no-op by default.
- `Runtime/Debugging/BehaviorTreeDump.cs`, `Runtime/Debugging/Why/BehaviorTreeGraphTopology.cs` — both now
  call `ChildrenInPriorityOrder` instead of sorting by X themselves.
- `Editor/Debugging/BehaviorTreePriorityBadge.cs` — **new**; the 1-based badge, amber when layout disagrees.
- `Editor/Authoring/BehaviorTreeVerification.cs` — new `layout ≠ priority` lint.
- `Editor/Authoring/BehaviorTreeAuthoring.cs` — `Connect`'s `index` now defaults to `-1` meaning "next free
  slot" instead of `0`.
- `Test/EditMode/ExplicitPriorityTests.cs` — **new**, 7 tests.
- `docs/execution-order.md` — **new**, registered in `docs/SUMMARY.md` and the README table.

## Decisions made

- **Contiguity, not presence, decides whether indices are trusted.** A container's indices are used only when
  they form exactly `0..n-1`, each once; otherwise child order falls back to canvas X. This is what makes
  acceptance criterion 5 hold *without* requiring a migration pass to have run — which matters because a
  player build loads assets the editor never touched. A gap means something was deleted without renumbering
  and a duplicate means two branches claim one priority; neither is an order worth trusting.
- **No separate migration step, a deliberate departure from the spec.** The spec asked for a first-load
  migration that adopts X order and marks the asset dirty. The fallback makes that unnecessary for
  correctness, and a migration rewriting every tree asset on load produces a large, noisy diff the first time
  anyone opens the project. Assets self-correct the first time a child is dragged. Trade-off:
  `RecordsAPriorityOrder` runs per container per awake rather than once ever — O(children) over a list of two
  to five, at awake only.
- **`ChildrenInPriorityOrder` is public and shared by the runtime, the dump and the why-panel topology.**
  Priority is one decision; a debugging view that ordered children differently would confidently report the
  wrong branch as higher priority, which is worse than not reporting one. Previously all three sorted by X
  independently — the duplication was invisible only because all three happened to agree.
- **`ContainerNode.SortChildren` kept as a no-op hook rather than deleted.** `RandomSelector` and
  `RandomSequence` override it to shuffle, and the awake-time call is their *only* initial shuffle. Deleting
  it outright — which the spec's wording invites — would have left them running in authored order on their
  first pass. The spec does not mention this; `RandomCompositesAreStillShuffledAtAwake` pins it.
- **Drag-end reindexing was treated as in scope** even though drag-to-reorder is nominally part 2. Without
  it, removing X-order authority would leave designers with *no way to reorder children at all* — the feature
  would ship as a regression. It is ~25 lines in `BaseCanvas`.

## Refactors done on the way

- `BehaviorTreeDump.WriteChildren` and `BehaviorTreeGraphTopology.Read` both had their own X-sorting; both now
  defer to the graph. Not a cleanup for its own sake — leaving either would have made the tooling disagree
  with the runtime the moment indices started mattering.
- Removed a now-unused `System.Linq` import from `ContainerNode.cs`.

## Not built (deferred, agreed)

- **Inspector reorderable list** for a container's children (part 2's optional affordance).
- **One-click Re-layout action** to re-space children to match indices. The badge and the `bt_verify` lint
  report the disagreement; fixing it is currently a manual drag.
- **Migration marking assets dirty on load** — superseded by the fallback, above.

## Known gaps / pre-existing failures

- **No PlayMode baseline** — the runner wedged before this work began and PlayMode is *unknown*, not green,
  on both sides of this change. Everything in this spec is EditMode-testable and is EditMode-tested.
- `TpsArchitectureTests.All_concrete_PositionEvaluators_are_marked_Serializable` fails at baseline and still
  fails. TacticalPositionSelection module, unrelated.
- `bt_verify` on the sample trees reports unset ports on `Zombie`/`Soldier` and an orphan node on `Draugr`.
  These pre-date this work and are unrelated to ordering. **No `layout ≠ priority` finding on any existing
  asset**, which is the fallback behaving as intended.
- EditMode suite: 331 → 344 tests (6 from spec 06, 7 from this spec), all passing but the one above.
