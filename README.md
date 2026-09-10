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

## Documentation

### 1. Start here

| Page | Read it when |
|---|---|
| [Installation](docs/1-start-here/01-installation.md) | You are adding BH3 to a project, or checking that a checkout works |
| [Your first tree](docs/1-start-here/02-your-first-tree.md) | You have never built a BH3 tree. Fifteen minutes, ends with an agent running |
| [Core concepts](docs/1-start-here/03-core-concepts.md) | You want the whole mental model on one page before going deeper |

### 2. Building trees

| Page | What it covers |
|---|---|
| [The graph editor](docs/2-building-trees/01-the-graph-editor.md) | The window, its panels, creating and connecting nodes, shortcuts |
| [Node reference](docs/2-building-trees/02-node-reference.md) | Every node that ships, by menu category, with the ports you must connect |
| [Ports and wiring](docs/2-building-trees/03-ports-and-wiring.md) | How a node gets its data: defaults, literals, conversions, Script Graph Variables |
| [Variables and scope](docs/2-building-trees/04-variables-and-scope.md) | The variable kinds, the Blackboard, what a branch can see and write |
| [Execution order](docs/2-building-trees/05-execution-order.md) | Which child runs first, the priority badge, reordering |
| [Guards](docs/2-building-trees/06-guards.md) | Conditional Execution and Reactive Guard: gating, aborting, taking over, triggers |
| [Sub-trees](docs/2-building-trees/07-sub-trees.md) | Reusing a branch as its own asset, and passing it parameters |
| [Functions](docs/2-building-trees/08-functions.md) | Shared Visual Scripting graphs with a declared contract |
| [Checking your tree](docs/2-building-trees/09-checking-your-tree.md) | Problem badges, one-click repairs, `bt_verify`, the broken-graph finder |
| [Best practices](docs/2-building-trees/10-best-practices.md) | Patterns that keep trees reusable and designer-friendly |

### 3. Debugging

| Page | What it answers |
|---|---|
| [Overview](docs/3-debugging/01-overview.md) | What is recorded, which panel answers which question, what it costs |
| [Timeline](docs/3-debugging/02-timeline.md) | *What did the tree look like at tick N?* Scrub, play back, save and load recordings |
| [Why panel](docs/3-debugging/03-why-panel.md) | *Why did this node do that, and why not something else?* |
| [Variable watch](docs/3-debugging/04-variable-watch.md) | *What was this variable at that moment, and who wrote it?* |
| [Breakpoints](docs/3-debugging/05-breakpoints.md) | *Stop the editor the instant it happens* |

### 4. Extending BH3 with C#

| Page | What it covers |
|---|---|
| [Custom nodes](docs/4-extending-with-csharp/01-custom-nodes.md) | Writing actions, conditions, guards and decorators |
| [API reference](docs/4-extending-with-csharp/02-api-reference.md) | The runtime types: `BehaviorTreeMachine`, `BTContext`, `ExecutionStatus`, node lifecycle |
| [Renaming and deleting node types](docs/4-extending-with-csharp/03-renaming-and-deleting-node-types.md) | What happens to trees when a node class goes away, and how to repair them |
| [Authoring from code](docs/4-extending-with-csharp/04-authoring-from-code.md) | Generating trees from C#, and the traps in it |
| [Command-line tools](docs/4-extending-with-csharp/05-command-line-tools.md) | The `bt_*` and `fn_*` commands, and every message `bt_verify` can emit |
---

## Where things are

| Folder | Contents |
|---|---|
| `Runtime/` | Nodes, the machine, variables, the flight recorder |
| `Editor/` | The graph window, panels, inspectors, authoring and verification tools |
| `Test/` | `EditMode/` and `PlayMode/` test assemblies, plus the fixture trees the verification test runs over. Feature demos live in the reference project; see [Demos and tests](docs/5-reference/01-samples-and-demos.md) |
| `docs/` | This documentation. [docs/README.md](docs/README.md) is the same table of contents, for browsing inside the folder |

## New to behavior trees?

- [Behavior tree (Wikipedia)](https://en.wikipedia.org/wiki/Behavior_tree_%28artificial_intelligence,_robotics_and_control%29)
- [Introduction to behavior trees (Robohub)](https://robohub.org/introduction-to-behavior-trees/)
- [Behavior trees explained (video)](https://www.youtube.com/watch?v=6VBCXvfNlCM)
