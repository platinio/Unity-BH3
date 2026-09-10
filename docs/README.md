# BH3 documentation

Read the parts in order the first time. Each page says what it is for in its first line, and ends with where
to go next.

## 1. Start here

| Page | Read it when |
|---|---|
| [Installation](1-start-here/01-installation.md) | You are adding BH3 to a project, or checking that a checkout works |
| [Your first tree](1-start-here/02-your-first-tree.md) | You have never built a BH3 tree. Fifteen minutes, ends with an agent running |
| [Core concepts](1-start-here/03-core-concepts.md) | You want the whole mental model on one page before going deeper |

## 2. Building trees

| Page | What it covers |
|---|---|
| [The graph editor](2-building-trees/01-the-graph-editor.md) | The window, its panels, creating and connecting nodes, shortcuts |
| [Node reference](2-building-trees/02-node-reference.md) | Every node that ships, by menu category, with the ports you must connect |
| [Ports and wiring](2-building-trees/03-ports-and-wiring.md) | How a node gets its data: defaults, literals, conversions, Script Graph Variables |
| [Variables and scope](2-building-trees/04-variables-and-scope.md) | The variable kinds, the Blackboard, what a branch can see and write |
| [Execution order](2-building-trees/05-execution-order.md) | Which child runs first, the priority badge, reordering |
| [Guards](2-building-trees/06-guards.md) | Conditional Execution and Reactive Guard: gating, aborting, taking over, triggers |
| [Sub-trees](2-building-trees/07-sub-trees.md) | Reusing a branch as its own asset, and passing it parameters |
| [Functions](2-building-trees/08-functions.md) | Shared Visual Scripting graphs with a declared contract |
| [Checking your tree](2-building-trees/09-checking-your-tree.md) | Problem badges, one-click repairs, `bt_verify`, the broken-graph finder |
| [Best practices](2-building-trees/10-best-practices.md) | Patterns that keep trees reusable and designer-friendly |

## 3. Debugging

| Page | What it answers |
|---|---|
| [Overview](3-debugging/01-overview.md) | What is recorded, which panel answers which question, what it costs |
| [Timeline](3-debugging/02-timeline.md) | *What did the tree look like at tick N?* Scrub, play back, save and load recordings |
| [Why panel](3-debugging/03-why-panel.md) | *Why did this node do that, and why not something else?* |
| [Variable watch](3-debugging/04-variable-watch.md) | *What was this variable at that moment, and who wrote it?* |
| [Breakpoints](3-debugging/05-breakpoints.md) | *Stop the editor the instant it happens* |

## 4. Extending BH3 with C#

| Page | What it covers |
|---|---|
| [Custom nodes](4-extending-with-csharp/01-custom-nodes.md) | Writing actions, conditions, guards and decorators |
| [API reference](4-extending-with-csharp/02-api-reference.md) | The runtime types: `BehaviorTreeMachine`, `BTContext`, `ExecutionStatus`, node lifecycle |
| [Renaming and deleting node types](4-extending-with-csharp/03-renaming-and-deleting-node-types.md) | What happens to trees when a node class goes away, and how to repair them |
| [Authoring from code](4-extending-with-csharp/04-authoring-from-code.md) | Generating trees from C#, and the traps in it |
| [Command-line tools](4-extending-with-csharp/05-command-line-tools.md) | The `bt_*` and `fn_*` commands, and every message `bt_verify` can emit |

## 5. Reference

| Page | What it covers |
|---|---|
| [Demos and tests](5-reference/01-samples-and-demos.md) | The feature demos in the reference project, one scene per feature, and the test assemblies |
| [Migrating older trees](5-reference/02-migrating-older-trees.md) | Behaviour that changed, what an old asset looks like, and how to bring it forward |
| [Glossary](5-reference/03-glossary.md) | The words these pages use, defined once |

## Design notes

`design/` holds the numbered specifications that were written before each feature was built. They record
decisions and alternatives, and they are not kept in step with the guides above. New users can skip them.
