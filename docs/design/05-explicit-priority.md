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
