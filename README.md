# BH3 — Behavior Trees for Unity

A visual, node-based behavior tree system for AI and gameplay logic, built on top of Unity Visual Scripting.

---

## What BH3 is

BH3 brings behavior trees and Visual Scripting together the way Unreal's Behavior Tree does: **nodes have
ports, and any port can be fed by a Visual Scripting graph**. A designer builds node logic without writing
code; a programmer writes reusable nodes in C# and wires them the same way.

Three ideas carry most of the weight, and the documentation is built around them:

| Idea | In one sentence | Read |
|---|---|---|
| **Ports separate what a node does from where its data comes from** | A node reads a port. It never knows whether the value came from a variable, a literal, another node or a whole Visual Scripting graph. | [Ports and wiring](docs/2-building-trees/03-ports-and-wiring.md) |
| **A branch is a function** | Put a branch in its own tree asset, pass it parameters through ports, and run it from any agent. Its variables are private to that call; facts about the agent live on the agent. | [Sub-trees](docs/2-building-trees/07-sub-trees.md) |
| **Guards decide when a branch may run, and when it must stop** | A Reactive Guard watches a condition while its branch runs, aborts the branch when the condition drops, and can take the slot from a lower-priority branch when it rises. | [Guards](docs/2-building-trees/06-guards.md) |

When an agent does the wrong thing, BH3 has already recorded what every node, guard and variable did. The
**Why panel** turns that into a sentence, the **Timeline** lets you scrub back to the moment, and a
**breakpoint** stops the editor when it happens again. See [Debugging](docs/3-debugging/01-overview.md).

---

## Start here

1. **[Installation](docs/1-start-here/01-installation.md)** — Unity 6, the Visual Scripting package, and the four modules BH3 sits on.
2. **[Your first tree](docs/1-start-here/02-your-first-tree.md)** — create an asset, build `Entry → Repeater → Sequence`, add the machine, press Play.
3. **[Core concepts](docs/1-start-here/03-core-concepts.md)** — the mental model on one page.

Then work through [Building trees](docs/README.md#2-building-trees) in order. The full table of contents is
in **[docs/README.md](docs/README.md)**.

---

## Where things are

| Folder | Contents |
|---|---|
| `Runtime/` | Nodes, the machine, variables, the flight recorder |
| `Editor/` | The graph window, panels, inspectors, authoring and verification tools |
| `Test/` | `EditMode/` and `PlayMode/` test assemblies, plus the fixture trees the verification test runs over. Feature demos live in the reference project; see [Demos and tests](docs/5-reference/01-samples-and-demos.md) |
| `docs/` | This documentation. |

## New to behavior trees?

- [Behavior tree (Wikipedia)](https://en.wikipedia.org/wiki/Behavior_tree_%28artificial_intelligence,_robotics_and_control%29)
- [Introduction to behavior trees (Robohub)](https://robohub.org/introduction-to-behavior-trees/)
- [Behavior trees explained (video)](https://www.youtube.com/watch?v=6VBCXvfNlCM)
