# Node problems — telling a designer what is wrong, on the canvas, before Play

**Status:** **IMPLEMENTED** on `feature/guard-watched-key-drift` (BH3 submodule), stacked on
`feature/function-graph-ports`. Written after the fact rather than before it: this began as one lint inside
spec 10 step 2b and grew into a general mechanism during review, which is the reason it has its own number.

Everything marked *verified* was read from the current source or measured in the Editor.

## Problem

BH3 could already detect most of what is wrong with a tree. It could not **tell the person who has to fix
it**.

`bt_verify` finds unset ports, contract drift, guard scheduling problems, purity mismatches and orphans —
and it is a CLI command, so its audience is an agent. A designer's surfaces are the canvas, the inspector
and the console. The result, stated by the tool owner: *"the reactive guard didn't show any problem."*

The canvas had exactly one health signal, and it pointed the wrong way in time. `DrawLastExecutionIcon`
draws a status icon from `LastExecutionStatus` (*verified*, `BehaviorTreeNodeElementWidget.cs:301`), which is
only set once something has **run**. So the editor reported problems strictly *after* they had bitten:

| | Unreal | BH3 before this |
|---|---|---|
| Open the asset | red node, error icon | nothing |
| Press Play | — | exception, then the icon appears |

Three specific failures were invisible at edit time, all of them silent at runtime in different ways:

1. **An unfed required port.** 22 shipped nodes declare a port with no default (*verified*, BREAK-2), and
   reading one throws `MissingValuePortInputException` on the first tick.
2. **A caller's contract copy drifting** from the Function or sub-tree it references.
3. **A guard's schedule disagreeing with its condition** — either the written-down keys lagging the
   declaration, or the declaration lagging what the graph actually reads.

## The design: any node can be wrong, so the base type answers for it

`BehaviorTreeNode.CollectProblems(List<NodeProblem>)` — a virtual with a do-nothing default.

**Not a capability interface, and the reasoning is the load-bearing part.** An interface earns its place by
separating things that have a capability from things that do not. Nothing lacks "can be wrong", so an
interface would separate nothing and the `is IReportsProblems` test at the call site was ceremony around a
question every node can answer. This was built as an interface first and corrected by the tool owner.

That is the opposite of `IDeclaresWatchedKeys`, which stays an interface precisely because it *is*
selective: most nodes declare no keys and the concept does not apply to them. The rule the guard walks state
— filter on capability, never on type — is about refusing to use a **type** as a proxy for a capability. It
is not a rule that every hook must be an interface, and applying it to something universal produces an
abstraction carrying no information.

Two extension points, for two different kinds of knowledge:

- **A node reports itself** by overriding `CollectProblems`. Found among the members an author already
  overrides, rather than requiring them to know an interface exists.
- **A rule outside the node** registers a provider with `NodeProblemCache.AddProvider`, so a lint does not
  have to become a property of the thing it inspects. `RemoveProvider` exists because providers are static
  and outlive whatever registered one.

`NodeProblem` carries a severity, a `Summary` (the diagnosis) and a `Fix` (the instruction), kept separate
because the two run together otherwise and the instruction is the half people need.

### What the base reports for every node

An unfed required value input — `!hasValidConnection && !hasDefaultValue && !safeToLeaveUnconnected`,
expressed once as `ValueInput.IsUnfedRequired` and asked by both the canvas and `bt_verify` so the two cannot
disagree.

This replaced a **name-based allowlist** in verification: `PortsSafeToLeaveUnset = { "Animator", "Target" }`.
Matching on name is matching on the wrong thing — whether an unconnected port is a defect depends on how the
owning node reads it — and it was wrong in both directions. `FaceTarget.Target` was excused for being called
`Target`; any future node naming a genuinely-required port `Target` would be excused too.

The knowledge now sits at the declaration: `ValueInput.SafeToLeaveUnconnected()`. Four ports needed it
(*verified*); every other `Animator` and `Target` already declares an inline default and was never at risk.
It is unconditional, unlike `NullMeansSelf()`, which silently does nothing when the type is not a component
holder — a marker that quietly fails to apply is worse than none, because the declaration then looks like it
handled the case.

### Freshness: borrowed, not invented

The canvas redraws constantly and answering "what is wrong with this node" allocates, so results are cached.
The invalidation signal is `FunctionEvaluator.Version`, which already exists to solve the identical problem
one layer down, plus a local counter for what the evaluator has no opinion about (sub-tree contracts, undo,
the refresh verbs).

Reusing it buys more than freshness: **the badge and the runtime go stale on the same signal**, so a node
drawn as healthy while evaluation would throw is unrepresentable rather than merely unlikely. Cost at draw
time is one integer comparison.

**Accepted lag, stated rather than discovered:** a Function edited in the graph window and not yet saved
bumps nothing. The evaluator lags identically, so the badge still tells the truth about what would happen on
Play. Closing it would mean re-hashing every referenced graph per frame — the walk `FunctionBindingPlan`
exists to delete.

## Guards: the two ways a schedule goes wrong

Both surfaced here, and they are not variants of one another.

**Stale trigger keys** (Unity-BH3#22). Seeding writes a Function's declared keys into the serialized
`GuardTrigger.Keys`, and `SeedMissingGuardTriggers` never revisits a trigger that already exists — rewriting
a schedule an author chose would be worse than the every-tick default it fixes. So the written list freezes.
**Runtime is correct**: inheritance unions the live declaration in, and the guard does wake. What is broken
is the asset as a description.

**An undeclared read.** Inheritance hands a guard the *declaration*, never a walk of a Function's units. So
adding a Get Variable unit to a Function does not make any guard reading it wake on that variable.
**Runtime is wrong**: the guard keeps its old schedule and the branch silently stops firing.

The second is worse and was the one the tool owner hit in practice. It is reported on the node holding the
Function *and* on the guard — one fact, two badges, kept deliberately because one Function feeding three
guards means three broken guards.

### Repairs, and which repair applies

Crucially these need different fixes, and offering the wrong one is worse than offering none:

| Symptom | Repair | Where |
|---|---|---|
| Trigger lags the declaration | **Refresh Watched Keys** | guard right-click, `bt_refresh_guard_keys` |
| Declaration lags the graph | **Declare 'x' on IsHurt** | guard right-click, Function inspector |
| Caller's ports lag the contract | **Refresh Ports** | node right-click, `fn_refresh_ports`, Function inspector |

A first version offered *Refresh Watched Keys* unconditionally, labelled **"(up to date)"** whenever the
trigger matched the declaration — including while the badge warned about an undeclared read, which a refresh
cannot fix because it copies the declaration missing the key. A menu contradicting the badge beside it is
worse than no menu, and it is now offered only when it would do something.

**Every repair adds and never removes.** A key nothing declares may be a deliberate hand-typed one naming a
fact no walk can see; seeded and hand-typed keys are byte-identical once written. Removing them would repeat
the mistake the seeder avoids. The one place removal is offered — the Function inspector — puts it behind a
confirmation that says so.

**Declaring on a Function is confirmed, because a Function is shared.** `WatchedKeyRepair.DeclareOnFunction`
counts and names the other trees reading it first. The edit is safe in the sense that matters — the graph
provably reads the key, so it makes the declaration true rather than imposing a preference, and it only ever
makes guards wake more often — but silently editing a shared asset from another asset's context menu is the
wrong habit to build.

## The Function inspector

`FunctionGraphAsset` had no custom inspector, so the metadata deciding whether a guard ever wakes was a bare
`List<string>` with no indication of what the graph reads, no check that a typed key was real, and no way to
reconcile them — while the derivation answering all three had shipped in the foundation pass and was used
only by `bt_verify`.

It now shows declared keys beside derived reads with repair in both directions, the contract, the binding
plan's error when a Function cannot be evaluated at all, purity, and — behind a button, because it loads
every tree in the project — its callers, with a bulk port refresh for the drifted ones.

**The contract is shown, not edited.** A Function's inputs and outputs *are* its graph's port definitions;
there is no second copy here to correct, and offering to edit them in two places is how they come to
disagree.

## Locked decisions (do not re-litigate)

1. Problems are a **virtual on `BehaviorTreeNode`**, not a capability interface. Universal concepts go on the
   base; selective ones stay interfaces.
2. Freshness rides `FunctionEvaluator.Version` so badge and runtime cannot disagree. No polling, no timer.
3. Whether an unconnected port is a defect is declared **at the port**, never inferred from its name.
4. Repairs add and never remove, except one removal behind an explicit confirmation.
5. Editing a shared Function from another asset's surface is confirmed and counted first.
6. An undeclared read is reported on both the Function-backed node and the guard. Duplication is accepted
   because both are genuinely broken.

## Known gaps

- **The node inspector says nothing.** Problems are visible only by hovering the badge, and the badge does
  not say *where* the fix is — three of the five repairs live in a context menu and nothing hints at that.
  Filed as **Unity-BH3#24**, with options; `NodeProblem.Fix` being prose rather than an action is the
  underlying reason a second surface cannot offer the repairs today.
- **A badge only helps on a canvas you have open.** A stale caller in a tree nobody opens is invisible until
  Play. There is no project-level problem list; `bt_verify` is that, for the wrong audience.
- **Fan-in through a condition is defensive, not exercised.** The walks are DAG-safe with a visited set, but
  BH3 ships no two-input boolean combinator, so no stock content builds a condition reaching two Functions.
- **One layout literal is still duplicated.** `ContractPortLayout` reads port spacing and chrome from the
  widgets' own styles, but the `70` header allowance is a bare literal inside
  `BehaviorTreeNodeElementWidget.CachePosition` with nothing to reference. Naming it there is a change to the
  widget layer.
- **`FaceTarget` declares a `Target` port nothing reads.** Marked safe so it is not reported as a missing
  connection — connecting it would change nothing — but the dead port is a real defect. Removing a public
  port is breaking, so it wants its own decision. The old name-based allowlist was hiding it.
- **No pixel verification.** The badge's data and draw path are verified; icon placement and border weight
  have not been eyeballed by the implementer, because `screenshot` captures only game and scene views.

## Tests

`NodeProblemTests` and `GuardWatchedKeyDriftTests` in `BH3/Test/EditMode/`. The ones worth keeping:

- Fixing a problem clears the badge — a stale badge is worse than no badge, because people trust it.
- An edit reaching **only** the evaluator is still picked up, which pins the shared-invalidation contract.
- `SafetyIsPerPort_NotPerPortName` — two ports both called `Target`, one safe and one not, which is exactly
  what the old rule could not express.
- A refresh never discards a hand-typed key.
- The tool owner's reproduction verbatim: a read added to a Function's graph, declared nowhere.

## Files touched

- **BH3 runtime:** `Nodes/Problems/NodeProblem.cs` (new), `BehaviorTreeNode.CollectProblems`,
  `ValueInput.SafeToLeaveUnconnected` / `IsUnfedRequired`, `IDeclaresWatchedKeys.UndeclaredReadKeys` and
  `DeclarationOwner`, `InheritedWatchedKeys.ResolveUndeclaredReads` / `ResolveIncompleteDeclarers`,
  `ReactiveGuard.RefreshWatchedKeys`, `ConditionalExecution.CollectProblems`.
- **BH3 editor:** `NodeProblemCache` (new), `WatchedKeyRepair` (new), `FunctionReferences` (new),
  `FunctionGraphAssetEditor` (new), badge drawing in `BehaviorTreeNodeElementWidget`, context menus in
  `ConditionalExecutionWidget` / `VisualScriptGraphVariableWidget` / `RunBehaviorTreeNodeElementWidget`,
  `bt_refresh_guard_keys`, verification's unset-port rule.
- **Docs:** `reactive-guards.md`, `functions.md`, spec 10's step 2b section.

> **Namespace trap.** `ArcaneOnyx.BehaviorTree.Inspector` as a namespace shadows Visual Scripting's
> `Inspector` **type** across the whole assembly and breaks ten unrelated files. Editor inspectors here live
> in plain `ArcaneOnyx.BehaviorTree`. Same collision class as the authoring skill's BREAK-1.
