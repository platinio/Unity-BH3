# Execution Order

A `Selector` tries its children in order and stops at the first that succeeds. A `Sequence` runs them in
order and stops at the first that fails. So for both, **which child comes first is a real decision** — it is
the priority of that branch.

This page is about how that order is decided, how to change it, and what to do when the canvas and the
runtime seem to disagree.

---

## The short version

**The order is stored on the connection, not read from the canvas.** Each parent-to-child connection carries
an index: 0 is the branch tried first, 1 is next, and so on.

Dragging a child left or right still reorders it — the drag rewrites the stored indices when you release the
mouse. So the way you have always worked keeps working. The difference is that the position is now the
*gesture*, and the index is the *truth*.

Every child of a composite shows a small numbered badge in its top-right corner, counting from **1**. That
badge is the priority the runtime will actually use.

---

## Why it is not simply left-to-right

Reading priority left to right is the right mental model and BH3 keeps it. The problem was making pixel
coordinates the stored truth:

- **Tidying the canvas changed behavior.** Nudging a node to stop two connection lines crossing could silently
  swap which branch an agent tried first. Nothing on screen said anything had changed.
- **Generated trees were fragile.** A tree built from code had to get its X coordinates right or it produced a
  wrong tree that *looked* right. The index argument was there but the runtime ignored it.
- **Nothing showed effective priority.** A mis-ordering was invisible until playtest.

Storing the index fixes all three, and the badge means you never have to infer the order by eye.

---

## Reordering branches

**On the canvas.** Drag a child past its sibling and release. The indices are rewritten from the new
left-to-right order, and the badges update immediately. This is the normal way to do it.

**From code.** `Connect` takes the priority directly:

```csharp
// Attack is tried first, Chase second, Idle last.
BehaviorTreeAuthoring.Connect(asset, selector, attack, 0);
BehaviorTreeAuthoring.Connect(asset, selector, chase,  1);
BehaviorTreeAuthoring.Connect(asset, selector, idle,   2);
```

Leave the index off and each child takes the next free slot, so calling `Connect` in the order you want the
branches tried gives you the right tree:

```csharp
BehaviorTreeAuthoring.Connect(asset, selector, attack);   // 0
BehaviorTreeAuthoring.Connect(asset, selector, chase);    // 1
BehaviorTreeAuthoring.Connect(asset, selector, idle);     // 2
```

Lay generated nodes out left to right anyway. It no longer affects execution, but a tree whose picture
disagrees with its priorities is hard for a human to review — see the warning below.

---

## "Layout ≠ priority"

If a child's badge is **amber** instead of grey, that container's children read left-to-right in a different
order than they run.

This is legal and the tree runs correctly — the badge is telling you the *canvas* is misleading, not that the
tree is broken. It usually means the tree was generated with indices that don't match its layout, or was
edited outside the graph editor.

`bt_verify` reports the same thing as a warning, so it shows up in CI as well:

```
Draugr: layout ≠ priority — 'Selector' runs 'Attack' before 'Chase', but lays them out the other way round.
```

The fix is cosmetic: drag the children into an order that reads correctly, which rewrites the indices to
match what you see.

---

## Random Selector and Random Sequence

These two deliberately have no fixed priority — they shuffle their children each time. The badges still show
the authored order, but the runtime order is randomized on every pass. That is the whole point of the node,
and it is unaffected by everything above.

---

## Trees authored before this change

They keep working, unchanged.

Older trees stored index 0 on every connection, because the editor computed the index by counting connections
between the same pair of nodes — which is always zero, since a pair is only ever connected once. A set of
all-zero indices is not an ordering, so BH3 treats it as "no order recorded" and falls back to canvas
position, exactly as it always behaved.

The same fallback applies whenever the stored indices are not a clean `0, 1, 2, …` run — a gap means a
connection was removed without renumbering, and a duplicate means two branches claim the same priority.
Neither is trustworthy, so position answers instead.

The first time you drag a child in such a tree, the indices are rewritten properly and the tree stops relying
on the fallback.

---

## Where the code lives

| Piece | File |
|---|---|
| The ordering rule both runtime and tooling read | `Runtime/Graphs/BehaviorTreeGraph.cs` — `ChildrenInPriorityOrder` |
| Where children are built from connections | `Runtime/Graphs/BehaviorTreeGraph.cs` — `ConvertTransitionNodesIntoTaskNodeChild` |
| The stored index | `GraphCore/Runtime/Nodes/Transitions/BaseGraphTransition.cs` — `TransitionIndex` |
| Drag rewrites the indices | `GraphCore/Editor/Canvases/BaseCanvas.cs` — `OnDragEnded` |
| The badge | `Editor/Debugging/BehaviorTreePriorityBadge.cs` |
| The verify warning | `Editor/Authoring/BehaviorTreeVerification.cs` |

`ChildrenInPriorityOrder` is the single rule. The runtime, the [Why Panel](why-panel.md) and the tree dump
all call it, so a debugging view can never report a different priority than the one that actually ran.
