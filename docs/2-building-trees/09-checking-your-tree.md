# Checking your tree

What a node tells you before you press Play, how to fix it in one click, and how to check every tree in the
project at once.

---

## Problem badges

A node that cannot work as authored marks itself on the canvas:

| Badge | Means |
|---|---|
| **Red** border and icon | An error. The node will throw, or quietly do the wrong thing |
| **Amber** | A warning. The node still does something sensible, but not what the asset says |

Hover the icon for the problem and its fix. Select the node and the **Graph Inspector** lists the same
problems under the node's fields, and where the fix is one click, a button that applies it.

| Problem | Example text | Fix |
|---|---|---|
| A required port is empty | `Required input 'threshold' has nothing connected` | Connect a value on the canvas |
| A Variables node has no kind or no key | `'Set Variable' has no variable store` | Pick a kind, fill the key |
| A sub-tree's parameters changed | `added: armour : Single` | **Refresh Parameters** |
| A Function's inputs or result changed | `Result: this node declares its Output as Boolean, but the Function now returns Single` | **Refresh Ports** |
| A Script Graph Variable has nothing to run | `has no Function assigned` | Assign a Function |
| A guard's key list lags its Function | names the missing key | **Refresh Watched Keys** |
| A Function reads a fact it never declared | `reads 'stamina' without declaring it` | **Declare 'stamina' on IsHurt** (confirms first) |
| A node's type no longer exists | `MISSING: PatrolToCover` | **Replace missing type…** or `[RenamedFrom]`. See [Renaming and deleting node types](../4-extending-with-csharp/03-renaming-and-deleting-node-types.md) |
| Layout disagrees with priority | amber priority badge | Drag the children into the order they run |

Badges recompute when the graph changes shape, so connecting a value clears the empty-port problem
immediately. The one lag is a Function edited in its own window and not yet saved; the runtime lags
identically, so the badge is still honest about what would happen if you pressed Play.

> Refreshing ports or parameters removes ports the asset no longer declares, **and the connections feeding
> them**. The button and the command both say which connections that costs before you confirm. There is no
> undo for that, so the report is the mitigation.

---

## `bt_verify`

`bt_verify` reloads each tree from disk, the way the machine will at Awake, and reports everything the
badges would plus the checks that need the whole tree:

```bash
unity command bt_verify --trees Assets/AI/Zombie.asset,Assets/AI/Soldier.asset
```

The `unity` command-line tool comes with the optional `com.unity.pipeline` package; it talks to the
running Editor, and the same commands are available to agents as tools. You never need it to *use* BH3:
everything it reports is also a badge in the window, and BH3 compiles without the package. It exists so a check can run in CI and so tools can
build trees. See [Command-line tools](../4-extending-with-csharp/05-command-line-tools.md).

From C#, `BehaviorTreeVerification.VerifyAndLog(paths)` is the same check. Because it reloads first, it is
honest even in the run that generated the asset, where in-memory values can hide a port that will be empty
after a reload.

What it checks:

- **Ports** left empty that the node will read
- **Missing node types**, with what state was preserved
- **Sub-tree contract drift** and **Function contract drift**
- **Invalid connections**: a wire whose types no longer fit after a Function's result changed
- **Layout ≠ priority**
- **Guards**: one that writes a variable; one with no trigger, so it re-checks every tick; one watching keys
  that nothing can mark dirty; one whose key list lags its Function; one set to take over whose owner is not
  a direct child of a Selector; an entry-only guard on a Selector branch
- **Watched keys written by Unity's stock Set Variable unit**, which cannot wake a guard
- **Functions** with no `Result`, a lifecycle-slot Function with a required input, or a purity or watched-key
  declaration that does not match what the graph does
- **Orphans**: nodes nothing reaches or reads; **unassigned sub-trees**; **recursion**

The full message catalogue is in [Command-line tools](../4-extending-with-csharp/05-command-line-tools.md#what-bt_verify-reports).

Run it in CI over every tree you ship. A sample test that does exactly that lives in
`Test/EditMode/SampleTreeVerificationTests.cs`.

---

## Finding broken trees across the project

**Window → Arcane Onyx → BH3 → Find Broken Behavior Tree Graphs** opens the **Broken BT Graphs** window.
**Scan All** lists every node in the project whose type no longer exists, grouped by the type it stands in
for, with a **Replace…** button per group. One rename leaves one placeholder per use site; grouped, it reads
as the single decision it actually is.

---

## Next

- [Best practices](10-best-practices.md)
- [Command-line tools](../4-extending-with-csharp/05-command-line-tools.md)
