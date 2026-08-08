# The Why Panel

Click a node, read why it did what it did.

---

## Overview

The behavior tree window already shows you what an agent is doing **right now** — the active branch lights
up as it runs. The Why panel answers the harder question: **why did that happen, and why not something
else.**

It does not guess. While an agent runs, BH3 records what its tree did — every node entered and exited,
every guard that changed its mind, every variable written and by whom. The Why panel reads that recording
back and assembles a sentence from it. Every clause you see is something that was actually recorded; where
the recording only supports a hunch, the panel says so instead of pretending.

> The usual reason to open it: a branch you expected to run didn't, or one you expected to keep running
> stopped. Those are the two questions it answers best.

---

## Opening it

**1. Press Play**

Recording is on by default in the editor. Nothing to enable.

**2. Open your tree**

Double-click the tree asset to open the behavior tree window, as usual.

**3. Open the Why panel**

It's in the window's sidebar, next to **Blackboard** and **Graph Inspector**.

**4. Pick the agent**

A dropdown at the top lists every agent in the scene currently recording. If you have one enemy, it's
already selected.

**5. Click a node on the canvas**

The panel explains that node. Click a different one and it re-explains.

> You can keep reading after pausing. The recording doesn't move while the editor is paused, which makes
> pausing the easiest way to study a moment that just went by.

---

## Reading the answer

A typical answer looks like this:

```
WhyDemo_Idle
WhyDemo_Agent

Aborted at tick 2274: guard 'not hasTarget' turned false while it was running.

  BECAUSE   Guard 'not hasTarget' aborted it, so everything under it stopped on the same tick.
  CONTEXT   It entered at tick 1773 and ran for 502 ticks.
  CONTEXT   It returned Failure to its parent on the same tick.
  EVIDENCE  The guard was last recorded turning false at tick 2274 (step 0).
  BECAUSE   It read Not -> True <- not hasTarget -> False
  BECAUSE   'hasTarget' changed False -> True at tick 2273 (step 0), written by WhyInspectorDemoSensor.

  GUARD CHAIN
    Not              →  True                       →
    └ not hasTarget  →  False              graph   →
```

Four parts, top to bottom.

### 1. The subject

The node's name, and underneath it the path to where it was running — `Zombie -> Combat -> Attack` reads
outermost first. That path matters when the same branch is reused: "Attack" alone is ambiguous the moment
two different parents run it.

### 2. The headline

One line, the whole answer. See [What the headlines mean](#what-the-headlines-mean).

### 3. The clauses

Each is labelled with how much weight to put on it:

| Label | What it means |
|---|---|
| **BECAUSE** | The thing that made it happen. This is the answer. |
| **EVIDENCE** | The recorded fact the answer rests on — a write, a guard flip, a returned status. Read this if you don't believe the BECAUSE. |
| **CONTEXT** | True and useful, but not part of the cause: how long it ran, how often it repeats. |
| **CAVEAT** | A limit on what the panel can claim. **Read these.** They mark the difference between a proven cause and a plausible one. |

### 4. The guard chain

What the guard was actually looking at, and what each value was, at the instant it changed its mind.

Behavior tree ports don't keep a history of their own, so this is captured on the spot when a guard flips.
It reads inward: `Not → True` because `not hasTarget → False`.

Rows fed by a Visual Scripting graph have a **graph** button — see
[The graph snapshot](#the-graph-snapshot).

---

## What the headlines mean

| Headline | What happened | Where to look next |
|---|---|---|
| **Running since tick N** | It's running now and hasn't finished. | Nothing wrong. The clauses list which guards are holding it open. |
| **Aborted at tick N: guard 'X' turned false** | It *was* running and a guard killed it mid-branch. | The guard chain, and the write that flipped it. This is usually a fact changing at a moment you didn't expect. |
| **Never entered at tick N: guard 'X' was false** | It never started. Different from an abort. | Why the guard was already false — often it has been false far longer than you think, and the clause says since when. |
| **Exited Success / Failure at tick N** | It ran to completion and returned a status. | If the topology can prove it, a clause names the child whose status caused it. |
| **Never ran: nothing about this node is in the recording** | No trace of it at all. | The clause naming the higher-priority sibling that won. If the recording is clipped, a CAVEAT says so — it may have run before the buffer's start. |
| **Guard is true / false, since tick N** | You clicked a guard rather than a behaviour node. | What it protects, and what last changed underneath it. |

> **"Aborted" and "never entered" are deliberately different sentences.** One ran and was killed; the other
> never started. They send you to different places, and conflating them wastes an afternoon.

### Oscillating

If a node keeps entering and aborting, a CONTEXT clause says so:

```
CONTEXT   Oscillating: entered 7 times and aborted 6 times in the last 60 ticks.
```

That is *selector thrash*, the classic behavior tree bug: a guard reading a value that flickers, so a branch
starts, dies, and restarts every few ticks. The fix is almost never in the branch — it's in whatever writes
the value.

---

## The buttons

| Button | What it does |
|---|---|
| **→** | Selects that node on the canvas. Appears only when the node is actually on the open canvas — a node inside a sub-tree instance has no button rather than a dead one. |
| **graph** | Opens the Visual Scripting graph snapshot for that chain row. |
| **Export…** | Saves the whole recording to a JSON file. |
| **Load…** | Opens a recording from a file and explains it with no agent running. |
| *agent dropdown* | Which agent you're looking at. |
| *call-site dropdown* | Appears **only** when the selected node ran in more than one place — a shared branch used by two parents is two different stories, and the canvas can't tell which one you meant. |

**Export and Load** are how you hand a bug to someone else. A recording plus the tree asset is a complete
account — they can read the same explanations you can, on a machine that never ran your scene.

---

## The graph snapshot

Most branches are guarded by a Visual Scripting graph, so "the guard returned false" often isn't enough.
The **graph** button opens that graph with the values that were on its wires at that tick.

- The window is banner-marked **SNAPSHOT — not live values** and drawn in a deliberately unfamiliar,
  ghosted style. That is on purpose: a snapshot that reads as live invites a conclusion about the present
  drawn from the past.
- Drag to pan, scroll to zoom.
- A wire reading **(not evaluated)** is a wire the flow never took — usually a fallback input. That is
  information about which way the logic went, not a gap in the recording.
- If you edited the graph after recording, wires the trace no longer matches are listed as **orphaned**
  rather than silently dropped.

---

## What it will not tell you

Worth knowing up front, so you don't read a limit as an answer.

**Collections and custom classes show as type names.** A value on a wire is stored as a short string, so a
`List<GameEntity>` reads as its type rather than its contents. Primitives, strings, `Vector3` and anything
deriving from `UnityEngine.Object` read fine. Inspecting the elements of a list is not supported yet.

**Wire values are editor-only.** The Visual Scripting values come from Unity's own debug data, which only
exists in the editor. In a development player build you still get the guard chain and the graph's final
output — just not the interior wires.

**A cause it cannot prove is marked as one it cannot prove.** When the panel can trace which variable a
guard reads, the write that changed it is a **BECAUSE**. When it can't — a guard whose key is computed at
runtime, say — the same write appears as **EVIDENCE** with a CAVEAT saying it is the last change before the
flip rather than a proven cause. Treat that difference seriously.

**Writes from inside a Visual Scripting graph are invisible.** BH3 records writes made by tree nodes and by
sensors. A `SetVariable` unit inside an embedded script graph cannot be intercepted, so it won't appear as
the writer.

**Ticks are not frames.** A tick is one update of *that agent's* tree, counted from when it started. Two
agents' tick numbers don't line up with each other.

**Clicking a tick doesn't jump anywhere yet.** The timeline scrubber isn't built. Tick numbers are there to
read and to correlate.

---

## Troubleshooting

**"Select a node on the canvas…"** — the panel has an agent but no node. Click one.

**"No live agent" / "Enter play mode to watch an agent"** — nothing is recording. Either you're not in play
mode, or `BehaviorTreeFlightRecorders.GloballyEnabled` was turned off.

**"This node has no recorded activity in the selected call site."** — the node ran somewhere else. Check the
call-site dropdown.

**"The recording is empty."** — recording is off for this agent, or it hasn't ticked yet.

**"The recording is clipped: N older events were dropped."** — the buffer holds the most recent ~2048
events, and the start of the story has scrolled away. Reproduce the bug closer to the moment you start
watching, or raise `BehaviorTreeFlightRecorders.DefaultCapacity` before agents spawn.

**No guard chain on an abort** — chains are captured only when a guard *changes* its answer, and they live
in a shorter buffer (64 per agent) than the events. A guard flipping very fast can push older chains out.
`BehaviorTreeFlightRecorders.TracingGloballyEnabled` turns chain capture off entirely, separately from
recording — worth checking if you never see one.

**A big scene is slowing down** — recording costs a little per agent, and guard chains cost more. Turn off
`TracingGloballyEnabled` first; it keeps the recording and drops only the chains. `GloballyEnabled` turns
everything off.

---

## Try it

`Assets/ArcaneOnyx/BH3Demos/WhyInspector/` is a small scene built to be read with this panel. It reproduces
the classic bug — a guard reading a variable a sensor flickers — so you can see every part of the panel
answer a question you already know the answer to. Its README walks through it in about two minutes.
