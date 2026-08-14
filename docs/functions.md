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
node changes. Both work today, so existing trees are unaffected.

The gain is that the Function is one asset. Fix a predicate once and every tree referencing it is fixed —
no copy to find, nothing to keep in step.

```
Assets/AI/Functions/HasTarget.asset
        ├── referenced by Zombie.asset
        └── referenced by Soldier.asset
```

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

Same philosophy as `bt_list_nodes`: the catalogue is derived from the assets, so it cannot go stale the way
a written table does. `fn_describe` is the fastest way to find out why a Function is not returning what you
expect — a missing `Result` output is reported by name rather than failing at runtime and nowhere else.

## What has not moved yet

Functions currently sit beside the older embedded-graph machinery rather than replacing it. Embedded
one-off graphs, the script-graph repository and its canvas sweep all still work as before. Retiring them is
sequenced separately, so that there is never more than one thing deleting graphs at a time.
