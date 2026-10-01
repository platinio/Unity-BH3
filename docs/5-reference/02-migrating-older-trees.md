# Migrating older trees

Behaviour that changed, what an asset from before the change looks like, and how to bring it forward. Each
section names the symptom first, so you can find yours.

None of these changes break a tree silently. Every one either keeps the old behaviour as a fallback, or
reports itself on the canvas and in `bt_verify`.

---

## Child order came from canvas position

**Symptom:** none. Old trees run exactly as they did.

Priority used to be decided by each child's X position on the canvas. It is now stored on the connection as
an index, shown as the numbered badge, and rewritten when you drag a child. See
[Execution order](../2-building-trees/05-execution-order.md).

Older assets stored index 0 on every connection. A set of all-zero indices is not an ordering, so BH3
treats it as "no order recorded" and falls back to canvas position, as those trees always behaved. The same
fallback applies whenever the indices are not a clean `0, 1, 2, …` run: a gap means a connection was removed
without renumbering, and a duplicate means two branches claim the same priority.

**To migrate:** drag any child of each composite once. The indices are rewritten from the layout and the
tree stops relying on the fallback. Trees built from code should pass the priority to `Connect`.

Connecting or deleting a child on the canvas also records the order, for that one composite: the editor
writes down the order it was already running in, with the new child slotted in or the deleted one taken out.
The canvas itself no longer leaves gaps behind, so a gap now only comes from an older asset or from code.

---

## Script Graph Variable nodes held an embedded graph

**Symptom:** every Script Graph Variable node in the tree is empty, marked `has no Function assigned` on the
canvas and by `bt_verify`. The Project window shows orphaned sub-assets under the tree asset.

A node used to hold an anonymous graph welded inside the tree asset, opened with an **Open Graph** button.
It now references a [Function](../2-building-trees/08-functions.md), a named project asset any tree can
reuse. Embedded graphs are gone entirely, along with the project-wide ledger that tracked which tree owned
each one, the sweep that deleted unreferenced ones, and the rule that a node holding one could not be
copied.

**To migrate:** re-author each read. Click the node's **Function** field, choose **Create new Function…**,
and rebuild the graph, or point the node at an existing Function. For a one-variable read,
`bt_add_variable_read` creates the Function for you. There is no automatic converter.

The old sub-assets stay inside the tree's `.asset` file, visible under it in the Project window and
reachable from nothing. Nothing removes them, because a tree saved by the current version cannot contain
one. Delete them by hand once the reads are re-authored; they are inert until you do.

---

## Function inputs were matched by agent variable name

**Symptom:** a Function that used to find its `threshold` from an agent variable called `threshold` now
reports `Required input 'threshold' has nothing connected`.

A Function's declared inputs are now **ports** on the node that reads it, fed at the call site. The old
behaviour, where a same-named agent variable fed the input invisibly, has been removed, not deprecated.
Ambient reads *inside* the graph still read the ambient scope, because they have no contract to declare
ports from.

**To migrate:** connect a value to the port. A literal, a variable read or another Function all work.

---

## Variable nodes defaulted to the `Flow` kind

**Symptom:** a Get, Set or Remove Variable node lights up with `has no variable store`, and throws naming the
node if run.

`Flow` was a Visual Scripting store a behavior tree could never serve, and the nodes used to default to it.
It now loads as **unset**.

**To migrate:** pick the kind the node was always meant to use. See
[Variables and scope](../2-building-trees/04-variables-and-scope.md).

---

## Graph-kind reads did not walk outward

**Symptom, on the old build only:** a sub-tree throws `Variable not found` reading a parameter its caller
definitely passed, or a branch can write a Graph value it cannot then read back.

A `Get Variable` of kind Graph used to read the *root* tree's variables regardless of which sub-tree it sat
in. It now looks in the branch's own scope first, then its caller's, out to the root. Writes always went to
the branch's own scope, which is why the old behaviour produced both symptoms. Nothing to migrate: trees
that worked around it by using Object variables keep working.

---

## Conditional Executions re-evaluated every tick

**Symptom:** `bt_verify` reports an entry-only guard on a Selector branch that used to abort its branch.

A **Conditional Execution** used to re-run every tick and abort its owner when it turned false. It is now
asked once, at entry, and the re-evaluate, abort and take-over behaviour belongs to the **Reactive Guard**,
which also lets a higher-priority branch take the slot from a running lower one. See
[Guards](../2-building-trees/06-guards.md).

**To migrate:** for each guard that should interrupt its branch, delete the Conditional Execution and add a
Reactive Guard in its place, feeding it the same condition. `bt_guard_on_variable` and
`bt_guard_on_function` build reactive guards by default.

---

## Load and Save lived on the Why panel

**Symptom:** the Why panel has no Load or Export button.

They moved to the [Timeline](../3-debugging/02-timeline.md), so that only one recording can be open at a
time and every panel describes the same one.

---

## Placeholders for missing node types kept nothing

**Symptom:** a red `MISSING` node whose inspector says it has no former type and no preserved state.

A placeholder written by an older BH3, before it preserved state, knows nothing about what it replaced. It
can only be replaced from defaults or deleted. Placeholders written since keep every value, reference and
wire. See [Renaming and deleting node types](../4-extending-with-csharp/03-renaming-and-deleting-node-types.md).

---

## Transitions serialized placeholder nodes

**Symptom:** none.

Each transition used to serialize three invisible `PlaceHolderNode`s so its line could be selected. They are
now created by the canvas while the tree is open and never saved. Old assets that still carry them discard
them on load. Tooling that walks `graph.Nodes` no longer needs to filter them out.

---

## Transitions could end on a guard

**Symptom:** `bt_verify` reports `transition from 'X' ends on guard 'Y' (owner 'Z')`, or the same sentence
appears as an error when an agent awakes. Before the fix the tree ran and did nothing: the guard was child 0
of its parent and returned Success on every tick, and the node it was meant to gate was an orphan.

A guard is drawn stacked on top of its owner, exactly where an incoming line lands, and the canvas used to
accept a transition released there as a transition *into the guard*. Guards now refuse incoming transitions,
a release over the guard box lands on the owner, and `bt_connect` refuses a guard as either end.

**To migrate:** open the tree. The canvas re-points every such wire at the guard's owner on open, keeping
its priority; a wire whose owner was already connected from the same parent is removed instead. Save. Until
a tree is opened, the runtime ignores the wire and logs the error above at awake, so an agent running an
unrepaired asset fails where you can see it rather than succeeding silently.

---

## Next

- [Glossary](03-glossary.md)
- [Demos and tests](01-samples-and-demos.md)
