# Ports and wiring

How a node gets its data: the idea BH3 is built around, and the rules for feeding a port.

---

## Why ports exist

Ports separate a node's **logic** from where its **data** comes from. A node reads a port. It does not care
whether the value arrived from the Blackboard, from a component, from another node, or from a literal you
typed in. You connect a source on the canvas and the node just reads it, with no glue script keeping a
blackboard in sync.

That is the whole trick, and most of the rest of these guides follows from it.

---

## Feeding a port

Every input port has a pin on the left of its node. You can feed it four ways:

| Source | When |
|---|---|
| **Leave it alone** | The port has a default. `Cooldown`'s `Duration` is 5 unless you say otherwise |
| **A Literal node** | You want a fixed value, set per tree, without touching code. Also the only way to fill a port that has no default |
| **Another node's output** | `Generate Random NavMesh Position` → `Set Nav Agent Position`, or `Get Variable` reading the blackboard |
| **A Script Graph Variable** | A whole Visual Scripting graph computes the value. See below |

### What "compatible" means

The canvas lets you wire an output into an input whenever the two types **convert**, not only when they are
identical:

| You wired | Into a port of | What happens |
|---|---|---|
| `int` | `float` | widened |
| `float` | `int` | rounded, the way `Convert.ChangeType` rounds |
| `GameObject` | `Transform` | resolved through `GetComponent` |
| `Vector3` | `Vector2` | the `z` is dropped |
| `Transform` | `Object` | passed through unchanged |

A wire the canvas refuses to draw is one the runtime could not read either. A wire it draws is always
readable.

### Ports with no default

A port declared without a default **must** be connected. Reading it unconnected throws, naming the port, and
the node shows a red problem badge until you fix it. That is deliberate: a missing wire is an authoring
mistake worth reporting, not something to paper over with zero. The [Node reference](02-node-reference.md#ports-you-must-connect)
lists the shipped ports that behave this way.

---

## Feeding a port from Visual Scripting

Ports become genuinely powerful combined with Visual Scripting. A **Script Graph Variable** node runs a
[Function](08-functions.md), a Visual Scripting graph saved as its own asset, and offers the result on an
output port that wires into any behavior tree port of a matching type.

Here is the loop on the tree from [Your first tree](../1-start-here/02-your-first-tree.md),
`Entry → Repeater → Sequence → (Wait, Add Force)`:

![A behavior tree: Entry connects to a Repeater, then a Sequence whose children are a Wait node and an Add Force node with Target and Force ports](../images/entry-repeater-sequence-example.png)

**1. Create the node.** Right-click → **Visual Scripting → Script Graph Variable**. Select it, click its
**Function** field, and choose **Create new Function…**. Pick where to save it; it opens for editing.

> The picker only lists Functions whose result fits the port you are feeding, so wire the node's output
> first if you want a shorter list. The screenshot below predates Functions and shows an **Open Graph**
> button; the field is now called **Function**.

![The Graph Inspector for a selected Script Graph Variable node, with its Open Graph button highlighted, and the node on the canvas showing a single Output port](../images/script-graph-variable-open-graph.png)

**2. Build a graph that returns the value.** Here, finding a GameObject by name and getting its Rigidbody.
The value leaves the graph through an output named **Result** on the graph's **Output** unit. Creating the
Function from the node declares that output for you, typed to fit the port; a Function made another way
needs it added by hand, and is not offered anywhere until it has one.

![A Visual Scripting flow graph: ScriptGraphInput Enter, into Game Object Find, into Component Get Component, into ScriptGraphOutput with Exit and Result ports](../images/script-graph-variable-flow-graph.png)

**3. Connect the output** to the **Target** input of the Add Force node.

![The Script Graph Variable node's Output port connected by a green wire to the Target input of an Add Force node](../images/script-graph-variable-feeding-port.png)

**4. Press Play.** ([short video](https://youtu.be/6owI5ZZtUJg))

The Add Force node has no idea where that Rigidbody came from, and the Function can fetch a value from
anywhere in the game. Neither needs to know about the other.

Once a Function is assigned, its declared **inputs become ports** on the node, so a call site can pass
arguments, and its result type **retypes the output port**, so a `bool` Function's output cannot be dragged
onto a `Transform` port. [Functions](08-functions.md) has the details.

### Where BH3 reaches Visual Scripting

| Node | What it runs |
|---|---|
| **Script Graph Variable** | One Function that produces a value for a port |
| **Script Graph** | Four Functions, one per lifecycle hook (`OnAwake`, `OnEnter`, `OnUpdate`, `OnExit`). The `OnUpdate` Function must return an `ExecutionStatus` |

### Reading and writing variables from a graph

BH3 ships two units, the Visual Scripting word for a node in a script graph, under **BH3 → Variables** in
the finder that opens when you right-click a script graph. Their full titles are **Set Behavior Tree
Variable** and **Get Behavior Tree Variable**; the short names below are what the finder shows:

| Unit | Use it because |
|---|---|
| **Set BT Variable** | It reports the write. Unity's built-in **Set Variable** cannot be observed, so a value written with it never wakes a reactive guard and has no recorded writer in the debugger. **Always prefer this one** |
| **Get BT Variable** | Reads the way Unity's **Get Variable** does, but with the tree's own kinds: no **Flow**, see [Variables and scope](04-variables-and-scope.md). Kept beside the other so both live in one menu. Enable **Fallback** for anything a reusable branch reads: without it, reading a name the agent does not declare throws |

If they do not show up in the finder, run **Tools → BH3 → Install**
once.

---

## Ports in C#

This is what a port looks like from inside a node. [Custom nodes](../4-extending-with-csharp/01-custom-nodes.md)
covers writing one end to end.

```csharp
using ArcaneOnyx.BehaviorTree;
using ArcaneOnyx.GraphCore;
using Unity.VisualScripting;
using UnityEngine;
using ValueInput = ArcaneOnyx.BehaviorTree.ValueInput;   // both namespaces declare one; see Custom nodes

[GraphCreateMenu("Custom/Move To Target")]
public class MoveToTargetNode : GameplayNode
{
    [DoNotSerialize] public ValueInput Target { get; private set; }
    [DoNotSerialize] public ValueInput Speed { get; private set; }

    public override string NodeName => "Move To Target";

    protected override void Definition()
    {
        base.Definition();
        Target = ValueInput<Transform>(nameof(Target));      // no default: must be wired
        Speed  = ValueInput<float>(nameof(Speed), 3.5f);     // default: safe to leave alone
    }

    public override ExecutionStatus OnUpdate(BTContext ctx)
    {
        var target = ctx.GetValue<Transform>(Target);
        var speed  = ctx.GetValue<float>(Speed);

        if (target == null) return ExecutionStatus.Failure;

        ctx.transform.position = Vector3.MoveTowards(
            ctx.transform.position, target.position, speed * Time.deltaTime);

        return Vector3.Distance(ctx.transform.position, target.position) < 0.1f
            ? ExecutionStatus.Success
            : ExecutionStatus.Running;
    }
}
```

Two ports, and each has several equally valid sources on the canvas:

- **Target**: a `Get Variable` reading an `Object` variable, so the agent chases whatever is stored there; a
  `Find Game Object`; or a `This Transform` literal.
- **Speed**: nothing, and the `3.5` applies; a `Float` literal, to tune it per tree; or a `Get Variable`
  reading `movementSpeed`, tuned per agent.

The node's code is identical in every case. That is the point.

### Rules for declaring ports

- **Declare ports in `Definition()`, calling `base.Definition()` first.**
- **A port key must be unique across the node.** `nameof` gives you that, as long as a subclass does not reuse
  a name its base class already took. If it does, the second declaration throws and the node arrives with
  **no ports at all**.
- **The second argument to `ValueInput<T>` is the default.** Leave it off and the port must be wired.
- **A `ValueOutput` takes a delegate**, called on demand every time a connected node reads the port. The
  value is never cached.

```csharp
Distance = ValueOutput<float>(nameof(Distance), () => ComputeDistance());
```

### Rules for reading ports

- **Read with `ctx.GetValue<T>(port)`** (or `port.GetValue<T>()`), at the type the port declares. It applies
  the same conversions the canvas allows, so `Speed` reads correctly whether the wire carries a `float`, an
  `int`, or a Visual Scripting value of unknown type.
- **Never cast an untyped `GetValue()`.** `(float) Speed.GetValue()` compiles and then throws
  `InvalidCastException` on every converting connection, because unboxing does not convert.
- **`GetValueOrDefault<T>()`** converts identically but answers `default` instead of throwing when the value
  cannot be used, for readers where "nothing here" is a normal answer. It still throws for an unconnected,
  defaultless port.
- **`ctx.GetComponent<T>(port)`** reads a port that names a GameObject or component and falls back to the
  agent's own component when nothing is connected. That is how the Navigation nodes' `Target` means "me".

---

## Next

- [Variables and scope](04-variables-and-scope.md) — the other place a value can come from
- [Functions](08-functions.md) — the graphs a Script Graph Variable runs
- [Custom nodes](../4-extending-with-csharp/01-custom-nodes.md)
