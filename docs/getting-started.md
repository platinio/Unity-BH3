# Getting Started

Your first working behavior tree, and the two mistakes that catch everyone on day one.

---

## Requirements

BH3 is built on Unity's **Visual Scripting** package — it must be installed in the project. For installing
BH3 itself, see the **Installation Guide.pdf** in the repository root.

---

## 1. Create a tree asset

Right-click in the Project window → **Create → Visual Scripting → Behavior Tree**.

This creates a `.asset` file holding your tree definition.

## 2. Open the graph editor

Double-click the asset. An **Entry** node is created automatically — it is the root of the tree and cannot
be deleted.

You can also open the editor with the **Edit Graph** button, either on the asset itself or on a
`BehaviorTreeMachine` component. See [The Graph Editor](editor-guide.md) for the full tour.

## 3. Build the tree

Right-click the canvas to add nodes. To connect a parent to a child, **hold `Ctrl` and drag** from the
parent to the child, then release.

A minimal tree that does something visible:

![A behavior tree: Entry connects to a Repeater, which connects to a Sequence, whose two children are a Wait node with a Time port and an Add Force node with Target and Force ports](images/entry-repeater-sequence-example.png)

`Entry → Repeater → Sequence → (Wait, Add Force)`. The Sequence runs its children left to right; the
Repeater sends it around again forever.

## 4. Add the machine

Add a **BehaviorTreeMachine** component to a GameObject and drag your tree asset into its **Graph** field.

![The Behavior Tree Machine component in the Inspector, showing Source set to Graph, a Graph field holding a Behavior Tree Graph Asset, and an Edit Graph button](images/behavior-tree-machine-component.png)

> `BehaviorTreeMachine` requires a `Variables` component. Unity adds it automatically if it's missing.

## 5. Press Play

The machine loads the graph on `Awake`, starts execution on `Start`, and ticks it every frame in `Update`.
No additional code is required.

---

## The two things that catch everyone

### Put a `Repeater` under `Entry`

The machine stops ticking once the root returns `Success` or `Failure`. A tree without a `Repeater` near the
top therefore runs **exactly once** and then goes silent — which looks identical to "my tree is broken".

If your tree does something once and then nothing ever happens again, this is almost always why.

### Child order is left-to-right *on the canvas*

Execution order is decided by each node's **horizontal position**, not by the order you connected them. When
the tree wakes up, every container sorts its children by canvas X.

So for a Selector, **the leftmost child is the highest priority**. If you drag a node sideways you have
changed its priority, even though the connecting lines look unchanged. Lay branches out left-to-right in the
order you want them tried.

---

## Where to go next

- [The Graph Editor](editor-guide.md) — the window, and how to drive it
- [Node Reference](node-reference.md) — what ships in the box
- [Ports and Wiring](ports-and-wiring.md) — the idea BH3 is built around
- [Best Practices](best-practices.md) — how to keep trees reusable once they grow
