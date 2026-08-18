# Sub-Behavior Trees

Reusing a branch across agents, and passing arguments to it.

---

## What they're for

A Sub-Behavior Tree lets one tree run another. In an FPS, different enemies may behave differently but all
share the same shooting logic — so you build "shoot" once and reuse it everywhere, instead of copying nodes
between assets and maintaining every copy.

Use the **Gameplay → Run Behavior Tree Graph** node. Until you assign an asset it reports itself as missing:

![A Run Behavior Tree Graph node on the canvas reading "Missing Graph!", with the Graph Inspector showing an empty Behavior Tree Graph field](images/sub-tree-missing-graph.png)

Assign one in the node inspector and a simplified, read-only preview of that tree is drawn on the canvas:

![The same node with the Wood Collection tree assigned, drawing a boxed preview containing Entry, a Boolean Conditional Execution, a Sequence, and three leaf nodes](images/sub-tree-canvas-preview.png)

You can't edit the preview. Open the sub-tree asset to change it; the preview updates when you next enter
play mode.

Otherwise the node behaves like any other — it has an execution status and finishes at some point, unless
the sub-tree keeps itself alive with a `Repeater`. Nesting can go to any depth.

---

## Think of a branch as a function

This is the mental model that makes sub-trees pay off. A Zombie tree might have three branches — Idle, Chase,
Attack — and each one knows nothing about its caller or its siblings. The Idle branch does not check for
enemies and does not decide to transition to Attack; a guard on the branch decides when Idle stops being
valid. The glue lives only in the Zombie tree.

What makes that literally true rather than aspirational is **parameters**.

### Where the analogy stops

A function returns; a branch does not. It runs over many ticks, can end in `Success` or `Failure`, and can be
aborted mid-run by a guard — so there is no single moment at which it hands a value back.

**There are no output ports, deliberately.** Data a branch produces that needs to outlive it is *agent
state*: write it as an Object variable on the agent's `Variables` component and let whatever needs it read
from there.

> Values going **into** a branch are parameters. Facts about the agent are agent state. Keeping those two
> separate is the whole discipline.

---

## Parameters

A tree declares variables in three lists, editable in the Blackboard panel's **Graph** tab:

| List | Meaning | Becomes a port on the caller? |
|---|---|---|
| **Instance** | the tree's own internal variables | no |
| **Required** | a parameter the caller **must** pass | yes — with no default |
| **Optional** | a parameter with a default the branch carries itself | yes — with that default |

Every Required and Optional declaration becomes one input port on the `Run Behavior Tree Graph` node that
runs it. You feed those ports exactly like any other — a literal, a variable read, or a whole Visual
Scripting graph.

```
[Float Literal 3] ──▶ idleTime port on the Run Behavior Tree node
                              │
                              ▼  on enter, written into the branch's own scope
                     the branch's declarations
                              │
                              ▼
                 Get Variable "idleTime" inside Idle
```

The branch never learns a variable name from its caller, so the same Idle works on a Zombie, a Soldier or a
Draugr.

### Required or Optional?

One question: **is there a value that is right most of the time?**

- `idleTime = 3` usually is — make it **Optional** and every call site inherits it.
- `attackRange` is not — a Zombie's is 1.5 and an Archer's is 20. Make it **Required** and force the caller
  to say.

A Required port with nothing connected throws on entry, naming the parameter and the node that owed it.

### Arguments are re-read on every entry

Parameters are applied when the branch is **entered**, not once at load. A port fed by a variable read or a
script graph is therefore evaluated afresh each time the branch starts, exactly like an argument evaluated at
each call.

---

## Scopes: what a branch can see

Every running tree instance gets its own variable scope, and a sub-tree asset is instantiated **per call
site**. Two branches running side by side under a Parallel hold two different scopes and cannot collide.

- **Reads walk outward.** A branch looks in its own scope first, then its caller's, out to the root — so it
  still sees anything the agent supplied.
- **Writes stay local.** A `Set Variable` with Graph kind writes only to the branch that ran it. A branch
  cannot reach its caller's variables, and cannot leak scratch state into a sibling. That is what makes the
  same branch safe to reuse across unrelated agents.
- **Agent state is different.** Anything belonging to the whole agent — `lastKnownPosition`, a cooldown, an
  attack token — is an **Object** variable on the agent's `Variables` component. Shared on purpose, and
  visible in the inspector.

> A read of a variable nothing declares **throws**, naming the variable. If a branch must survive an agent
> that lacks it, use `Get BT Variable` with **Fallback** enabled.

Because the scope is per call site, using the same branch twice with different arguments keeps them apart.
Passing data by agreeing on a variable name is never necessary, and should not be done.

### How long an instance lives

The clone is created **lazily, on first use**, and released when the tree holding it is destroyed — so a
destroyed agent takes its whole tree of instances with it, at every depth. A branch the agent never entered
is never cloned at all, and costs nothing to tear down.

Nothing else can free these. The instance belongs to one call site on one agent, which is exactly what keeps
two uses of a branch from sharing state; the flip side is that the call site is the only thing that knows the
clone exists. That makes the count per call site **per agent, multiplied by nesting** — the reason it is
worth knowing that a wave-based scene spawning modular agents is the case that would feel it first.

---

## Refreshing the contract

The caller's ports are built from a **copy** of the sub-tree's contract, held on the calling node — not read
live from the other asset.

That's deliberate. Port definitions are rebuilt during deserialization, and a connection whose port key
doesn't exist yet is dropped silently. Reading across to another asset at that moment would lose wiring on
any load where the other asset hadn't resolved yet.

The cost is that the copy can go stale. So it is **reported rather than silently applied**: change a branch's
contract, and the caller tells you. Right-click the node and choose **Refresh Parameters**, which names what
changed and rebuilds the ports.

> Refreshing removes ports the branch no longer declares, and drops whatever was feeding them. The drift
> report says so before you do it.

---

## Two rules that bite

**A tree must not reach itself.** Recursion is checked at load and throws with the path that caused it, like
`A -> B -> A`. Reuse is judged per path — the same sub-tree used by two different branches is entirely
normal; the same asset appearing twice on *one* path is a cycle.

**Guards do not cross the boundary.** A Conditional Execution protects a node inside its own graph. To let a
caller interrupt a sub-tree, put the guard on the **Run Behavior Tree Graph node itself**, in the parent
tree.

---

## See also

- [Best Practices](best-practices.md#think-of-behavior-tree-branches-like-a-function-that-does-just-one-thing) — branch design
- [The Why Panel](why-panel.md) — a shared branch running in two places is two different stories; the panel has a call-site picker for exactly that
- [Authoring From Code](authoring-from-code.md) — declaring parameters and wiring call sites programmatically
