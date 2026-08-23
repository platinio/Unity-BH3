# Missing node types — keeping what a deleted script left behind, and putting it back

**Status:** **IN REVIEW** 2026-08-23. Implemented on `feature/missing-type-recovery` in both repos:
BH3 submodule [platinio/Unity-BH3#74](https://github.com/platinio/Unity-BH3/pull/74) (`55a0ee4`), and the
superproject pointer bump plus demo in
[platinio/bh3-development#40](https://github.com/platinio/bh3-development/pull/40) (`59874d1`).
**#74 merges first** — #40 carries the pointer bump. Decisions locked with the tool owner before building (see *Locked
decisions*); what building it changed is in *Corrections to this spec, found by building it* at the end,
which is the section to read if the design above and the code disagree.

Everything marked *verified* was read from the current source (BH3 `main` at `b09f7c1`'s pointer, Unity VS
`com.unity.visualscripting@8bed5ad90189`).

## Problem

When a node type's C# script is deleted or renamed, the tree still opens: `BehaviorTreeGraphAsset` swaps the
unknown `$type` for `ArcaneOnyx.BehaviorTree.MissingType` before FullSerializer sees it, and spec 11 made the
stand-in report itself as an error on the canvas and in `bt_verify`. That is where it stops. The stand-in
offers **no way back** — not to the original type if the script returns, not to a renamed type, not to a
replacement the author picks. The only instruction it gives is "delete it and rebuild what it did".

For a renamed or moved type that is the wrong instruction, and it is wrong *N* times — once per node, per
tree, across the project. Retiring or renaming a node type is an accepted operation here (`RunScriptGraph`
was deleted, the Function-graph work in spec 10 moved types between assemblies), so this is a recurring cost,
not a one-off.

Three things make recovery impossible today, all *verified*:

1. **The original type name is thrown away.** `BehaviorTreeGraphAsset.OnBeforeDeserialize`
   (`Runtime/Graphs/BehaviorTreeGraphAsset.cs:49-85`) does a raw string replace of `"$type":"<old>"` with
   `"$type":"ArcaneOnyx.BehaviorTree.MissingType"`. `MissingType.formerType` and `formerValue` are declared
   `[Serialize]` but **nothing ever assigns them** — the only write in the repo is the declaration. The
   verifier's "formerly '{former}'" (`BehaviorTreeVerification.cs:588`) therefore always prints *unknown*.
2. **The node's own state is lost on the first re-save.** The rest of the node's JSON is left in place, and
   FullSerializer ignores keys the stand-in has no member for. `guid`, `positionRect`, `position` and
   `defaultValues` survive because `BehaviorTreeNode` owns them; every `[Serialize]` member of the deleted
   type (`RunBehaviorTreeGraphNode.behaviorTreeGraphAsset`, its `parameters` list, …) is dropped the next
   time the asset serializes. After that, even re-adding the script cannot bring the node back intact.
3. **The stand-in is a `BehaviorTreeNode`, not a VS `Unit`, so Unity VS's own recovery never runs.**
   `fsSerializer.GetDataType` (VS, `fsSerializer.cs:879-986`) has the whole mechanism — it only fires for
   dictionaries it recognises as units and only produces `Unity.VisualScripting.MissingType`, which cannot
   sit in a `BehaviorTreeGraph`. BH3's pre-rewrite exists precisely to dodge that, and in dodging it dodges
   the recovery too.

A fourth fact is not a blocker but shapes the design: the stand-in **returns `Success` when ticked**
(*verified*: `MissingType` overrides nothing, `BaseGraphNode.OnUpdate` defaults to `Success`,
`BaseGraphNode.cs:266`). A `Sequence` walks straight past it. That is a runtime-semantics question and is
left out of this spec deliberately — see *Known gaps*.

## What Unity VS actually does — and what it does not

Worth stating precisely, because the request was phrased as "the inspector Unity VS has", and that inspector
does not exist. VS's answer is two code-side mechanisms (*verified*):

- **Round-trip through the stand-in.** On load, an unresolvable unit `$type` becomes
  `Unity.VisualScripting.MissingType` with `formerType` = the old name and `formerValue` = the unit's entire
  JSON as a string. On a *later* load, if `formerType` resolves again, the serializer re-parses `formerValue`,
  copies the current position over it, and deserializes the real unit. Re-adding the script heals the graph
  with no user action. (`fsSerializer.cs:893-963`.)
- **Rename attributes.** `[RenamedFrom("Old.Full.Name")]` on a type, `[assembly: RenamedNamespace(old, new)]`,
  `[assembly: RenamedAssembly(old, new)]`. `RuntimeCodebase.TryDeserializeType` consults all three after the
  direct lookup fails (`RuntimeCodebase.cs:145-170, 248-287`). `[RenamedFrom]` on a *member* is also honoured
  by the reflected converter (`RenamedMembers`).

`MissingTypeUnitWidget` (VS Editor) only changes the node's title to the former type name. There is no
type picker. So:

> **BH3 already has the rename attributes.** `OnBeforeDeserialize` calls `RuntimeCodebase.TryDeserializeType`,
> so `[RenamedFrom("ArcaneOnyx.BehaviorTree.OldName")]` on a BH3 node type resolves today. It is
> undocumented and has no test. This spec documents and tests it; it does not reinvent it.

What is genuinely new here: (a) a stand-in that *keeps* the data, (b) the load-time round-trip, and (c) an
in-Editor **retarget** — pick a type, carry over what matches, apply to one / all in this tree / all in the
project. (c) is BH3-original.

## The design

### 1. The stand-in keeps everything

`MissingType` gains the members it should have had, and the loader fills them:

```csharp
public sealed class MissingType : BehaviorTreeNode
{
    [Serialize] public string formerType { get; private set; }                // full type name as written in the asset
    [Serialize] public string formerValue { get; private set; }               // the node's own JSON, as it was
    [Serialize] public List<UnityEngine.Object> formerObjects { get; private set; } // every Unity object the node referenced
}
```

**The loader moves from a string replace to an `fsData` walk.** `OnBeforeDeserialize` parses `_data.json`
with `fsJsonParser`, walks every dictionary, and for each one that

- has a `$type` that `RuntimeCodebase.TryDeserializeType` cannot resolve, **and**
- looks like a node — has `guid`, `positionRect` and `defaultValues` keys (the BH3 analogue of VS's
  `IsVisualScriptingUnit`; `position.x/y` is not used because BH3 nodes carry both `position` and
  `positionRect`, *verified* in `FR_Demo_Engage.asset`)

it rewrites the dictionary in place:

| key | becomes |
|---|---|
| `formerType` | the old `$type` string, verbatim |
| `formerObjects` | a list of the `{"$content": i}` Unity-object references found anywhere inside the node dict, each copied as-is (still indexes into the asset's `objectReferences`) |
| `formerValue` | the node dict printed with `fsJsonPrinter.CompressedJson`, **after** rewriting every Unity-object reference inside it to its *ordinal in `formerObjects`* and **removing `$id`** |
| `$type` | `ArcaneOnyx.BehaviorTree.MissingType` |
| `guid`, `positionRect`, `position`, `defaultValues` | untouched — the base class still owns them, so the stand-in keeps its place and its inline values |

Then `fsJsonPrinter.CompressedJson` back into a new `SerializationData` with the **same** `objectReferences`
list, exactly as today.

Two of those rules exist because of traps VS's version has (*verified* in `fsSerializer.cs:898, 932`):

- **Object references go stale.** A Unity object inside a node serializes as an *index* into the asset's
  `objectReferences` table (`UnityObjectConverter.TrySerialize`, VS). VS stores the raw JSON with those
  indexes; the moment the asset re-serializes without a live object holding that reference, the table is
  rebuilt without it and the stored index points at the wrong object or nothing. `formerObjects` is a real
  `List<UnityEngine.Object>` on the stand-in, so the asset keeps referencing them and they survive every
  re-save; `formerValue` indexes into *that* list, which is stable by construction.
- **`$id` is per-serialization.** FullSerializer assigns `$id` fresh on every serialize; a snapshot taken on
  load carries the number from *that* document. VS pastes the snapshot back wholesale, `$id` included, and
  any `$ref` elsewhere in the current document that pointed at the stand-in's *current* `$id` now dangles.
  (In a BH3 asset the node's first occurrence is embedded inside a transition's `source`/`destination`; the
  rest are `$ref`s — *verified*.) `formerValue` therefore never contains `$id`; restore always keeps the
  current dictionary's `$id`.

Dictionaries with an unresolvable `$type` that do **not** look like a node (a deleted custom variable type
inside `declarations`, say) keep today's treatment — the blanket rewrite — so nothing outside node recovery
changes behaviour in this pass. Noted in *Known gaps*.

The `Debug.Log("type X is missing updating it")` at load becomes one `Debug.LogWarning` per asset listing
the former types found, so the console says something a person can act on.

### 2. Auto-restore on load (Unity VS parity)

Same walk, opposite direction. A dictionary whose `$type` is `MissingType` and whose `formerType` **now
resolves** to a type assignable to `BehaviorTreeNode` is rebuilt from `formerValue`:

1. parse `formerValue`; set its `$type` to the *resolved* type's serialized name (so a `[RenamedFrom]`
   resolution writes the new name, not the old one);
2. map every relative object index back to the absolute `$content` the current document holds in the
   stand-in's `formerObjects` array;
3. overwrite `positionRect`/`position` with the stand-in's current ones (the node may have been moved while
   a stand-in), keep the current `$id`, keep `guid` (identical by construction — assert it);
4. replace the dictionary.

If `formerType` resolves but is **not** a `BehaviorTreeNode`, the stand-in stays and its problem text says
so ("'X' exists again but is not a behavior tree node"). If it resolves to a type whose `Definition` throws,
`Define` already catches that and the node arrives with no ports — existing behaviour, nothing new to handle.

Effect: re-adding a deleted script heals every tree on its next load; adding `[RenamedFrom]` to the new
class heals every tree on its next load; neither needs a human. This is the durable path, and the retarget
below is for the cases where the code does not (or cannot) say where the type went.

### 3. Retarget: one operation, four doors

One Editor-side operation, `MissingTypeRecovery.Retarget(asset, missingNode, Type target)`, returning the
replacement node. Everything user-facing is a door onto it.

**How it converts (in memory, on the open graph — no asset reload, so open windows keep their
`GraphReference`):**

1. `NodePreservation.Preserve(missing)` — records inline defaults and every port connection *by key*. The
   stand-in has no defined ports, so its connections live on invalid ports, which still carry the key
   (*verified*, `PortConnection.cs:146-155`).
2. Build the replacement: `new SerializationData(formerValueWith$typeSetTo(target), missing.formerObjects)
   .Deserialize()` — the public VS API that takes its own object table, which is exactly why `formerValue`
   indexes into `formerObjects`. Members whose name and type match the target carry over for free;
   FullSerializer drops the rest. If `formerValue` is empty (a stand-in saved by the *old* loader), fall
   back to `Activator`-style construction with the stand-in's `defaultValues`.
3. `replacement.guid = missing.guid`, position copied, `Define()`.
4. `preservation.RestoreTo(replacement)` — defaults where the key exists and the type is assignable,
   connections where the key exists, **invalid ports for the rest** — the same rule a node already gets
   when its own `Definition` renames a port, so the author sees a familiar result rather than a new one.
5. Swap in the graph: remove the stand-in, add the replacement, re-point every `BehaviorTreeTransition`
   whose `source`/`destination` was the stand-in (`SetupTransition` is public; `source`/`destination` setters
   are `internal` to GraphCore), re-point guards/comments if they key on the node.
6. Undo: **never `UndoUtility.RecordEditedObject` from here.** The type picker is a popup callback and the
   batch paths run outside any canvas frame; `RecordEditedObject` silently no-ops there (see memory
   `vs-undo-outside-edit-bracket`, and the reason the context-menu verbs defer through `canvas.delayCall`).
   Use `Undo.RegisterCompleteObjectUndo(asset, "Retarget missing node")` before and `EditorUtility.SetDirty`
   after, with the asset taken from `BehaviorTreeCanvas.GetBehaviorTreeGraphAsset()` or passed in.

**Carry-over preview before Apply.** With a target selected, the door shows what survives and what does
not, computed by instantiating the target once (`Define()` on a throwaway) and comparing keys: *kept*
(same key, assignable), *dropped* (key absent on the target) and *connections that become invalid*. Cheap,
and it is the difference between a retarget that feels safe and one that feels like a gamble.

**Type picker.** Unity's `UnityEditor.IMGUI.Controls.AdvancedDropdown`, the same one the Function picker in
`BTScriptGraphVariableInspector` uses (*verified*, `:51, :397`). Candidates: every concrete
`BehaviorTreeNode` subclass that carries `[GraphCreateMenu]` (the canvas's own add-node set), plus, if the
former type's *short* name exists anywhere under a different namespace/assembly, that type is pinned at
the top as "Same name, moved" — the overwhelmingly common case. Ranked after that by short-name similarity
to the former type, then by the canvas's menu path.

**Batch scopes.** After one retarget, the door offers:

- *Apply to the N other nodes with this former type in this tree* — same operation in a loop, one undo
  group.
- *Apply across the project* — `AssetDatabase.FindAssets("t:BehaviorTreeGraphAsset")`, load each,
  retarget every stand-in whose `formerType` matches, `Save` only the assets that changed, report counts.
  Runs with a progress bar; assets that are open in a window are handled in memory like the single-node
  path, the rest through load → retarget → save.

**The four doors:**

| Door | What it shows / does |
|---|---|
| **Node inspector** (`[Inspector(typeof(MissingType))]`, a sibling of `BehaviorTreeNodeInspector`) | Former type (full name, copyable). Preserved state, read-only: each `defaultValues` entry, each top-level key of `formerValue` with its value, each `formerObjects` entry as an object field. Then: the type picker, the carry-over preview, **Apply**, the two batch buttons, and **Copy `[RenamedFrom]`** — puts `[RenamedFrom("<formerType>")]` on the clipboard for the author to paste onto the target class, which is the durable fix for code they own. |
| **Canvas context menu** (`BehaviorTreeNodeElementWidget.contextOptions` when the node is a `MissingType`) | "Replace missing type…" opens the picker; on pick, runs through `canvas.delayCall` like the other verbs. Same batch prompts after. |
| **Broken BT Graphs window** (`BrokenBehaviorTreeGraphFinder`) | A new section, *Missing node types*, grouped by former type with the count of nodes and the list of assets. Each group has the picker and an Apply that is the project-wide batch. Today the window inspects only the VS `IUnit`s embedded inside nodes (*verified*, `:344-362`), so this is an addition, not a change to what it already reports. |
| **`bt_retarget_missing`** | `--tree <path> --former <typeName> --to <typeName> [--all-trees]` in `BehaviorTreeAuthoring`; reports converted / kept / dropped keys per node. `bt_verify`'s message for a stand-in changes from "delete it and rebuild" to name the former type and point at this command and the inspector. `bt_describe_tree` lists a stand-in as `MissingType (formerly X)`. |

### 4. Canvas and problems

- `BehaviorTreeNodeElementWidget` titles a stand-in with the **former type's short name** and subtitles it
  *Script missing* (VS's `MissingTypeUnitWidget` does this; BH3's shows "MISSING TYPE!" with no clue which
  one). Tooltip carries the full name.
- `MissingType.CollectProblems` keeps the `Error`, and its `Fix` becomes the instruction that is now true:
  *"Re-add the script or add [RenamedFrom(...)] to its replacement, or pick a replacement type in the
  inspector."* If `formerType` resolves but is not a node, a distinct message (see §2).
- `Description` stops saying "remove this node and create a new one".

### 5. `[RenamedFrom]` — document and test, do not reimplement

`docs/` gets a short "Renaming or moving a node type" page: put `[RenamedFrom("Old.Full.Name")]` on the
new class (multiple allowed), `[assembly: RenamedNamespace]` / `[RenamedAssembly]` for wholesale moves,
with the note that BH3 resolves these through VS's `RuntimeCodebase` at load and that a stand-in created
before the attribute existed auto-restores on the next load (§2). One test pins the behaviour so a future
change to the loader cannot silently drop it.

## Locked decisions (do not re-litigate)

Each taken with the tool owner on 2026-08-23.

1. **Auto-restore on load is in.** The stand-in is a round-trip container, not a tombstone. Manual
   retarget is for the cases the code cannot express.
2. **Batch scope is this tree *and* project-wide.** A renamed type is never one node. The project-wide
   path reuses the Broken BT Graphs window rather than growing a second finder.
3. **Nothing persists after a manual retarget.** No project remap asset. `[RenamedFrom]` on the target
   class is the durable mechanism for code you own, the inspector hands you the attribute line, and the
   batch paths cover what exists now. A remap table would be a second rename mechanism beside one that
   already works; it is listed under *Known gaps* for the case it is actually for (types you cannot
   annotate).
4. **All four doors ship** — inspector, canvas context menu, finder window, `bt_retarget_missing`. One
   operation underneath; the doors are thin.
5. **Carry-over is by key and by member name, with the `NodePreservation` rule.** Not a user-editable
   mapping table; that is a different feature and the preview makes the automatic rule legible.
6. **`formerValue` indexes Unity objects into `formerObjects` and never carries `$id`.** These are the two
   VS traps; they are design constraints, not implementation details.
7. **Non-node dictionaries keep the legacy rewrite.** Out of scope here; flagged below.

## Known gaps

- **Runtime status of a stand-in.** It returns `Success` today (*verified*). Whether a deleted node should
  read as a pass is a semantics decision with blast radius (a `Sequence` silently continues; a `Selector`
  silently stops), and this spec does not change it. Worth its own small decision once recovery exists and
  the stand-in is rarer.
- **Stand-ins saved by the old loader** have no `formerType`/`formerValue`. They retarget through the
  fallback (defaults only) and their inspector says the former type is unknown. Nothing can recover what was
  already dropped.
- **Non-node types** with an unresolvable `$type` (variable values, declaration types) still get the blanket
  `MissingType` rewrite, which produces a node object in a non-node slot. Harmless so far, but it is the
  wrong stand-in for those and the right one (leave `$type` alone and let VS warn) should be its own pass
  once this one proves the `fsData` walk.
- **A remap table for types you cannot annotate** (a third-party package node retired upstream). Decision 3
  keeps it out; the project-wide batch covers existing assets, and the gap only bites if the old type keeps
  arriving in new assets.
- **`[RenamedFrom]` on members** (a renamed `[Serialize]` field or port key) is honoured by the converter
  for fields; port *keys* live in `defaultValues` and connections, which `NodePreservation` matches by
  literal key. A renamed port key still drops its value. Same limitation as today, unchanged by this spec.
- **Sub-graphs inside nodes.** Embedded VS script graphs inside a node are VS units; their own missing
  types go through VS's mechanism and the existing finder, not this one.

## Tests

EditMode, in `Test/EditMode/`:

- **Loader keeps the name and the state.** Write an asset containing a node of a type that does not exist
  (author the JSON by saving a real node, then editing its `$type` on disk), load it: the stand-in has
  `formerType` == the name, `formerValue` parses, `formerObjects` holds the referenced assets, `guid`
  and position match, transitions still reach it. Save and reload: all of it is still there.
- **Auto-restore.** Save a `RunBehaviorTreeGraphNode` pointing at a sub-tree asset with one parameter set;
  rewrite `$type` on disk to a bogus name, load (stand-in), save, then rewrite `formerType` on disk back to
  the real name, load: a `RunBehaviorTreeGraphNode` with the same guid, the same sub-tree asset, the
  parameter value and its transitions. The object-reference case is the point of this test.
- **`[RenamedFrom]` resolves at load.** A test-only node type with `[RenamedFrom("ArcaneOnyx.Tests.Gone")]`;
  an asset whose `$type` is `ArcaneOnyx.Tests.Gone` loads as that type, no stand-in. Pins what already works.
- **Retarget carries by key.** Stand-in from a type with ports `A`, `B` and an inline value on `A`;
  retarget to a type with `A`, `C`: `A` keeps its value and its connection, `B`'s connection becomes an
  invalid port, `C` is default, guid preserved, transitions re-pointed, the stand-in gone from `Nodes`.
- **Batch across the project.** Two assets, three stand-ins of the same former type, one of another:
  `--all-trees` converts three, leaves one, saves exactly two assets, the report counts agree.
- **Verify names the former type and the fix.** Extends `VerifyNamesANodeWhoseTypeNoLongerExists`: the
  finding contains the former type name and `bt_retarget_missing`.
- **Undo.** After a single retarget in an open canvas, `Undo.PerformUndo()` brings the stand-in back with
  its `formerValue` intact and the asset is dirty before and clean after.

Canvas rendering is checked by hand with the GrabPixels route from memory `bt-canvas-visual-verification`,
not by a test.

## Files touched (expected)

All inside the BH3 submodule unless stated.

| Area | Files |
|---|---|
| Stand-in | `Runtime/Nodes/Special/MissingType.cs` — `formerObjects`, problem text, description |
| Loader | `Runtime/Graphs/BehaviorTreeGraphAsset.cs` — `OnBeforeDeserialize` becomes the `fsData` walk (§1) + restore (§2); consider lifting the walk into `Runtime/Preservation/MissingTypeSerialization.cs` so it is testable without an asset |
| Retarget | `Editor/Authoring/MissingTypeRecovery.cs` (new) — `Retarget`, `Preview`, `RetargetAll(asset)`, `RetargetAcrossProject` |
| Doors | `Editor/Inspector/MissingTypeInspector.cs` (new); `Editor/Widgets/BehaviorTreeNodeElementWidget.cs` (title/subtitle, context option); `Editor/BrokenBehaviorTreeGraphFinder.cs` (section); `Editor/Authoring/BehaviorTreeAuthoring.cs` (`RetargetMissingCommand`), `BehaviorTreeVerification.cs` (message) |
| Command wiring | `[CliCommand("bt_retarget_missing", …)]` on the new method in `BehaviorTreeAuthoring.cs` — the same attribute `bt_verify` uses (*verified*, `:681`), inside the submodule. Confirm the MCP server surfaces a new attribute-declared command without a server-side list before relying on it from an agent. |
| Docs | this file; `docs/` page on renaming node types; `docs/` entry for `bt_retarget_missing` |
| Tests | `Test/EditMode/MissingTypeRecoveryTests.cs` (new); `BehaviorTreeVerificationTests.cs` (extend) |

## Acceptance criteria

1. Delete a node script, open a tree that used it: the canvas shows the former type name on the node, the
   console names it once, `bt_verify` names it and the fix.
2. Re-add the script (or add `[RenamedFrom]` to its replacement), reopen: the node is back, with its
   values, object references, position and connections, and no stand-in remains.
3. With the script still gone, select the node: the inspector shows what it was and what it held, offers a
   ranked list of replacement types with the moved-same-name type first, previews what carries over, and
   Apply converts it in place with undo.
4. "Apply to all in this tree" and "Apply across the project" convert every node with that former type,
   save only what changed, and report the counts; the finder window shows the same groups and can do the
   same.
5. `bt_retarget_missing` does the project-wide conversion headless and its report matches the window's.
6. Saving a stand-in and loading it again loses nothing — `formerType`, `formerValue`, `formerObjects` all
   round-trip, and the object references still resolve after the asset has been re-serialized.

---

# Implementation — landed 2026-08-23

Branch `feature/missing-type-recovery` in the BH3 submodule; the demo lives in the superproject on a branch
of the same name. All 16 new tests pass. The project EditMode suite is 746/747, the one failure being the
pre-existing `TpsArchitectureTests.All_concrete_PositionEvaluators_are_marked_Serializable` that was already
failing before this change; PlayMode is 81/84, the three failures being TPS PlayMode tests whose assembly
does not reference BH3 at all and whose own fixture documents the `GameEntity` exceptions they trip on.

## Corrections to this spec, found by building it

Three, and the first is load-bearing enough that the design above is wrong without it.

### 1. A Unity object reference is a bare integer, so it cannot be found in the document

The spec says `formerObjects` is built from "the Unity-object references found anywhere inside the node
dict". **There is no such thing to find.** `UnityObjectConverter.TrySerialize` emits `new fsData(index)` — a
plain integer, indistinguishable in the raw document from any other integer (*verified*: a sub-tree
reference is stored as `"behaviorTreeGraphAsset":5`). And the type is gone, so nothing can be reflected over
to learn which members were objects. That is the whole problem in one line.

**What was built instead:** `formerValue` keeps the node's JSON **verbatim**, absolute indices and all, and
`formerObjects` is a snapshot of the asset's **entire object table** at the moment of conversion. The
invariant then holds trivially rather than by construction — the indices in `formerValue` are valid indices
into `formerObjects` *because `formerObjects` is the array they already indexed*. No detection, no
rewriting, nothing to get wrong.

The cost is that a placeholder holds every object the asset referenced rather than only the ones its node
used. That is bounded (single digits in practice), only exists while the tree is broken, and is
self-cleaning — deleting or retargeting the placeholder drops the references with it.

`formerObjects` is assigned from C# in `OnAfterDeserialize`, not written into the JSON, so the shape of a
serialized `List<UnityEngine.Object>` never had to be authored by hand. It is assigned **only** to
placeholders created by that same load (`formerValue != null && formerObjects == null`) — one that survived
a save carries its own table, and the current asset's table is a different one with different indices.

### 2. Restoring is not document surgery, which deletes the `$id` problem entirely

The spec has the loader rebuild a node by splicing `formerValue` back into the document, and worries about
`$id` collisions and remapping object indices while doing it. **None of that was necessary.** The restore
runs *after* deserialization, from the live placeholder:

```csharp
new SerializationData(formerValueWithTypeSwapped, placeholder.formerObjects).Deserialize()
```

`SerializationData` takes its own object table, which is exactly why `formerValue` indexes into
`formerObjects`. Nothing is spliced, so there is no `$id` to collide and no index to remap — both traps the
spec spends paragraphs on simply do not arise. Only the **forward** direction is document surgery, because
there the type genuinely does not exist and nothing can deserialize it.

The direct consequence is the thing the spec most wanted: auto-restore and manual retarget are **one code
path** (`MissingTypeRecovery.Rebuild` + `Replace`), not two implementations of one rule.

### 3. The canvas title needed no widget change

`BehaviorTreeNodeElementWidget` already draws `element.NodeName` (*verified*, `:278`), so overriding
`NodeName` on `MissingType` was the whole change. The spec's planned widget edit was dropped.

## Decisions taken while implementing

- **The node heuristic is `guid` + `positionRect` + `defaultValues`.** These are the members
  `BehaviorTreeNode` declares, so the test is exactly "is this a node", which is exactly the set
  `MissingType` can stand in for. A transition carries `guid` but neither of the others. Guards are nodes
  (`ConditionalExecution : GameplayNode`) and so are covered, which differs from the blanket rewrite only in
  that they are now recoverable too.
- **A cheap string scan gates the parser.** A healthy asset — every asset in the project, nearly always —
  never reaches `fsJsonParser`. Re-printing the document is safe *because* it only happens when something
  was converted, and that copy is consumed immediately by the deserializer; the next save regenerates the
  document from the live objects.
- **`RestoreResolvableNodes` scans without allocating before doing anything.** It runs on every load of
  every tree, including the per-agent `Instantiate` that shared-tree instancing pays (spec 07), so a healthy
  tree must not pay a `List` for being healthy.
- **A placeholder with nothing preserved is not auto-restored**, only offered for manual retarget. Bringing
  it back stripped of everything the node held is worse than leaving it visibly broken.
- **The rich UI is one window, not three.** `MissingTypeRetargetWindow` owns the picker, the preview and the
  batch buttons; the inspector shows evidence plus a button into it, and the canvas menu and the finder open
  the same window. Three copies of a preview is three chances for one to disagree with what Apply does.
- **The preview is computed by performing the conversion on a throwaway node**, not by predicting it.
  Predicting means a second implementation of the carry-over rule, and a preview that can disagree with the
  thing it previews is worse than no preview.
- **Undo is `Undo.RegisterCompleteObjectUndo` + `SetDirty` on the named asset.** `UndoUtility.RecordEditedObject`
  is literally those two calls against `LudiqEditorUtility.editedObject`, which is only populated inside a
  canvas draw frame — and every caller here (dropdown callback, context menu, project-wide batch) runs
  outside one, where it silently records nothing.

## The trap that cost the most time

`OnBeforeDeserialize` originally logged the asset's `name`. Reading `UnityEngine.Object.name` during
serialization throws `GetName is not allowed to be called during serialization`, and because the throw
escapes into Unity's deserialization it **takes the whole asset down with it** — every node silently absent.
The symptom looks exactly like a broken converter and nothing like a logging mistake: the first spike
reported zero placeholders *and* zero surviving nodes, while the converter was provably correct when called
directly. The asset is passed as the log's context object instead, which also makes the entry click through
to the tree.

## Verified in the Editor, not only by tests

- A `WaitTime` broken on disk becomes a placeholder with its guid, position, transition and port connection
  intact; saved; reloaded; healed back to `WaitTime` with the `Time` wire reconnected.
- A `RunBehaviorTreeGraphNode` pointing at a sub-tree asset, through the same cycle including a save that
  rebuilds the object table, comes back pointing at **the same sub-tree**. This is the case Unity VS's own
  implementation gets wrong.
- The demo tree, broken on disk and committed: the preview keeps `WaitSeconds` and the `Radius` connection
  and drops `Label` and `Speed`.
- Mutation-tested: replacing `CaptureFormerObjects(convertedObjectReferences)` with `(null)` fails exactly
  `ARestoredNodeKeepsItsUnityObjectReferences` and nothing else.

## Known gaps, restated against what shipped

Everything under *Known gaps* above still stands, plus:

- **A `formerValue` containing a `$ref` to something outside the node** cannot be deserialized standalone.
  The rebuild is wrapped in try/catch, the placeholder is left in place, and the reason is logged and
  repeated in the node's problem text — but no node type in the project does this today, so the path is
  reasoned about rather than exercised by a test.
- **The runtime status of a placeholder is still `Success`.** Deliberately unchanged; the demo's HUD now
  makes it visible, which is the argument for deciding it separately rather than the fix.
- **`Test/EditMode/MissingTypeRecoveryTests.cs` declares three node doubles** (`RetargetSourceNode`,
  `RetargetTargetNode`, `RenamedNode`) carrying `[GraphCreateMenu]`, so they appear in the canvas create
  menu. Harmless — the test assembly is Editor-only and gated on `UNITY_INCLUDE_TESTS` — but worth knowing
  before wondering why "Test/Retarget Source" is in the menu.
- **No PlayMode baseline was captured** for this change (the baseline run was disrupted by concurrent
  recompiles). EditMode is compared against a real baseline; PlayMode was run clean afterwards instead.

## Files touched (actual)

| Area | Files |
|---|---|
| Placeholder | `Runtime/Nodes/Special/MissingType.cs` |
| Loader | `Runtime/Preservation/MissingTypeSerialization.cs` (new), `Runtime/Graphs/BehaviorTreeGraphAsset.cs` |
| Recovery | `Runtime/Preservation/MissingTypeRecovery.cs` (new) |
| Editor operation | `Editor/Authoring/MissingTypeRetarget.cs` (new) |
| Doors | `Editor/Window/MissingTypeRetargetWindow.cs` (new), `Editor/Inspector/MissingTypeInspector.cs` (new), `Editor/Widgets/BehaviorTreeNodeElementWidget.cs`, `Editor/BrokenBehaviorTreeGraphFinder.cs`, `Editor/Authoring/BehaviorTreeAuthoring.cs`, `Editor/Authoring/BehaviorTreeVerification.cs` |
| Tests | `Test/EditMode/MissingTypeRecoveryTests.cs` (new, 16 tests) |
| Docs | `docs/renaming-and-deleting-nodes.md` (new), `docs/SUMMARY.md`, `docs/custom-nodes.md`, this file |
| Superproject | `Assets/ArcaneOnyx/BH3Demos/MissingTypeRecovery/` (new), `.claude/skills/behavior-trees-unity-authoring/SKILL.md` |

## The self-review round

Five findings, four acted on. Worth recording which, because one of them was a real defect that every test
written up to that point walked straight past.

### Fixed — the recovered node snapped back to where it used to be

`Rebuild` took the node's position from `formerValue`, which froze it at the moment the type went missing.
A placeholder stays draggable, and dragging the red node somewhere readable is the *first* thing anyone does
with a broken tree — so recovering it silently threw that away. The fallback path
(`RebuildFromDefaults`) had always copied the placeholder's live position; the JSON path, written later,
did not.

Now both do, and `guid` is reasserted from the placeholder rather than trusted from the document, because a
mismatch there presents as a node that loses all its wiring for no visible reason.

Pinned by `MovingAPlaceholderBeforeRecoveringItKeepsWhereItWasMovedTo`, and mutation-tested: removing
`rebuilt.Position = placeholder.Position` fails that test and only that test.

**Why every earlier test missed it:** they all asserted the position was *preserved* — and it was, from the
original. None of them moved the placeholder first, so nothing distinguished "kept the node's position" from
"kept a stale copy of it". The bug lived exactly in the gap between those two.

### Fixed — the undo test the spec asked for did not exist

The spec lists it, and it is the one behaviour built specifically to route around a documented project trap
(`UndoUtility.RecordEditedObject` silently recording nothing outside a canvas draw frame). Every caller here
runs outside one. `UndoingARetargetBringsBackThePlaceholderAndWhatItHeld` now pins it: clean asset, apply,
assert dirty, `Undo.PerformUndo()`, assert the placeholder and its `formerValue` are back.

### Fixed — `formerValue` no longer carries `$id`

Locked decision #6 said it never would, and nothing enforced it. Today's architecture cannot trip over a
stale id — the node is rebuilt as a standalone document rather than spliced back into this one — but that is
a property of how `Rebuild` happens to work, not of the data. An id is a position in the document that
produced it, and `formerValue` outlives that document by design.

### Fixed — one definition of "which members does every node have"

`MissingTypeInspector` and `MissingTypeRetarget` each carried their own list. They are read side by side by
someone deciding whether to commit — the inspector says what was preserved, the preview says what carries
over — so two lists that can drift is two surfaces that can disagree about the same node. Now
`MissingTypeSerialization.IsBaseNodeMember` and nothing else.

### Not fixed — `formerObjects` holding the whole object table

The reviewer read the spec above rather than the corrections below it (it was written before this section
existed) and proposed scoping the capture to "the `{"$content": i}` entries inside the node dict". **There
are no such entries to scope to** — that is correction 1: a Unity object reference is a bare integer, and
the type is gone, so nothing can say which integers were objects.

The cost the finding names is real and was accepted deliberately: a placeholder references every object its
asset referenced, so N placeholders in one load cost N × table size rather than N × their own subset. It is
bounded (single digits in practice), exists only while the tree is broken, is self-cleaning, and is the
price of an invariant that holds by construction instead of by detection. The alternative considered and
rejected was one shared table on the asset, which trades this for a second lifetime to manage and a field
that exists only for a broken state.
