# Behavior Trees — BH3

A visual node-based behavior tree system for designing AI and gameplay logic, built on top of Unity Visual
Scripting.

---

## What BH3 is

BH3 is a Behavior Tree implementation for Unity that leans heavily on Visual Scripting. You can build custom
node logic without writing a single line of code, which makes it approachable if you are an artist or
designer exploring behavior trees — and convenient for programmers writing one-off nodes or making a quick
change on the fly.

The idea behind BH3 was to bring behavior trees and Visual Scripting together the way Unreal's Behavior Tree
does: nodes have **ports**, and those ports can be fed by Visual Scripting graphs.

## The one idea worth understanding first

**BH3 uses ports to separate what a node *does* from where it gets its *data*.**

A node doesn't care whether a value comes from the Blackboard, a Visual Scripting graph, or a typed-in
literal — it just reads its port. That removes the usual chore of writing code to keep a blackboard in sync,
because fetching data and acting on it become two independent pieces you can rearrange freely.

```
[ Get Escape Position ]──Value ──▶ NavPosition──[ Set Nav Agent Position ]
      fetches                                            acts
```

Swap the left-hand node for a Visual Scripting graph, a blackboard read, or a literal, and the right-hand
node neither knows nor cares. That single seam is what most of the rest of these docs is about — see
[Ports and Wiring](docs/ports-and-wiring.md).

---

## Quick setup

**1. Create a tree asset** — right-click in the Project window → **Create → Visual Scripting → Behavior
Tree**.

**2. Open the editor** — double-click the asset. An **Entry** node is created for you; it is the root and
cannot be deleted.

**3. Build the tree** — right-click the canvas to add nodes. Hold `Ctrl` and drag from parent to child to
connect them.

**4. Add the machine** — put a **BehaviorTreeMachine** component on a GameObject and drag your tree asset
into its **Graph** field.

**5. Press Play** — the machine loads the graph on `Awake`, starts on `Start`, and ticks it every frame in
`Update`. No extra code required.

> **Put a `Repeater` under `Entry`.** The machine stops ticking once the root returns `Success` or `Failure`,
> so a tree without one runs exactly once and then goes quiet. This surprises nearly everyone the first time.

The full walkthrough, with screenshots, is in [Getting Started](docs/getting-started.md).

---

## Documentation

| Guide | What's in it |
|---|---|
| **[Getting Started](docs/getting-started.md)** | Install, first tree, and the mistakes that bite on day one |
| **[The Graph Editor](docs/editor-guide.md)** | Window layout, creating and connecting nodes, the inspector |
| **[Node Reference](docs/node-reference.md)** | Every node that ships, by menu category |
| **[Ports and Wiring](docs/ports-and-wiring.md)** | How ports work, and feeding them from Visual Scripting |
| **[Execution Order](docs/execution-order.md)** | Which branch runs first, the priority badges, and reordering |
| **[Custom Nodes](docs/custom-nodes.md)** | Writing your own actions, conditions and decorators |
| **[Sub-Behavior Trees](docs/sub-behavior-trees.md)** | Reusing branches, and passing parameters to them |
| **[Best Practices](docs/best-practices.md)** | Patterns that keep trees reusable and designer-friendly |
| **[The Why Panel](docs/why-panel.md)** | Click a node, read why it did what it did |
| **[The Variable Watch](docs/variable-watch.md)** | What every variable held at a given tick, and who wrote it |
| **[Breakpoints](docs/breakpoints.md)** | Pause the editor on a node, a variable write, or a guard flip |
| **[API Reference](docs/api-reference.md)** | Runtime types, lifecycle, and `ExecutionStatus` |
| **[Authoring From Code](docs/authoring-from-code.md)** | Generating trees programmatically, and the traps in it |

**Debugging:** when a branch doesn't run and you can't see why, open
**[The Why Panel](docs/why-panel.md)**. When you need the value that caused it — what `hasTarget` was three
seconds ago, and which sensor set it — open **[The Variable Watch](docs/variable-watch.md)**. Both read back
what was actually recorded rather than guessing. To catch something *in the act* rather than explain it
afterwards, arm a **[breakpoint](docs/breakpoints.md)** — the editor pauses on the exact frame, with the
canvas, the timeline and the Why panel all describing that moment.

---

## New to behavior trees?

A behavior tree is a visual representation of an NPC's decision making. If you want the background theory
first:

- [Behavior tree (Wikipedia)](https://en.wikipedia.org/wiki/Behavior_tree_\(artificial_intelligence,_robotics_and_control\))
- [Introduction to behavior trees (Robohub)](https://robohub.org/introduction-to-behavior-trees/)
- [Behavior trees explained (video)](https://www.youtube.com/watch?v=6VBCXvfNlCM)

---

## Related modules

- **[Tactical Position Selection](../Modules/TacticalPositionSelection/README.md)** — how an agent decides
  *where* to stand. Pairs with BH3 through the `TacticalPositionSelection` node.
