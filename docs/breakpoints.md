# Breakpoints

Stop the editor the moment a node runs, a variable is written, or a guard changes its mind.

---

## Overview

The [Why panel](why-panel.md) answers *why did that happen* after the fact. Breakpoints answer a different
question: **catch it in the act.** You arm one, press Play, and the editor pauses on the exact frame the
thing you care about happened — with the tree frozen around it so you can look at everything else.

They come in three kinds:

| Kind | Stops when |
|---|---|
| **Node** | a node is entered, exits, is aborted, or is skipped |
| **Guard** | a guard changes its mind — either way, or only one way |
| **Variable** | a variable is written, optionally only with a value you name |

> Nothing to enable. Recording is on by default in the editor, and breakpoints ride on it.

---

## Arming one

### On a node

Right-click any node on the canvas → **Breakpoint**. Tick whichever moments you want:

| Moment | Stops when |
|---|---|
| **Break on Enter** | the node starts |
| **Break on Exit** | it stops, whatever it ended on |
| **Break on Abort** | a guard turned false mid-run and killed it |
| **Break on Skip** | a guard was false as it was about to start, so it never ran |

Abort and Skip are different events on purpose — *"it was interrupted"* and *"it never started"* are
different bugs. If you are chasing a branch that never runs, **Skip** is usually the one you want.

A node can have several moments armed at once; they are one breakpoint with a mask, not several.

### On a guard

Right-click the guard itself. Guards are pulled by their owner rather than entered, so they have no
lifecycle — only a change of mind:

| Option | Stops when |
|---|---|
| **Break when it changes** | either direction |
| **Break when it becomes true** | the branch just became allowed |
| **Break when it becomes false** | the one that aborts a running branch |

> Only *changes* are on offer, and that is what makes this usable. A guard is evaluated every tick, so
> "stop while it is false" would pause the editor on the frame you armed it and every frame after.

### On a variable

Right-click a row in the [Variable Watch](editor-guide.md) panel — you get the whole operator set,
pre-filled with the value in front of you. Or type a name into the **Breakpoints** panel, which is the only
way to name a variable that is not currently on screen.

---

## The Breakpoints panel

It lives in the window's sidebar, next to **Blackboard** and **Why**.

| | |
|---|---|
| **checkbox** | enable/disable without losing it — re-finding the node is the expensive part |
| **the label** | click it to open the operator, the expected value and **Break on hit #** |
| **hit count** | `3×` fired three times; `0/12` matched twelve times and fired none (see [Break on the Nth hit](#break-on-the-nth-hit)) |
| **→** | select the node on the canvas |
| **×** | remove it |
| **Clear All** | remove everything |
| **Breakpoints enabled** | master switch — keeps everything armed but stops any of it firing |

The row the editor stopped on is **filled amber and marked `▶`**, matching the ring drawn around the node on
the canvas. On the canvas, a **red dot** means armed; a **grey dot** means armed but switched off.

---

## What happens when one fires

The editor pauses, and four things line up on the moment it stopped:

1. **A line in the console** naming the breakpoint, what happened, and the tick.
2. **The node is selected** and ringed in amber on the canvas.
3. **The timeline parks on the hit tick**, so the canvas is ghosted to how the tree looked then.
4. **The Why panel and Variable Watch follow the playhead**, so all three describe the same instant.

Press **Play** again to carry on to the next one.

> Scrubbing the timeline while paused is safe — nothing can fire while the editor is paused. Stepping one
> frame *does* tick, and can hit.

---

## Comparing variable values

The operator compares the **live value**, not the text the recording shows for it. In practice that means it
does what you expect:

| You type | Matches |
|---|---|
| `ammo == 0` | the int `0` |
| `speed == 3.5` | the float `3.5` — **whatever your machine's decimal separator is**; `3,5` works too |
| `hasTarget == true` | the bool `true`, even though it renders as `True` |
| `hp < 5` | any number below five |
| `state contains Attack` | `AttackMelee`, `AttackRanged`, … |

### Ordering applies to numbers only

`<`, `<=`, `>`, `>=` need numbers. A behavior tree variable can hold a `Vector3`, a `GameObject` or a list,
and "less than" means nothing for any of them.

Rather than fall back to alphabetical order and give you a confident wrong answer, the breakpoint **says so
on its row**:

```
⚠ stance held Standing, which is not a number, so GreaterThan cannot apply.
```

The Variable Watch menu also greys out `<` and `>` when the value in front of you is not a number.

### `contains` on a number matches text

This one does fire, and is worth knowing about:

```
⚠ alertLevel is a number, and 'contains' matches its text — "1" also matches any number containing it.
```

So `alertLevel contains 1` stops on `1`, and also on `10`, `11` and `21`. That is what a substring test on a
number means. Use `==` when you want a value.

> A breakpoint that fires **more** often than you expect is harder to notice than one that never fires,
> which is why this one warns even though it works exactly as defined.

---

## Break on the Nth hit

Click a row and set **Break on hit #**. Every kind supports it, not just variables.

This is the answer to an **oscillating guard** — a branch that enters and aborts every few ticks, which is
the classic behavior tree bug. The interesting occurrence is rarely the first, and the alternative is
pressing Play twenty-nine times.

While it counts up, the row reads `0/12`: matched twelve times, fired none, on purpose.

---

## Which agent they fire for

A breakpoint is armed on a **node**, and that node belongs to a tree any number of agents may be running. So
breakpoints are narrowed to **the agent the debugger is already pointed at** — the same one the ghosted
canvas, the Why panel and the Variable Watch describe.

There is deliberately **no agent picker**. A second way to choose would let you select one agent in the
inspector and watch another in the debugger, and the failure would be at its worst here: the editor stops,
and every panel describes a different agent than the one that stopped it.

The panel says which agent it is watching, so a breakpoint that did not fire is never a mystery. If nothing
resolves an agent, breakpoints fire for **any** of them.

---

## Where they are kept

`UserSettings/BH3Breakpoints.json` — **per developer, and gitignored.**

Nobody commits their code editor's breakpoints, and the same reasoning applies here: a breakpoint is a
statement about what *you* are debugging this afternoon, not a property of the tree. Committing them would
pause your teammates' editors on nodes they were not looking at.

To hand someone a repro, copy them the file.

They survive domain reloads and editor restarts, and are keyed by node guid — so renaming a node, moving it
on the canvas, or reformatting the asset does not break them.

---

## Troubleshooting

**Nothing fires at all.** Breakpoints are matched inside the flight recorder, so they only work while the
agent is recording. If `BehaviorTreeFlightRecorders.GloballyEnabled` is off, or that agent's recorder is
disabled, nothing fires — the panel shows a warning when that applies. Check the **Breakpoints enabled**
switch too.

**It fires for the wrong agent, or not for the one I want.** Check the line at the top of the panel: it
names the agent being watched. Select the agent in the hierarchy, or open its tree from the machine rather
than from the asset.

**A `<` or `>` breakpoint never fires.** Read its row — if the value is not a number, it says so. Use `==`
or `contains` for text.

**A `contains` breakpoint fires too often.** If the variable is a number, it is matching text. Use `==`.

**A node breakpoint on a branch that never runs.** The branch is probably being *skipped*, not entered — arm
**Break on Skip** rather than **Break on Enter**, then read the Why panel for which guard refused it.

**The editor pauses somewhere I did not expect.** Look at the amber row in the Breakpoints panel; that is the
one that stopped it. The console line says the same thing.

**Breakpoints on a shared branch fire more often than expected.** A breakpoint is keyed by node guid, and a
sub-tree used at two call sites keeps the same guids — so one breakpoint covers both copies. The console line
and the Why panel's call-site path tell you which copy it was.

---

## Try it

`Assets/ArcaneOnyx/BH3Demos/Breakpoints/` is a scene built to be read with this panel. It arms **six**
breakpoints: four that fire — one per kind of matching — and **two that are deliberately wrong**, so you can
see what a breakpoint does when you ask it something that cannot be answered. Its README walks through it.

---

## See also

- [The Why Panel](why-panel.md) — once it stops, this tells you why
- [The Variable Watch](variable-watch.md) — and this tells you what the variables were when it stopped
- [The Graph Editor](editor-guide.md) — the window both panels live in
- [Best Practices](best-practices.md#prefer-conditional-executions-over-condition-nodes-for-interruptions) — guards, which most breakpoints end up being about
