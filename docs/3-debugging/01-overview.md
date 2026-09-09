# Debugging: overview

What BH3 records while an agent runs, which panel answers which question, and what it costs.

---

## Everything is recorded

While an agent runs in the editor, a **flight recorder** attached to its machine logs, per tick:

- every node **entered** and **exited**, with the status it returned
- every node **skipped** at the door by a guard, **aborted** mid-run by its own guard, or **taken over** by a higher-priority sibling
- every **guard evaluation** that changed its answer, with the values on every wire feeding it
- every **variable write** that went through BH3, and who made it
- every **sub-tree** entered and left

Nothing needs enabling. Recording is on by default in the editor, and the debugging panels read the recording
rather than guessing from live state. When a panel cannot prove something from what was recorded, it says so
instead of pretending.

## Which panel answers which question

| You want to know | Open | Page |
|---|---|---|
| What the tree looked like three seconds ago, and how the branches came and went | **Timeline**, docked under the canvas | [Timeline](02-timeline.md) |
| Why a node ran, stopped, or never started | **Why**, in the sidebar | [Why panel](03-why-panel.md) |
| What a variable held at that moment, and which node or sensor set it | **Variable Watch**, in the right sidebar | [Variable watch](04-variable-watch.md) |
| Where the editor should pause the next time it happens | **Breakpoints**, in the sidebar | [Breakpoints](05-breakpoints.md) |

They are built to be read together. Park the Timeline at a tick, and the canvas ghosts to how the tree
looked then, the Why panel explains that tick, and the Variable Watch shows the values at that tick. A
breakpoint that fires parks all of them on the tick it fired.

The usual loop:

1. Something goes wrong. **Pause**, or let a breakpoint pause for you.
2. Click the node in question and read the **Why** panel's headline.
3. If the answer names a variable, open the **Variable Watch** and expand its history to see who wrote it.
4. Click any ⏱ button to scrub the **Timeline** to that tick and watch it happen on the canvas.

## Which agent you are looking at

There is deliberately **no agent picker** on any panel. All of them describe the same agent, which the
debugger resolves in this order:

| Situation | The agent is |
|---|---|
| You opened the tree from a **Behavior Tree Machine** component | That machine's agent, *shown on this canvas* |
| You opened the tree as a plain asset | The agent *selected in the hierarchy* |
| Nothing says, but only one agent is recording | *The only agent recording* |

If nothing resolves, the panels say **No live agent**. Select the agent in the hierarchy. A second picker
would let the canvas show one agent while a panel explained another, which is worse than asking you to
select one.

A saved recording opened from the Timeline replaces the live agent for every panel until you close it.

## Turning it off, and what it costs

Recording costs a little per agent per tick; guard-chain capture costs more. In a scene you are profiling,
turn it off:

- The **Rec** button at the left of the Timeline toolbar switches recording off for every agent. Off stays
  off for the rest of the editor session, including across Play, and each panel puts up a warning saying
  so. Click it again to resume.
- From code, `BehaviorTreeFlightRecorders.GloballyEnabled` is the same switch, and
  `BehaviorTreeFlightRecorders.TracingGloballyEnabled` turns off only the guard-chain capture while keeping
  the event recording.

Two buffers are involved, both per agent and both ring buffers:

| Buffer | Default size | When it matters |
|---|---|---|
| Events | 2048 | "The recording is clipped" means the start of the story scrolled away. Raise `BehaviorTreeFlightRecorders.DefaultCapacity` before agents spawn, or reproduce closer to the moment |
| Guard chains | 64 | A guard flipping very fast can push older chains out, so an abort may show no chain |

## The raw event log

**Tools → BH3 → Open Flight Recorder Window** opens a plain, per-agent event log: every recorded event in
order, with node guids resolved to names, an event-kind filter, a **Follow** toggle that tails the newest
tick, and **Copy JSON**. It also lists the agent's boolean facts as toggles you can flip by hand to poke a
guard, and a **Recording** / **Paused** toggle that pauses one agent's recorder while the others carry on.
It is the low-level view; the panels above are built on the same data and are usually the better first
stop.

## In a player build

The recorder compiles out of an ordinary build. Add the `BH3_DEV_TOOLS` scripting define to a development
build and every agent records exactly as it does in the editor.

What does not exist in a build is a way to get the recording off the device: the **Save** and **Copy JSON**
buttons are editor windows. If you need recordings from a device, call
`BehaviorTreeRecordingDump.ToJson(recorder)` yourself, from a trigger and an IO path of your own, and open
the file in the editor's Timeline afterwards.

## Reading and writing variables so the recorder can see them

A write only appears in the recording, and can only wake a reactive guard, if it went through BH3:

| How the write was made | Recorded | Wakes guards |
|---|---|---|
| The **Set Variable** tree node | yes | yes, for `Object` kind |
| The **Set BT Variable** unit inside a script graph | yes | yes, for `Object` kind on the agent |
| A sensor calling `AgentVariableWriter.Write` | yes, named after the component | yes |
| Unity's stock **Set Variable** unit | **no** | **no** |
| `Variables.Object(go).Set(...)` from C# | **no** | **no** |

The last two write the value perfectly well. They are just invisible, so the guard reading the variable will
look like the bug. See [Variables and scope](../2-building-trees/04-variables-and-scope.md#writing-facts-from-a-sensor).

## Ticks are not frames

A tick is one update of *that agent's* tree, counted from when it started. Two agents' tick numbers do not
line up with each other, and neither lines up with `Time.frameCount`.

---

## Next

- [Timeline](02-timeline.md)
- [Why panel](03-why-panel.md)
