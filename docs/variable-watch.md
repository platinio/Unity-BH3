# The Variable Watch

What every variable held at the moment you're looking at, and who put it there.

---

## Overview

The Blackboard panel shows what your agent's variables are **right now**. That's the wrong question when
something already went wrong — by the time you notice the zombie walked past you, `hasTarget` is back to
`false` and the evidence is gone.

The Variable Watch answers the other question: **what was this variable at tick 412, and which node or
sensor set it?** It is built from the same recording the Timeline and the Why panel read, so it never
guesses — every row is a write that actually happened.

> The usual reason to open it: a guard flipped and you want to know what changed underneath it. Scrub the
> Timeline to the moment, read the value here, click through to whoever wrote it.

---

## Opening it

**1. Press Play**

Recording is on by default in the editor. Nothing to enable.

**2. Open your tree**

Double-click the tree asset to open the behavior tree window.

**3. Open the Variable Watch**

It's in the window's **right** sidebar — the other panels live on the left, so you can read the watch, the
canvas and the Timeline at the same time. Drag the anchor button in the panel header if you'd rather have it
on the left; that choice is remembered.

**4. Check which agent it's watching**

The line at the top names it. There's no agent picker: the watch follows whatever the **Timeline** panel is
showing, so the table and the ghosted canvas can never describe two different agents. With the Timeline
closed, it falls back to the agent the rest of the debugger resolves — the one on the canvas, or your
hierarchy selection.

To switch agents, select the other one in the hierarchy.

---

## Reading the table

```
● live — tick 1030 — VariableWatchAgent
[ Filter variables by name…                        ]

agent  (Object)
  ▸ alertLevel = 2            VariableWatchDemoSensor   @1004  ⏱
  ▸ hasTarget = True          VariableWatchDemoSensor   @1022  ⏱
  ▸ lastEngageAlert = 2       Set Variable               @880  ⏱
VW_Demo_Engage #1  (Graph)
  ▸ alertAtEntry = 2          Set Variable              @1008  ⏱
VW_Demo_Engage #2  (Graph)
  ▸ alertAtEntry = 2          Set Variable               @881  ⏱
```

| Part | What it means |
|---|---|
| `agent (Object)` | A **store**. Variables are grouped by where they live, not by who wrote them. |
| `alertLevel = 2` | The value **at the playhead**, reconstructed from the writes up to that tick. |
| `VariableWatchDemoSensor` | Who wrote it last. Shown when the panel is wide enough; always in the history. |
| `@1004` | The tick of that last write. |
| `⏱` | Scrub the Timeline to that tick. |
| `▸` | Expand for this variable's write history. |

### The groups are stores, and that matters

| Group | What it is |
|---|---|
| **agent (Object)** | State belonging to the whole agent — the facts sensors publish, and anything a node writes with `BehaviorTreeVariableKind.Object`. |
| **`<TreeName>` (Graph)** | The root tree's own variables. |
| **`<Branch>` (Graph)** | One running branch's private scratch. |
| **scene / application / saved** | The wider Unity stores, listed last. |

Two things follow from grouping this way, and both are the point:

**A branch running twice gets two groups.** If your tree runs one sub-tree at two call sites, you'll see
`Attack #1` and `Attack #2` with their own values. They *are* separate — branch scratch belongs to the
running instance, so showing one row would be inventing a contradiction. The `#n` suffix only appears when
names actually collide.

**A node writing agent state is filed under `agent`, not under its branch.** A `Set Variable` node inside
`Attack` writing an `Object` variable is writing agent-wide state; the branch is where the write came *from*,
not where the value lives. The history still tells you which node did it.

---

## The write history

Expand a row (`▸`) for the last several writes, most recent first:

```
▸ hasTarget = False          VisionSensor   @1022  ⏱
    @1022  True → False   by VisionSensor          →  ⏱
    @988   False → True   by VisionSensor          →  ⏱
    @902   True → False   by VisionSensor          →  ⏱
    …14 older write(s) not shown
```

| Button | What it does |
|---|---|
| `⏱` | Scrub the Timeline to that write. The canvas ghosts to the same tick. |
| `→` | Go to the node that wrote it — **opening its asset if the node lives in another tree**, then selecting it. |

`→` is disabled when there's nothing to select — a sensor is a MonoBehaviour, not a node on any canvas.
Hover it and the tooltip says which case you're in.

The list is capped, and a row says how many older writes it isn't showing rather than pretending to be
complete. A variable churning every tick is worth noticing on its own.

---

## It answers for the playhead, not for now

This is the one rule to internalise. Park the Timeline at tick 300 and **every** value, tick and count in
the table becomes what it was at tick 300. The header turns orange and says so:

```
⏸ values at tick 300 — VariableWatchAgent
```

Which means the table and the ghosted canvas beside it always describe the same moment. Return to live and
it follows the newest tick again.

---

## What it will not show you

**Variables nobody has written.** The table is built from write events, so a variable that was declared in
the Blackboard but never assigned has no row. That's deliberate: mixing in live reads would put rows meaning
"right now" beside rows meaning "at tick 300". For current values, use the **Blackboard** panel or
**Tools → BH3 → Flight Recorder**.

**Writes that bypass the recorder.** A write only appears if it went through one of these:

| How the write was made | Recorded? | Attributed to |
|---|---|---|
| A node calling `SaveVariable` (including the built-in **Set Variable** node) | yes | the node — `→` selects it |
| The **Set Behavior Tree Variable** unit in a script graph | yes | the node that ran the graph |
| A sensor using the `AgentVariableWriter` component | yes | the component that called it, by name |
| `variables.declarations.Set(...)` called directly | **no** | — |
| Unity's own built-in **Set Variable** unit | **no** | — |

The last two write the value perfectly well; they're just invisible to the recording, and the guard reading
that variable will look like the bug. Prefer the first three.

### Writing a variable from a sensor

```csharp
[RequireComponent(typeof(AgentVariableWriter))]
public sealed class VisionSensor : MonoBehaviour
{
    private AgentVariableWriter variables;

    private void Awake() => variables = AgentVariableWriter.On(gameObject);

    private void Update() => variables.Write(this, "hasTarget", SeesEnemy());
}
```

`Write` returns whether the value actually changed — unchanged values are dropped, so a fact recomputed
every frame can't flood the recording. Passing `this` is what makes the watch say `VisionSensor` instead of
a bare guid, and it's why several components can share one writer and stay individually named.

---

## Troubleshooting

| What you see | Why |
|---|---|
| *"Enter play mode, or open a recording in the Timeline panel."* | Not playing. Recording is editor and dev-build only. |
| *"…has recorded nothing yet."* | The agent exists but its tree hasn't ticked. Check for a `Repeater` under `Entry` — without one the tree runs once and goes quiet. |
| *"…recorded no variable writes… N other agents are recording"* | You're pointed at the wrong agent. Select the one you want in the hierarchy. |
| *"No variable matches …"* | The filter is hiding everything. Clear it with the `✕`. |
| A row you expected is missing | Nothing has written it yet, or it's written by one of the two unrecorded paths above. |
| `→` is greyed out | The writer is a sensor, not a node — there is nothing on a canvas to select. |

---

## Try it

`Assets/ArcaneOnyx/BH3Demos/VariableWatch/` is a scene built to exercise every case above — a sensor
publishing three facts, one branch running at two call sites with its own scratch in each, and a node
writing agent state from inside a branch. Its README walks through what to look at.

---

## See also

- **[The Why Panel](why-panel.md)** — click a node, read why it did what it did. The watch tells you *what*
  a value was; the Why panel tells you what that *caused*.
- **[Best Practices](best-practices.md)** — why facts come from sensors rather than from branches.
