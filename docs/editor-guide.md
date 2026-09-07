# The Graph Editor

The window where you create, edit and connect nodes.

---

## Opening it

Use the **Edit Graph** button, on either the Behavior Tree Graph asset or the `BehaviorTreeMachine`
component.

![The Inspector showing a Behavior Tree Graph Asset with its Edit Graph button highlighted](images/open-graph-from-asset.png)

![The Behavior Tree Machine component in the Inspector, with Source, Graph and Edit Graph fields](images/behavior-tree-machine-component.png)

---

## Layout

![The Behavior Tree editor window with five numbered regions: the Blackboard on the lower left, the Graph Inspector on the upper left, the zoom slider in the toolbar, the toolbar buttons on the upper right, and the node canvas filling the rest](images/editor-window-panels.png)

| Area | Description |
|---|---|
| **1 — Blackboard** | Edit variables across every scope, including those on the machine's GameObject. Tabs: Graph, Object, Scene, App, Saved |
| **2 — Graph Inspector** | Inspect the selected node. If you'd rather not use ports, you can expose values here in the traditional inspector style |
| **3 — Zoom** | Canvas zoom level |
| **4 — Toolbar** | Graph-level actions: Dim, Carry, Align, Distribute, Overview, Full Screen |
| **5 — Canvas** | The main editing area |

---

## Creating nodes

Right-click anywhere on the canvas and pick a node from the menu.

![The canvas right-click menu, open on the Composite submenu, listing Create Parallel Selector, Create Parallel Sequence, Random Selector, Random Sequence, Create Selector and Create Sequence](images/create-node-menu-composite.png)

Categories are named for what the node does, not where it came from: `Composite`, `Decorator`, `Condition`,
`Flow`, `Variables`, `Literal`, `Math`, `Logic`, `Navigation`, `Transform`, `Physics`, `Animation`,
`Game Object`, `Audio`, `Debug` and `Visual Scripting`. Every node that ships is listed in the
[Node Reference](node-reference.md).

| Action | Shortcut |
|---|---|
| Delete | Select node(s) → `Del` |
| Duplicate | Select node(s) → `Ctrl + D` |
| Copy | `Ctrl + C` |
| Paste | `Ctrl + V` |
| Cut | `Ctrl + X` |

> **Nodes containing a Visual Scripting graph cannot be copied, duplicated or pasted**, because they hold a
> direct reference to their Script Graph. To duplicate one, create a new node and copy the contents of its
> Script Graph by hand.

---

## Connecting nodes

**Parent to child:** hold `Ctrl`, drag from the parent to the child, and release. Press `Esc` to cancel.

![Two unconnected nodes on the canvas: a Sequence above, and an Add Explosive Force node below showing its Target, Explosion Origin, Explosion Force, Explosion Radius and Explosion Up Modifier ports](images/connect-parent-to-child.png)

Remember that the **canvas X position sets execution order** — the leftmost child of a container runs first.
Moving a node sideways changes its priority.

**Port to port:** drag from an output pin to an input pin. Press `Esc` to cancel.

![A GameObject node with an output pin on its right, next to an Add Explosive Force node whose Target input pin is ready to receive the connection](images/connect-value-ports.png)

---

## The node inspector

Most nodes are driven through ports, but you can also expose values directly in the inspector when you want
a simpler, more traditional workflow. Select any node to open it in the Graph Inspector.

![The Graph Inspector showing the selected Add Explosive Force node, with a Force Mode dropdown and the node's description text, while a green wire feeds its Target port on the canvas](images/graph-inspector-selected-node.png)

The panel also shows the node's own description, which is the quickest way to check what a node does without
leaving the window.

---

## Sticky notes

`Create Sticky Note` — at the top of the right-click menu — adds a canvas note. Trees get large quickly and
a note naming what a branch is for pays for itself.

---

## See also

- [Ports and Wiring](ports-and-wiring.md) — what those pins actually do
- [The Why Panel](why-panel.md) — a sidebar panel that explains why a node did what it did at runtime
