# Command-line tools

The `bt_*` and `fn_*` commands: what each does, its arguments, and every message `bt_verify` can emit.

These commands run inside the Editor through the `com.unity.pipeline` package, which is optional: they
live in `ArcaneOnyx.BehaviorTree.Editor.Commands`, an assembly that compiles only when the package is
installed, and nothing else in BH3 needs it. From a shell they are invoked as `unity command <name> --arg
value`; from an agent or an MCP client they are the tools of the same names. Every command that edits a
tree has a C# helper beside it in `BehaviorTreeAuthoring`, which works with or without the package; see
[Authoring from code](04-authoring-from-code.md).

Asset paths may omit the `Assets/` prefix and the `.asset` extension. Node guids come from
`bt_describe_tree`.

---

## Reading a tree

| Command | Arguments | What it does |
|---|---|---|
| `bt_list_nodes` | `--type X` for one node's full port list, `--category X` to narrow | The node catalogue from the code: category, description, child limit, and which ports must be connected. Call this before `bt_add_node` rather than reading source |
| `bt_describe_tree` | `--tree`, `--reload` (default on) | The tree as JSON: the execution hierarchy in runtime order, every node's guid, each port's source or inline value, guards, sub-trees expanded in place, and unreachable nodes |
| `bt_describe_script_graph` | `--graph` | Any Visual Scripting graph asset as JSON: its port definitions, then every unit with guid, inline values and control edges |
| `bt_verify` | `--trees` (comma-separated) | Reload the trees and report what would break at runtime. See below |

`bt_verify` is not read-only: the reload round trip saves every dirty asset in the project.

## Building a tree

| Command | Arguments | What it does |
|---|---|---|
| `bt_create_tree` | `--path`, `--overwrite` | An empty tree with only its Entry node. Without `--overwrite` an existing asset is left alone |
| `bt_add_node` | `--tree`, `--type`, `--x`, `--y` | Add a node, sized the way the editor sizes it. Returns its guid |
| `bt_connect` | `--tree`, `--parent`, `--child`, `--index` | Parent one node under another. `--index` is the priority. Refuses a second child on Entry or a decorator, and refuses a guard as either end: guards attach to their owner, so connect to the owner |
| `bt_set_value` | `--tree`, `--node`, `--port`, `--value`, `--x`, `--y` | Fill a port by whichever mechanism survives a reload: inline where the port declares a default, a connected literal where it does not. Prefer this over every other way of filling a port |
| `bt_feed_float` / `bt_feed_vector3` | `--tree`, `--node`, `--port`, `--value`, `--x`, `--y` | Force a literal node even where an inline value would do. Vector3 as `x,y,z` |
| `bt_declare` | `--tree`, `--name`, `--value`, `--type` (string, float, int, bool, vector2, vector3), `--scope` (instance, required, optional) | Declare a variable on the tree. On a sub-tree, use `required` or `optional` |
| `bt_add_sub_tree` | `--tree`, `--sub_tree`, `--x`, `--y` | A Run Behavior Tree Graph node pointed at another tree, with its parameter ports built |
| `bt_add_variable_read` | `--tree`, `--variable`, `--fallback`, `--fallback_type`, `--x`, `--y` | A Script Graph Variable node reading one agent variable through a generated Function, with a fallback so a reused branch does not throw |
| `bt_guard_on_variable` | `--tree`, `--owner`, `--variable`, `--expected`, `--fallback`, `--x`, `--y` | A reactive guard on a boolean agent variable, waking when it changes. `--expected false` inserts a Not |
| `bt_guard_on_function` | `--tree`, `--owner`, `--function`, `--expected`, `--x`, `--y` | A reactive guard on a Function returning bool, with its trigger seeded from the Function's watched keys. Prefer it when the condition is more than one variable read |
| `bt_add_sticky` | `--tree`, `--title`, `--body`, `--x`, `--y`, `--width`, `--height`, `--color` | A canvas note. Reusing a title replaces that note |
| `bt_set_comment` | `--tree`, `--node`, `--comment` | A node's comment, shown as its title on the canvas |
| `bt_save` | `--tree` | Flush edits to disk. Not undoable |

The command-line guards are always reactive; the C# `GuardOnVariable` and `GuardOnFunction` helpers take a
`GuardKind` for an entry-only Conditional Execution.

## Repairing a tree

| Command | Arguments | What it does |
|---|---|---|
| `bt_refresh_sub_tree_ports` | `--tree`, `--node` (omit for all) | Rebuild a Run Behavior Tree Graph node's parameter ports from the sub-tree's declarations |
| `bt_refresh_guard_keys` | `--tree`, `--node` (omit for all) | Add to each reactive guard's key trigger whatever its Function declares and the trigger does not list. Adds only |
| `bt_retarget_missing` | `--former`, `--to`, `--tree` (omit for the whole project) | Replace nodes whose type no longer exists with one that does, keeping every value and connection the replacement has a home for. The durable fix for code you own is `[RenamedFrom]` |

## Functions

| Command | Arguments | What it does |
|---|---|---|
| `fn_create` | `--path`, `--result` (type name, e.g. `System.Boolean`) | A new Function, with Enter wired to Exit so it runs before you have declared anything |
| `fn_list` | `--folder` | Every Function with its flavour, contract size and purity |
| `fn_describe` | `--function` | The full contract: inputs with types and optionality, outputs, purity, watched keys, and whether it can be evaluated |
| `fn_set_metadata` | `--function`, `--pure`, `--watched_keys` (comma-separated), `--description` | The asset-level metadata a graph cannot express |
| `fn_rename_output` | `--function`, `--from`, `--to` (omit for `Result`) | Rename an output, carrying its wires. The main use is a Function whose result is not named `Result`, which evaluates fine but no caller reads |
| `fn_refresh_ports` | `--tree`, `--node` (omit for all) | Rebuild every Script Graph Variable node's input ports from its Function's contract, reporting the drift and any connection it cost |

Tactical Position Selection ships its own `tps_*` family; see
[that module's authoring guide](https://github.com/platinio/Unity-TacticalPositionSelection/blob/main/docs/authoring-queries-from-code.md).

---

## What `bt_verify` reports

Each line is prefixed with the tree's name. Grouped by what it checks:

**Ports and structure**

- `port 'X' is unset and will throw when read`
- `nothing reaches or reads this node` — an orphan
- `(none assigned)` — a Run Behavior Tree Graph node with no asset
- `recursion` — a tree that reaches itself, with the path
- `layout ≠ priority — 'Selector' runs priority 1 ('Attack' at x=…) before priority 2 ('Chase' at x=…), but lays them out the other way round`
- `invalid connection -- 'from'.Output (Boolean) no longer fits 'to'.Target (Transform)` — a wire demoted after a Function's result changed
- `transition from 'Selector' ends on guard 'G' (owner 'Sequence'). Guards attach to their owner and are never children` — the runtime ignores the wire and logs the same sentence at awake; opening the tree re-points it at the owner

**Missing types**

- `node at (x, y) has a type that no longer exists (formerly 'X')`, with a fix line that depends on whether preserved state exists

**Sub-trees**

- `sub-tree contract — added: name : Type` / `removed: …` / `retyped: …` — refresh with `bt_refresh_sub_tree_ports`

**Guards**

- `'guard' writes a variable ('unit'). A guard must only read`
- an entry-only guard on a Selector branch — make it a Reactive Guard if it is meant to interrupt
- `'guard' watches no keys, so nothing can ever mark it dirty`
- `'guard' has no triggers, so it re-checks every tick` — with a variant naming the keys its Function declares
- `'guard' does not list 'key' among its watched keys, but its condition declares it` — refresh with `bt_refresh_guard_keys`
- `'guard' is set to preempt, but its owner is not a direct child of a Selector` — preemption is defined there and not yet active anywhere else
- `'key' is watched by a Reactive Guard but written by Unity's stock Set Variable unit in 'graph', which cannot wake it. Use Set Behavior Tree Variable`

**Functions**

- `has no Function assigned`
- `Function contract <drift>. Refresh its ports with fn_refresh_ports`
- `reads Function 'F', which returns X, but the node feeds Y`
- `Function 'F' declares no 'Result' output`
- `Function — <why it cannot be evaluated>`
- a lifecycle slot whose Function returns the wrong type, or declares a required input nothing can feed
- a Function declared pure that writes, a watched key it never reads, or a read key it never declares

Every one of these also appears as a problem badge on the node concerned, except the whole-tree checks
(orphans, recursion, layout). See [Checking your tree](../2-building-trees/09-checking-your-tree.md).

---

## Next

- [Authoring from code](04-authoring-from-code.md)
- [Checking your tree](../2-building-trees/09-checking-your-tree.md)
