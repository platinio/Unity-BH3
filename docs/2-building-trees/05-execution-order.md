# Execution order

Which child a composite tries first, how that order is stored, and how to change it.

A `Selector` tries its children in order and stops at the first that succeeds. A `Sequence` runs them in
order and stops at the first that fails. For both, **which child comes first is a real decision**: it is the
priority of that branch.

---

## The rule

**The order is stored on the connection, not read from the canvas.** Each parent-to-child connection carries
an index: 0 is the branch tried first, 1 is next, and so on.

Every child of a composite shows a small numbered badge in its corner, counting from **1**. That badge is the
priority the runtime will use.

Dragging a child left or right past a sibling still reorders it: when you release the mouse, the editor
rewrites the stored indices from the new left-to-right layout. So laying branches out in the order you want
them tried keeps working. The position is the *gesture*; the index is the *truth*.

Connecting and deleting keep the numbers honest too. A child you connect takes the number of the place it
sits in, and when you delete a child the ones after it close up. You never have to nudge a node to make the
badges catch up.

## Why the canvas is not the truth

If pixel coordinates decided priority, three things would go wrong, and all three used to:

- **Tidying the canvas changed behaviour.** Nudging a node to stop two lines crossing could silently swap
  which branch an agent tried first, with nothing on screen saying so.
- **Generated trees were fragile.** Code building a tree had to get its X coordinates right or it produced a
  wrong tree that looked right.
- **Nothing showed the effective priority.** A mis-ordering was invisible until playtest.

Storing the index fixes all three, and the badge means you never infer the order by eye.

---

## Reordering branches

**On the canvas.** Drag a child past its sibling and release. The badges update immediately.

**From code** (see [Authoring from code](../4-extending-with-csharp/04-authoring-from-code.md)). `Connect`
takes the priority directly:

```csharp
// Attack is tried first, Chase second, Idle last.
BehaviorTreeAuthoring.Connect(asset, selector, attack, 0);
BehaviorTreeAuthoring.Connect(asset, selector, chase,  1);
BehaviorTreeAuthoring.Connect(asset, selector, idle,   2);
```

Leave the index off and each child takes the next free slot, so calling `Connect` in the order you want the
branches tried gives the right tree. Lay generated nodes out left to right anyway: it no longer affects
execution, but a tree whose picture disagrees with its priorities is hard for a person to review.

---

## Connecting and deleting

**Connecting a child** puts it where it sits. Count the siblings to its left: connect a node to the left of
all of them and it becomes 1, between the second and third and it becomes 3, to the right of all of them and
it goes last. The siblings keep the order they already ran in and shift along to make room.

**Deleting a child**, or just its connection, closes the gap: the children after it move up one, and the
order of everything that is left does not change.

Neither gesture reorders the other children. That matters for a composite whose badges are amber on purpose:
connecting a new child there slots it in by where it sits and leaves the deliberate order of the rest alone.

A drag is different. Releasing a drag, of any node, renumbers every composite in the tree from its layout, so
it is the one gesture that turns an amber composite grey.

---

## When a badge turns amber

An **amber** badge instead of a grey one means that composite's children read left-to-right in a different
order than they run. The tree runs correctly; the badge is saying the *canvas* is misleading. It usually
means the tree was generated with indices that do not match its layout, or was edited outside the editor.

`bt_verify` reports the same thing, so it shows up in CI:

```
Zombie: layout ≠ priority — 'Selector' runs 'Attack' before 'Chase', but lays them out the other way round.
```

The fix is cosmetic: drag the children into an order that reads correctly, which rewrites the indices to match
what you see.

---

## Random Selector and Random Sequence

These two deliberately have no fixed priority: they shuffle their children on every pass. The badges still
show the authored order, but the runtime ignores it. That is the point of the node.

---

## Trees saved before indices existed

They keep running exactly as they did: a composite with no recorded order falls back to canvas position
until the first time you drag a child, or connect or delete one of its own children. Each of those writes
down the order the composite was already running in. See
[Migrating older trees](../5-reference/02-migrating-older-trees.md#child-order-came-from-canvas-position).

---

## Where the rule lives

For contributors; skip this on a first read. `ChildrenInPriorityOrder` is the single rule; the runtime, the
Why panel and the tree dump all call it, so a debugging view can never report a different priority than the
one that ran. The sort inside it is GraphCore's `SortIntoChildOrder`, the same rule the canvas sorts with
before it renumbers. The canvas counts every connection a composite has; the runtime leaves out one that ends
on a node that cannot be a child, so the two can still differ on an old asset that holds such a connection.

| Piece | File |
|---|---|
| The ordering rule | `Runtime/Graphs/BehaviorTreeGraph.cs` — `ChildrenInPriorityOrder`; GraphCore `Runtime/Graphs/BaseGraph.cs` — `SortIntoChildOrder` |
| The stored index | GraphCore `Runtime/Nodes/Transitions/BaseGraphTransition.cs` — `TransitionIndex` |
| Drag rewrites the indices | GraphCore `Editor/Canvases/BaseCanvas.cs` — `OnDragEnded` |
| Connect slots the new child in | GraphCore `Editor/Canvases/BaseCanvas.cs` — `EndTransition` |
| Delete closes the gap | GraphCore `Editor/Canvases/BaseCanvas.cs` — `CloseIndexGapsLeftBy`, called for a deleted connection by `OnElementsDeleted` and for a deleted node by `BehaviorTreeCanvas.RemoveDanglingElements` |
| The badge | `Editor/Debugging/BehaviorTreePriorityBadge.cs` |
| The verify warning | `Editor/Authoring/BehaviorTreeVerification.cs` |

---

## Next

- [Guards](06-guards.md) — how a higher-priority branch takes the slot while a lower one is running
- [Sub-trees](07-sub-trees.md)
