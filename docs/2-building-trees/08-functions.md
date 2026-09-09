# Functions

A **Function** is a Visual Scripting graph saved as its own asset, with a declared contract: its inputs, its
result, and the agent facts it reads. It is what a `hasTarget` predicate should have been all along, instead
of a three-unit graph rebuilt by hand in every tree that needed it.

Functions are owned by the [Visual Scripting Extension](https://github.com/platinio/visual-scripting-extension)
module. This page is the behavior-tree side: how a tree uses one. For the contract model, evaluation and
performance rules, see that module's [Functions guide](https://github.com/platinio/visual-scripting-extension/blob/main/docs/functions.md).

---

## Using one from a tree

A **Script Graph Variable** node runs a Function and offers its result on an output port. That is the only
way the node gets a graph, and the gain is that the Function is one asset: fix a predicate once and every
tree referencing it is fixed, with no copies to find.

```
Assets/AI/Functions/HasTarget.asset
        ├── referenced by Zombie.asset
        └── referenced by Soldier.asset
```

### Creating one

Two ways:

- **From the node.** Select a Script Graph Variable, click its **Function** field and choose
  **Create new Function…**. It asks where to save and makes a Function that already declares the result
  type the port needs, so it is valid the moment it exists.
- **From the Project window.** **Create → ArcaneOnyx → Function Graph → Returning Bool** (or Float, Int,
  String, Void). Headless, `fn_create --path <asset>`.

A Function can live anywhere in the project. The commands that generate them follow one convention,
`<TreeFolder>/Functions/<Tree>.<Name>.asset`, beside the tree that asked for it. Nothing enforces it.

### Picking one

Click the **Function** field and you get a searchable dropdown of the Functions that can legally go there,
not every Function in the project. What "legally" means is decided by **the port the node feeds**, because
the node itself is untyped until a Function is assigned:

| The node's output feeds | The dropdown offers |
|---|---|
| a `bool` port | Functions whose `Result` can be read as a `bool` |
| a `Component` port | Functions returning `Component` **or a subclass**: a `Transform` Function is offered |
| an `int` port | a `float` Function too, because the port converts on read |
| several ports | only what fills all of them |
| nothing yet | everything that returns something |

The rule is exactly what the port accepts, the same test that decides whether you may draw the wire, so
anything offered demonstrably works. Entries are grouped **Predicates (bool)**, **Queries** and **Values**,
and each row names the inputs the node will owe the Function, `IsHurt — needs threshold`. A value row also
names what it returns, `PickCoverSpot : Vector3`. Two Functions sharing a name are qualified by their folder.

A Function that declares **no `Result`** is never offered, at any port: this node exists to read a value, and
a Function with no result has none to give.

When the list is empty, two different things look the same:

- **No Function returns the type yet.** Use **Create new Function…**.
- **The node feeds two ports no single value can satisfy**, `bool` and `Transform` say. No Function will fix
  that; disconnect one of the ports. The dropdown says which case you are in.

### The port takes the Function's type

Until a Function is assigned, the node's **Output** is `object` and may be wired to any port. The moment one
is assigned the output **retypes to the Function's result**, and from then on the canvas refuses wires that
do not fit: a `bool` Function's output cannot be dragged onto a `Transform` port.

If the node was **already wired** when the Function was assigned, a connection the new type cannot feed is
not deleted. It is demoted to an **invalid connection**, drawn red on the canvas and named by `bt_verify`.
Assigning a Function that fits brings the wire back without rewiring.

---

## Declared inputs are ports

Assigning a Function grows **one input port per input it declares**, typed the way the Function declared
it. That is how a call site passes an argument:

```
Float Literal 80 ──▶ threshold ┐
                               ├─ Script Graph Variable (IsLowHealth) ──▶ guard
```

Feed a port with a literal, a variable read, or any other value node, like every port in BH3. Because the
argument lives at the call site, one Function serves four agents that each want a different answer.

An input the Function gives a default is **optional**: the port carries that default and is safe to leave
unconnected. An input with no default is **required**, and leaving it empty shows a problem badge and is
reported by `bt_verify` before anything runs.

### The node keeps a copy, and the copy can drift

A node declares its ports from a **copy of the contract stored on the node**, never by reading the Function
live. This is the same bargain sub-tree nodes make, for the same reason: ports are rebuilt during
deserialization, and reading another asset that may not have loaded yet would silently drop wiring.

The cost is that the copy can fall behind the Function, and that is reported rather than hidden:

- **The node marks itself on the canvas**, red border and error icon, with the problem on hover. You see it
  when you open the tree, not when you press Play.
- **Refresh Ports** rebuilds the ports: a button in the node's inspector beside the reported drift, which
  also says how many changes it will apply; the node's right-click menu; or `fn_refresh_ports`. All three
  run the same repair.
- Refreshing removes ports the Function no longer declares, **and the connections feeding them**. Both the
  button and the command say which connections that costs.

The same applies to the result: a Function whose `Result` type changes after it was assigned does not
silently retype the port. The node reports it (`Result: this node declares its Output as Boolean, but the
Function now returns Single`) until you refresh.

See [Checking your tree](09-checking-your-tree.md) for the full list of what a node reports.

---

## Functions on a Script Graph node

A **Script Graph** node's four lifecycle slots, `OnAwake`, `OnEnter`, `OnUpdate` and `OnExit`, take Functions
through the same picker, with two differences:

- **No ports.** A lifecycle slot cannot declare ports, so a Function there cannot be passed arguments.
  `bt_verify` names one that has a required input.
- **`OnUpdate` must return an `ExecutionStatus`**, the node's verdict, and the picker offers only those. The
  other three run for their effects, so any Function is offered, including one with no result.

---

## Functions and guards

A reactive guard's condition is a natural Function: small, pure, and asked repeatedly. Two things from
[Guards](06-guards.md) matter more once the condition is shared:

- **A guard may only read.** A Function declares its purity, defaulting to pure, and that declaration is
  what callers rely on.
- **Watched keys decide whether a guard ever wakes.** They belong on the Function, beside the graph that
  does the reading, and a guard whose condition is a Function inherits them. Declare them in the Function's
  inspector or with `fn_set_metadata --watched_keys`.

---

## From the command line

| Command | What it does |
|---|---|
| `fn_create --path <asset>` | New Function, already runnable end to end |
| `fn_list [--folder <f>]` | Every Function with its flavour (predicate, query or value, from the result type), contract size and purity |
| `fn_describe --function <asset>` | Full contract, and why it cannot be evaluated if it cannot |
| `fn_set_metadata --function <asset> [--pure] [--watched_keys] [--description]` | Asset-level metadata |
| `fn_rename_output --function <asset> ...` | Rename the Function's output |
| `fn_refresh_ports --tree <asset> [--node <guid>]` | Rebuild a node's ports from its Function's contract, reporting the drift and any connection it cost |

`fn_describe` is the fastest way to find out why a Function is not returning what you expect: a missing
`Result` output is reported by name rather than failing at runtime.

---

## Try it

The reference project has three small scenes for this page: [FunctionPicker](https://github.com/platinio/bh3-development/tree/main/Assets/ArcaneOnyx/BH3Demos/FunctionPicker)
(what the dropdown offers at five different ports), [FunctionPorts](https://github.com/platinio/bh3-development/tree/main/Assets/ArcaneOnyx/BH3Demos/FunctionPorts)
(three agents, one Function, three arguments) and [FunctionGraphs](https://github.com/platinio/bh3-development/tree/main/Assets/ArcaneOnyx/BH3Demos/FunctionGraphs)
(a stale contract beside a fresh one).

## Next

- [Checking your tree](09-checking-your-tree.md)
- [Guards](06-guards.md#watched-keys-and-functions)
