# Renaming and Deleting Node Types

What happens to trees that used a node type after you rename, move or delete it — and how to get them back.

---

## The short version

| You did this | Do this |
|---|---|
| Renamed a node class, or moved it to another namespace or assembly | Put `[RenamedFrom("Old.Full.Name")]` on the class. Trees repair themselves on load. |
| Moved a whole namespace or assembly | `[assembly: RenamedNamespace("Old.Ns", "New.Ns")]` or `[assembly: RenamedAssembly(...)]`. Same effect. |
| Deleted a node type and want to put a different one in its place | Open the tree, select the red node, **Replace missing type…**. |
| Deleted a node type on purpose and the nodes should go | Delete them. Nothing else is needed. |
| A third-party node type disappeared and you cannot annotate it | **Replace missing type…**, then *Apply across the project*. |

**Nothing is lost while you decide.** A node whose type is gone becomes a placeholder that keeps everything
the node held — its values, its object references, its position, and every wire attached to it — until you
retarget it or delete it.

---

## What you see when a type goes missing

Open a tree that used the type and the node is still there, red, named after what it used to be:

```
MISSING: PatrolToCover
```

The Console says it once per tree, and the node's problem badge and `bt_verify` both name it. What none of
them do is lose your work: the node keeps its guid, so every transition still points at it, and the
Sequence or Selector above it still has a child in that slot.

At **runtime** a placeholder ticks as **Success**. It has no behaviour to run and nothing to fail, so a
Sequence walks straight past it and the branch quietly does less than it used to, with no exception. That
silence is why this page exists — a deleted node type does not announce itself at play time.

---

## The durable fix: `[RenamedFrom]`

If you own the code, say where the type went and stop thinking about it:

```csharp
using Unity.VisualScripting;

[RenamedFrom("ArcaneOnyx.BehaviorTree.PatrolToCover")]
[GraphCreateMenu("Navigation/Move To Cover")]
public class MoveToCover : GameplayNode
{
    // ...
}
```

Every tree that still names the old type restores itself the next time it loads — **including trees nobody
has opened**, trees restored from source control later, and trees in other projects that take BH3 as a
package. You do not have to find them.

The attribute may be repeated, so a type that has been renamed twice can carry both old names. Whole-scope
moves have their own form, declared once per assembly (`AssemblyInfo.cs` is the usual home):

```csharp
[assembly: RenamedNamespace("ArcaneOnyx.BehaviorTree.Old", "ArcaneOnyx.BehaviorTree.Navigation")]
[assembly: RenamedAssembly("ArcaneOnyx.Legacy", "ArcaneOnyx.BehaviorTree")]
```

A placeholder that was created *before* you added the attribute still heals — the placeholder remembers the
old name, and the attribute is consulted every time the tree loads.

> **Renamed a serialized field rather than the type?** `[RenamedFrom]` works on members too. A renamed
> **port key** is a different matter: port values and connections are matched by the literal key, so
> renaming a port key drops what was wired to it. Rename the C# property and keep `nameof` pointing at the
> old string if you need both.

---

## The manual fix: replace the type

For a type you cannot annotate — retired upstream, or genuinely being replaced by a *different* node rather
than renamed — pick the replacement yourself.

1. Select the red node. The inspector shows the former type and, under **Preserved state**, every value and
   object reference the node was holding.
2. **Replace missing type…**
3. Pick a type. Types whose short name matches the missing one are offered first under *(same name, moved)*
   — a type that changed namespace is nearly always the answer. Everything else is grouped by the same
   create-menu paths the canvas uses.
4. **Read the preview.** It is computed by actually performing the conversion on a throwaway node, so it
   cannot disagree with what Apply does:

```
Keeps 2 value(s) and 1 connection(s).
   kept: WaitSeconds, Radius
Dropped, because the replacement has nowhere to put them: Label
These connections survive only as invalid ports, for you to re-wire: Speed
```

5. Apply — to this node, to every node of that type **in this tree**, or **across the project**.

Undo works normally.

### What carries over

A member of the old node carries over when the replacement declares one with the **same name and a
compatible type**. A connection carries over when the replacement declares a **port with the same key**.
Everything else is dropped, and a connection with nowhere to land survives as an **invalid port** — visible
on the canvas, so you can see what was wired there instead of silently losing it. This is the same rule that
applies when you change a node's own `Definition`, so the result should look familiar.

### The other doors

The same operation, reachable four ways:

- the node inspector
- right-click the node on the canvas → **Replace missing type…**
- **Window ▸ Arcane Onyx ▸ BH3 ▸ Find Broken Behavior Tree Graphs** → **Scan All**, which lists every
  placeholder in the project **grouped by the type it stands in for**, with a *Replace…* per group. One
  rename leaves one placeholder per use site; grouped, it reads as the single decision it actually is.
- headless:

```bash
unity command bt_retarget_missing --former ArcaneOnyx.BehaviorTree.PatrolToCover --to ArcaneOnyx.BehaviorTree.MoveToCover
```

Add `--tree <path>` for one tree; omit it and every tree in the project is converted, saving only the ones
that changed.

---

## Retiring a node type on purpose

Deleting a node type is a supported way to retire one. If nothing should stand in its place:

1. Delete the script.
2. Run `bt_verify` (or open the Broken BT Graphs window) to find every tree that used it.
3. Delete the placeholders and rebuild those branches.

The placeholders are harmless in the meantime, apart from the silent-Success behaviour above — so do not
leave them in a shipping tree.

---

## Limits worth knowing

- **A placeholder written before BH3 preserved state** — one already sitting in a tree from an older version
  — has no former type and nothing kept. It says so, and can only be replaced from defaults or deleted.
- **A placeholder standing in for a node that referenced other nodes** may refuse to restore automatically.
  It stays a placeholder and the Console says why; retargeting it by hand reports the same reason.
- **Only nodes are recovered.** A missing type somewhere other than a node — a variable's value type, say —
  is still reported by the serializer but has no placeholder and nothing preserved.

---

## See also

- [Custom Nodes](custom-nodes.md) — writing the node types this page is about retiring
- [Ports and Wiring](ports-and-wiring.md) — port keys, and why a renamed key drops its value
- `Assets/ArcaneOnyx/BH3Demos/MissingTypeRecovery/` — a tree that is genuinely broken, to try this on
- `docs/design/12-missing-node-type-recovery.md` — how it works and why it is built that way
