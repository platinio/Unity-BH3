# Functions

A **Function** is a named, shared Visual Scripting graph with a declared contract — the thing a
`hasTarget` predicate should have been all along, instead of a three-unit graph rebuilt by hand in every
tree that needed it.

Functions are owned by the VisualScriptingExtension module. This page is the behavior-tree side: how a tree
uses one. For the contract model, the evaluation seam and the performance rules, see
[the Functions guide](../../Modules/VisualScriptingExtension/docs/functions.md).

## Using one from a tree

A **Script Graph Variable** node can read either an embedded graph, as it always could, or a Function.
Assign a Function and the node evaluates it through the shared seam; leave it unset and nothing about the
node changes. Existing trees are unaffected.

New graphs, however, are Functions. The node's inspector no longer offers to create an embedded one-off --
see [Embedded graphs are being retired](#embedded-graphs-are-being-retired).

The gain is that the Function is one asset. Fix a predicate once and every tree referencing it is fixed —
no copy to find, nothing to keep in step.

```
Assets/AI/Functions/HasTarget.asset
        ├── referenced by Zombie.asset
        └── referenced by Soldier.asset
```

## Picking one

Click the **Function** field on a Script Graph Variable node and you get a searchable dropdown of the
Functions that can legally go there -- not every Function in the project.

What "legally" means is decided by the port the node feeds. A node is deliberately untyped: it declares its
output as `object` so that one node type can serve predicates, floats and queries alike. The requirement
therefore lives downstream -- a reactive guard's `Value` port is a `bool`, and that port is the only thing
that knows `bool` is required.

| The node's output feeds | The dropdown offers |
|---|---|
| a `bool` port | Functions whose `Result` is a `bool` |
| a `Component` port | Functions returning `Component` **or a subclass** -- a `Transform` Function is offered |
| several ports | only what fills all of them |
| nothing yet | everything that returns something, grouped by flavor |

Entries are grouped **Predicates / Queries / Values**, and each row names the inputs the node will owe the
Function -- `IsHurt -- needs threshold` -- so what you are signing up for is visible before you choose, not
after. Two Functions sharing a name are qualified by their folder, because a choice between two identical
rows is worse than no choice.

A Function that declares **no `Result`** is never offered, at any port, even an unwired one: this node
exists to read a value, and a Function with no result has none to give.

### When the list is empty

Two different things look the same and are not:

* **"No Function returns X yet."** The project is missing one. **Create new Function...** asks where to save
  and makes one that already declares the right `Result`, so it is valid the moment it exists.
* **"This node feeds bool and Transform, which no value can be at once."** The *wiring* is the problem. No
  Function will ever fix it; disconnect one of the ports.

### A Function that stopped fitting

If a Function's `Result` type changes after it was assigned, the node keeps the reference and draws it in
error, naming the mismatch. It is not silently cleared -- destroying an authored assignment to enforce a
rule you cannot yet see would be worse than showing it broken.

### Embedded graphs are being retired

The inspector used to create an anonymous embedded graph whenever a node had nothing assigned, which is how
most embedded graphs in this project came to exist. It no longer does; **Create new Function...** replaces
it.

A node that **already** holds an embedded graph is unaffected: **Open Graph** still opens it, and
**Extract to Function** promotes it to a project asset and re-points the node at it -- the same operation
`fn_extract` performs from the CLI. The original sub-asset stays in the tree and `bt_verify` reports it as
an orphan, because deletion has exactly one owner at a time.

## Declared inputs are ports

Assigning a Function grows **one input port per input it declares**, typed the way the Function declared
it. That is how a call site passes an argument:

```
Float Literal 80 ──▶ threshold ┐
                               ├─ Script Graph Variable (IsLowHealth) ──▶ guard
```

Feed a port with a literal, a variable read, or any other value node — the same as every other port in
BH3. Because the argument lives at the call site, one Function serves four agents that each want a
different answer, without copying anything.

**Required and optional come from the Function.** An input the Function gives a default is *optional*: the
port carries that default and is safe to leave unconnected. An input with no default is *required*, and
leaving it unconnected is the ordinary unset-port case — `bt_verify` reports it before anything runs, and
reading it fails naming this node and the input rather than throwing `KeyNotFoundException` from somewhere
inside the graph.

So a Function states the sensible value once, and a call site disagrees with it explicitly.

### Ports are a copy, and the copy can drift

A node declares its ports from a **copy of the contract stored on the node**, never by reading the Function
live. This is the same bargain sub-tree nodes make, for the same reason: `Definition()` runs while the graph
is being deserialized, connections resolve by port key, and a key that does not exist yet is dropped
*silently* — so reading the asset there would lose wiring on any load where it had not resolved yet, which
is an import-order failure and therefore shows up on one machine and not another.

The cost is that the copy can fall behind the Function. That is the better failure, because it can be
reported:

- **The node marks itself on the canvas** — a red border and an error icon, with the problem and its fix on
  hover. You see it when you open the tree, not when you press Play.
- **Refresh Ports** on the node's right-click menu, or `fn_refresh_ports`, rebuilds it.
- Refreshing removes ports the Function no longer declares, **and the connections feeding them**. Both the
  menu and the command say which connections that cost. There is no undo, so the report is the mitigation.

The node is resized to fit its ports whenever its contract changes — and only then, so a size you set by
hand survives everything except the next contract change.

### What a node tells you before you press Play

A node that cannot work marks itself: a red border, an error icon in the corner, and — on hover — what is
wrong and what to do about it. A warning (amber) means it will still do something sensible; an error means
it will throw or quietly do the wrong thing.

What a Script Graph Variable reports today:

| On the node | Means | Fix |
|---|---|---|
| `added: armour : Single` | The Function gained an input this node has never heard of | **Refresh Ports** |
| `Required input 'threshold' has nothing connected` | The port exists and is empty | Connect a value |
| `Both a Function and an embedded graph are assigned` | The Function runs; the graph is editable but dead | Clear one |
| `No Function or graph assigned` | The node has nothing to read | Assign one |

A sub-tree node reports the same shapes against its own contract. Neither used to say anything at all —
you found out when the tree ran.

This is a **general** mechanism, not a Function one: a node reports itself by implementing
`BehaviorTreeNode.CollectProblems`, and a rule that lives outside the node registers a provider with
`NodeProblemCache`. New
checks appear on the canvas without the drawing code being touched.

> **Freshness.** The badge recomputes on two signals. The first is the evaluation layer dropping its own
> caches — the same counter, on purpose, so the badge and the runtime cannot disagree about whether a node is
> fine. The second is the graph changing shape: connecting a port, disconnecting one, adding or deleting
> anything. That second one is why `Required input 'threshold' has nothing connected` clears the moment you
> connect a value, rather than lingering until something unrelated happened to refresh it.
>
> The one lag: a Function edited in the graph window and not yet saved. The evaluator lags identically, so
> the badge is still honest about what would happen if you pressed Play right now.

> **Before this existed**, a declared input was matched by name against the agent's own variables: you
> supplied `threshold` by declaring an agent variable called `threshold`. That worked, but nothing about it
> was visible on the node, and two agents could not disagree. It has been **removed**, not deprecated — a
> same-named agent variable no longer feeds a Function. Embedded graphs are untouched and still read the
> ambient scope, because they have no contract to declare ports from.

## Functions and guards

A reactive guard's condition is a natural Function: small, pure, and asked repeatedly.

Two things carry over from [Reactive Guards](reactive-guards.md) and matter more once the condition is
shared:

- **A guard may only read.** A guard is evaluated on its own schedule, including while unrelated branches
  run, so a condition that writes fires that side effect for the life of the agent. A Function declares its
  purity, defaulting to pure, and that declaration is what callers rely on.
- **Watched keys decide whether a guard ever wakes.** They belong on the Function, beside the graph that
  actually does the reading — a guard whose condition is a Function inherits them, so the list stops being
  something each call site has to remember and get right.

## Authoring from code

| Command | What it does |
|---|---|
| `fn_create --path <asset>` | New Function, already runnable end to end. |
| `fn_list [--folder <f>]` | Every Function with flavor, contract size and purity. |
| `fn_describe --function <asset>` | Full contract, and why it cannot be evaluated if it cannot. |
| `fn_set_metadata --function <asset> [--pure] [--watched_keys] [--description]` | Asset-level metadata. |
| `fn_refresh_ports --tree <asset> [--node <guid>]` | Rebuild a node's input ports from its Function's contract. Reports the drift it repaired and any connection that cost. |

Same philosophy as `bt_list_nodes`: the catalogue is derived from the assets, so it cannot go stale the way
a written table does. `fn_describe` is the fastest way to find out why a Function is not returning what you
expect — a missing `Result` output is reported by name rather than failing at runtime and nowhere else.

## What has not moved yet

Functions sit beside the older embedded-graph machinery rather than having replaced it. Embedded graphs
that already exist, the script-graph repository and its canvas sweep all still work as before.

What has changed is the direction: **no surface creates a new embedded graph any more.** Retiring the rest
-- migrating the graphs that exist, then removing the repository and the sweep -- is sequenced separately,
so that there is never more than one thing deleting graphs at a time.
