# Authoring From Code

Generating behavior trees and Visual Scripting graphs programmatically — for tooling, bulk edits, and
generated content.

Most people never need this page. Read it if you are writing an editor tool that builds trees, migrating a
lot of assets at once, or driving BH3 from a script.

---

## Read this first — five things that will break your output

These are not style advice. Each one produces a tree that looks correct and fails at runtime.

### 1. Five type names exist in both namespaces

`Literal`, `ValueInput`, `ValueOutput`, `ValueInputDefinition` and `ValueOutputDefinition` are defined by
**both** `ArcaneOnyx.BehaviorTree` and `Unity.VisualScripting`. Inside the `ArcaneOnyx.BehaviorTree`
namespace the BH3 one silently wins, even with `using Unity.VisualScripting;` present.

When authoring script graphs, fully qualify:

```csharp
var literal = new Unity.VisualScripting.Literal(typeof(bool), true);
var output  = new Unity.VisualScripting.ValueOutputDefinition { key = "Result", type = typeof(float) };
```

Only those five collide. `ControlInputDefinition` and `ControlOutputDefinition` are unambiguous.

### 2. An unset `ValueInput` throws — it does not default

`GetValue()` throws when a port has neither a connection nor a declared default. Plenty of shipped nodes
declare ports with no default:

```csharp
Time     = ValueInput<float>(nameof(Time));      // no default -> MUST be connected
Duration = ValueInput<float>(nameof(Duration), 5f);  // has a default -> safe to leave alone
```

Don't keep a list of these in your head — ask. `bt_list_nodes` flags every defaultless port on every node,
derived from the nodes themselves; `bt_list_nodes --type WaitTime` narrows it to one. The
[Node Reference](node-reference.md#ports-you-must-connect) lists the common ones.

**Exception:** ports read through `GetComponent<T>(port)` never call `GetValue()`, so they safely fall back
to the machine's own GameObject.

### 3. Inline values don't survive a reload on a defaultless port

This is the one that costs an afternoon, because it works right up until the asset reloads.

A node's default values live in a plain dictionary that is cleared and rebuilt by `Definition()` every time
the node deserializes. **Only keys `Definition()` itself puts back survive.** Setting a port that was
declared bare writes a key nothing will re-create:

```csharp
// WaitTime.Time is declared with no default
waitNode.Time.SetDefaultValue(1.5f);   // holds in memory, GONE after save + reload -> throws on first tick

// PlayAnimationAndWait.TriggerName is declared with a default
playNode.TriggerName.SetDefaultValue("Attack");   // the key exists, so this persists
```

Nothing warns you. A generator that sets a bare port and dumps in the same run reports the value correctly;
reopen the project and the same port reads `(unset)`.

**So: to give a bare port a value, connect a literal node** — `FloatLiteral`, `Vector3Literal`,
`StringLiteral`, `BoolLiteral`. That is what a designer does on the canvas anyway.

`BehaviorTreeAuthoring.SetValue` decides which of the two is correct for you, so using it makes this trap
unreachable.

### 4. Child execution order is canvas X, not insertion order

When the graph awakes, every container sorts its children by `Position.x`. A Selector's leftmost child is its
highest-priority branch. Add children in the wrong X order and priority is wrong even though the graph looks
right.

### 5. Containers cap their children — but only the CLI enforces it

`Entry` and every `Decorator` accept exactly one child. **Nothing enforces this while authoring in C#.**
Adding a second transition to a decorator succeeds silently, and since a decorator only ever enters child 0,
the extra becomes dead weight that still draws on the canvas.

The `bt_connect` command refuses to exceed the limit; `BehaviorTreeAuthoring.Connect` does not. Generated
code must respect it itself.

> **Also:** `SetDefaultValue` is a no-op unless the type supports inline values — basic types, `Vector2/3/4`,
> `Color`, `AnimationCurve`, `Rect`, `Ray`, `Ray2D`, `Type`, and any `UnityEngine.Object`. Anything else must
> be supplied by a connection.

---

## Ask the project, don't read it

Everything about the node set can be discovered from the running editor, and those answers cannot go stale
because they are derived from the code:

| Question | Command |
|---|---|
| What nodes exist, and which ports must I connect? | `bt_list_nodes [--category X] [--type X]` |
| What does this tree contain, and what are its node guids? | `bt_describe_tree --tree <path>` |
| Did I build it correctly? | `bt_verify --trees <path,path>` |
| What does this script graph contain? | `bt_describe_script_graph --graph <path>` |

Prefer these over reading source.

---

## The authoring helpers

`ArcaneOnyx.BehaviorTree.Authoring` (editor assembly) is the supported way to do this. Each helper has a
`bt_` CLI command beside it, so the same recipe runs from C# or step by step from the command line.

**Prefer C# for anything substantial** — a forty-node tree is one call instead of forty, and you keep typed
locals instead of passing guids around. Use the commands to inspect, to verify, and to make small edits to a
tree that already exists.

| Helper | CLI | Why it exists |
|---|---|---|
| `AddNode<T>(asset, x, y)` | `bt_add_node` | Sizes the node from its own `StartingSize`, the way the create menu does — no size parameter to get wrong |
| `SetValue(asset, port, value, x, y)` | `bt_set_value` | Checks whether the port declares a default and routes to an inline value or a connected literal. **Use this instead of `SetDefaultValue`** and gotcha 3 stops mattering |
| `Connect(asset, parent, child, index)` | `bt_connect` | Parent → child transition |
| `GuardOnVariable(asset, owner, name, expected, fallback, x, y)` | `bt_guard_on_variable` | A `BooleanConditionalExecution` fed by a variable read, adding a `Not` when `expected: false` |
| `GuardOnFunction(asset, owner, function, expected, x, y)` | `bt_guard_on_function` | The same shape fed by a Function instead, and it **seeds the guard's key trigger from the Function's declared watched keys**. Refuses a Function whose `Result` is not `bool` |
| `AddSubTree(asset, subTree, x, y)` | `bt_add_sub_tree` | A `RunBehaviorTreeGraphNode` pointed at another tree |
| `Declare(asset, name, value, scope)` | `bt_declare` | Declares a variable — `required` / `optional` / `instance` |
| `FeedFloat` / `FeedVector3` | `bt_feed_float` / `bt_feed_vector3` | Feed a port from a literal node whatever its declaration |
| `AddSticky(asset, title, body, x, y)` | `bt_add_sticky` | Canvas documentation; replaces a note with the same title rather than stacking duplicates |
| `SetComment(node, comment)` | `bt_set_comment` | The node's inspector comment |
| `CreateTree(path)` / `LoadTree(path)` | `bt_create_tree` | **Creating overwrites** — see below |
| `Save(asset)` | `bt_save` | Write to disk |
| `BehaviorTreeVerification.VerifyAndLog(paths)` | `bt_verify` | Reloads each tree and reports unset ports, orphans, unassigned sub-trees, recursion |
| `BehaviorTreeDump.ToJson(asset)` | `bt_describe_tree` | The tree as JSON, with guids |

On the sub-tree node itself:

| Method | CLI |
|---|---|
| `RefreshParameters()` | `bt_refresh_sub_tree_ports` |
| `DescribeContractDrift()` | included in `bt_verify` |

### Generate and verify in one run

A plain dump taken in the generating run is **blind to gotcha 3** — values written by `SetDefaultValue` are
still in memory, so ports report as filled that will be empty the moment the asset reloads.

`BehaviorTreeVerification.Reload` re-imports and instantiates the asset, forcing the serialize/deserialize
cycle. That is the same call the machine makes at awake, so what survives it is what reaches gameplay:

```csharp
Generate();                                   // whatever builds your assets
BehaviorTreeVerification.VerifyAndLog(paths); // sees them as the machine will
```

`bt_verify` and `bt_describe_tree` both reload before reading, so they are honest in the generating run.

### Editing a tree that already exists

`CreateTree` calls `AssetDatabase.CreateAsset`, which **replaces the file** — any hand-arranged layout goes
with it. To add to an existing tree, `LoadTree` it; every other helper works the same on a loaded asset.

Nodes are addressable by guid, which the dump emits:

```csharp
var asset = BehaviorTreeAuthoring.LoadTree(path);
var node  = asset.graph.Nodes.First(n => n.guid.ToString() == guidFromTheDump);
```

Prefer that over matching on position or type. Position is not cosmetic in a behavior tree — X is priority —
so it is the thing most likely to have moved.

---

## Building a tree

```csharp
using ArcaneOnyx.BehaviorTree;
using UnityEditor;
using UnityEngine;

// 1. asset and graph
var asset = ScriptableObject.CreateInstance<BehaviorTreeGraphAsset>();
var graph = asset.graph;                 // already contains exactly one Entry node
var entry = graph.EntryNode;

// 2. add a node -- size comes from the node, not from you
var selector = new Selector();
selector.Position = new Rect(new Vector2(0f, 200f), selector.StartingSize);
graph.Nodes.Add(selector);   // Nodes.Add fires Define(), so ports exist after this line

// 3. parent -> child
var transition = new BehaviorTreeTransition();
transition.SetupTransition(entry, selector, 0);   // third argument is the transition index
graph.Transitions.Add(transition);

// 4. wire a value port: source output -> destination input
getSlotPosition.Output.ValidlyConnectTo(setNavAgentPosition.NavPosition);

// 5. fill a defaultless port by connecting a literal
var duration = new FloatLiteral();
duration.Position = new Rect(new Vector2(-200f, 400f), duration.StartingSize);
graph.Nodes.Add(duration);
duration.Value.ValidlyConnectTo(waitNode.Time);

// a port that DOES declare a default can take an inline value instead
playNode.TriggerName.SetDefaultValue("Attack");

// 6. save
AssetDatabase.CreateAsset(asset, "Assets/Draugr/Trees/DraugrBase.asset");
AssetDatabase.SaveAssets();
```

### Conditional Executions

Guards are not parented. They are separate nodes naming an owner, and the runtime attaches them when the
graph awakes:

```csharp
var guard = new BooleanConditionalExecution();
guard.Position = new Rect(new Vector2(-150f, 180f), guard.StartingSize);
graph.Nodes.Add(guard);
guard.UpdateOwner(attackSequence);                 // the node this guard protects
hasAttackToken.Output.ValidlyConnectTo(guard.Value);
```

`UpdateOwner` records the intent; it does **not** arm the guard. The link is made during graph awake, which
has three consequences:

- A dump reads `Owner` directly, so guards show correctly in a freshly generated tree.
- Any test exercising guard *behaviour* must awake the graph first, or the branch runs unguarded and the test
  passes for the wrong reason.
- Awake **appends**, so calling it twice registers every guard twice.

Guards on one owner are stored as a list and ANDed. There is no `And` node and none is needed.

### Value nodes are not parented

Nodes like `GetSlotPosition` and the literals are pulled through ports, never given a transition. They appear
under `unreachable` in a dump, noted as read through a port. That is correct and expected.

### Sub-trees

```csharp
var runNode = new RunBehaviorTreeGraphNode { Position = new Rect(0f, 100f, 150f, 100f) };
graph.Nodes.Add(runNode);
runNode.SetBehaviorTreeGraphAsset(sharedBranchAsset);
runNode.RefreshParameters();   // without this the node has no parameter ports yet

BehaviorTreeAuthoring.SetValue(
    asset, runNode.valueInputs.First(p => p.key == "attackRange"), 1.8f, x, y);
```

See [Sub-Behavior Trees](sub-behavior-trees.md) for what the parameters mean. Three rules:

- **Never read `BehaviorTreeGraphAssetInstance` or `BehaviorTreeGraphInstance` while authoring.** Both
  `Object.Instantiate` on first access, so merely looking at a node clones an asset.
- **A tree must not reach itself.** Recursion is checked at load and throws with the path (`A -> B -> A`).
- **Guards do not cross the boundary.** To let a caller interrupt a sub-tree, guard the
  `RunBehaviorTreeGraphNode` in the parent.

### Canvas plumbing to ignore

Every transition owns three invisible `PlaceHolderNode`s so the connecting line can be selected. They live in
`graph.Nodes`, have null port collections, and are not authored structure. Tooling walking `graph.Nodes` must
skip them — filter on `IsVisible`.

---

## Building a Visual Scripting graph

Structurally identical, because BH3's port system is modelled on Unity's.

```csharp
using Unity.VisualScripting;

var asset = ScriptableObject.CreateInstance<ScriptGraphAsset>();
var graph = asset.graph;                          // FlowGraph

var branch = new If { position = new Vector2(0f, 0f) };
graph.units.Add(branch);                          // fires Define()

var literal = new Unity.VisualScripting.Literal(typeof(bool), true) { position = new Vector2(-200f, 0f) };
graph.units.Add(literal);

literal.output.ValidlyConnectTo(branch.condition); // value edge
branch.ifTrue.ValidlyConnectTo(nextUnit.enter);    // control edge
branch.condition.SetDefaultValue(true);            // inline value
```

| Behavior tree | Visual Scripting |
|---|---|
| `graph.Nodes` | `graph.units` |
| `node.Position` (`Rect`) | `unit.position` (`Vector2`) |
| `BehaviorTreeTransition` | `ControlOutput.ValidlyConnectTo(ControlInput)` |
| `valueOutput.ValidlyConnectTo(valueInput)` | same |
| `valueInput.SetDefaultValue(v)` | same |

> A port typed `object` — Unity's `GetVariable.fallback`, for one — fails `SupportsDefaultValue`, so
> `SetDefaultValue` on it is a silent no-op. Feed it a `Unity.VisualScripting.Literal` unit instead.

Generated `ScriptGraphAsset`s owned by a tree should be registered with `ScriptGraphAssetsRepository` so
unused-asset cleanup does not collect them.

---

## Checklist before calling a generated tree correct

- Every container's children appear in the intended order in the dump (X positions are right).
- No `(unset)` on a port the node actually reads.
- Nothing unexpected under `unreachable`.
- Each decorator has exactly one child.
- Guards list the owner you intended.
- Every sub-tree expanded to a real asset — no `(none assigned)` and no `recursion`.
- There is a `Repeater` under `Entry`, unless the tree really is meant to run once.

Two more that only matter once you look at behaviour rather than structure:

- **Guard through `GuardOnVariable`.** Every `BooleanConditionalExecution` is named "Boolean Conditional
  Execution", so without the label it writes, every explanation in [The Why Panel](why-panel.md) reads "a
  guard was false".
- **Keep value nodes pure.** Guard tracing re-pulls each `ValueOutput` when a guard flips, so a guard graph
  that writes will fire its side effects twice per transition.

---

## Related

- **Tactical Position Selection queries** are authored the same way, with their own `tps_` helpers and their
  own set of traps. That lives with the module:
  [Authoring TPS Queries From Code](../../Modules/TacticalPositionSelection/docs/authoring-queries-from-code.md).

---

## See also

- [API Reference](api-reference.md) — the runtime types
- [Custom Nodes](custom-nodes.md) — writing node types by hand
- [The Why Panel](why-panel.md) — proving a generated tree does what you meant, not just that it loads
