# Variables and scope

Where a variable lives, what a branch can see and write, and how to publish facts so guards and the
debugger can see them.

---

## The five kinds

Every **Get Variable**, **Set Variable** and **Remove Variable** node has a **Variable Kind** dropdown, and
the Blackboard panel has a tab per kind.

| Kind | Blackboard tab | Where the value lives | Use it for |
|---|---|---|---|
| **Graph** | Graph | The running tree instance, through the calling chain | A branch's own working values, and parameters passed to it |
| **Object** | Object | The agent's `Variables` component | Facts about the agent: `hasTarget`, `lastKnownPosition`, an attack cooldown. Shared by every branch on that agent, visible in the inspector, and the only kind a reactive guard can watch |
| **Scene** | Scene | Everything in the active scene | State bigger than one agent |
| **Application** | App | Shared across scenes, reset when the application quits | Session-wide state |
| **Saved** | Saved | Outlives the application | Persistent state. Unity object references are not supported |

These are Visual Scripting's own stores; the Blackboard panel and the `Variables` component the machine
requires are Visual Scripting's too. BH3 adds the scoping rules below and the recording.

A new node starts with **no kind chosen** and shows a problem badge until you pick one.

This page mentions two things explained later: [guards](06-guards.md), which can watch Object variables,
and [Functions](08-functions.md), the graphs a Script Graph Variable runs. Read on; the references are
one-liners here.

The **Key** is a port, so it can be typed inline, fed by a `Variable Key` literal, or computed by a graph.
An empty key is reported on the canvas and throws when the node runs, naming the node.

---

## Graph variables are scoped

Every running tree gets its own scope, and a **Run Behavior Tree Graph** node opens a child scope around the
sub-tree instance it runs. A sub-tree used at two call sites has two scopes that cannot collide.

| Rule | What it means |
|---|---|
| **Reads walk outward** | A `Get Variable` of kind Graph looks in the branch's own scope first, then its caller's, out to the root. A branch sees anything the agent passed down |
| **Writes stay local** | A `Set Variable` of kind Graph writes only the branch that ran it. A branch cannot reach its caller's variables, and cannot leak scratch state sideways into a sibling |
| **Agent state is Object** | Anything belonging to the whole agent is an Object variable. Shared on purpose |

Those three rules together are why you never move data between branches by agreeing on a variable name.
Values *into* a branch are parameters, passed at the call site; facts *about* the agent are Object
variables. Keeping the two apart is what lets the same Patrol run on a Zombie, a Soldier and an Archer. See
[Sub-trees](07-sub-trees.md).

A read of a variable nothing declares **throws**, naming the variable. If a branch must survive an agent
that lacks a fact, read it from a Function with **Get BT Variable** and enable its **Fallback**.

---

## Declaring variables in the Blackboard

The Blackboard's **Graph** tab has three lists for a tree asset:

| List | Meaning |
|---|---|
| **Instance** | The tree's own variables |
| **Required** | A parameter the caller **must** pass. Becomes a port with no default on every node that runs this tree |
| **Optional** | A parameter with a default the tree carries itself. Becomes a port with that default |

The **Object** tab edits the `Variables` component of the agent whose tree is open, so you can seed facts
and watch them change in Play mode.

---

## Writing facts from a sensor

A fact is an Object variable that something outside the tree keeps up to date. Sensors are the normal
source: a MonoBehaviour that looks around and publishes what it saw.

Write them through `AgentVariableWriter`. It does two things a plain `Variables` write does not: it bumps a
version so reactive guards watching that key wake up, and it records the write with the sensor's name so
the debugger can say who changed the value.

```csharp
using ArcaneOnyx.BehaviorTree;
using UnityEngine;

[RequireComponent(typeof(AgentVariableWriter))]
public sealed class VisionSensor : MonoBehaviour
{
    private AgentVariableWriter variables;

    private void Awake() => variables = AgentVariableWriter.On(gameObject);

    private void Update() => variables.Write(this, "hasTarget", SeesEnemy());

    private bool SeesEnemy() => false; // whatever your game does
}
```

`Write` returns whether the value actually changed. Unchanged values are dropped, so a fact recomputed every
frame cannot flood the recording or wake a guard for nothing. Passing `this` is what makes the debugger say
`VisionSensor` rather than a bare guid, and lets several components share one writer while staying
individually named.

### Which writes are visible

| How the write was made | Recorded | Wakes a reactive guard |
|---|---|---|
| The **Set Variable** tree node, kind Object | yes | yes |
| The **Set BT Variable** unit in a script graph, kind Object, on the agent | yes | yes |
| `AgentVariableWriter.Write` from C# | yes, named after the component | yes |
| Unity's stock **Set Variable** unit | **no** | **no** |
| `Variables.Object(go).Set(...)` from C# | **no** | **no** |
| Any of the above with a kind other than Object | as above | no; only Object variables can be watched |

The invisible paths write the value perfectly well. They are just invisible, so the guard reading that
variable will look like the bug. `bt_verify` reports a stock **Set Variable** unit writing a watched key when
the write is inside the tree's own graphs.

### Reading facts from a graph

Inside a Function, read with **Get BT Variable** rather than Unity's unit. It behaves the same way, and a
Function that reads agent facts should also **declare** them as watched keys, so a guard fed by that
Function knows what to wake on. See [Guards](06-guards.md#watched-keys-and-functions).

Both BT units offer the same kinds as the tree's variable nodes: Graph, Object, Scene, Application and
Saved. There is no **Flow** kind, because flow scratch is not tree state and nothing in a tree can read it.
For per-flow scratch inside a graph, use Unity's own **Get Variable** and **Set Variable**. A unit that has
no kind chosen shows an error on the canvas and throws, naming itself, if the graph runs anyway.

> **Set BT Variable with kind Graph** writes the *script graph's* own variables, not the behavior tree
> branch's scope, despite the name. To write a branch variable, use the **Set Variable** tree node.

---

## The `This` variable

The machine sets an Object variable named `This` to the agent's GameObject before the tree starts. Script
graphs use it to know which agent they are running for; you rarely need it directly.

---

## Next

- [Execution order](05-execution-order.md)
- [Guards](06-guards.md) — how facts drive branch selection
- [Variable watch](../3-debugging/04-variable-watch.md) — what every variable held at a tick, and who wrote it
