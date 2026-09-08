# Authoring from code

Generating behavior trees and Visual Scripting graphs programmatically, for tooling, bulk edits and
generated content.

Most people never need this page. Read it if you are writing an editor tool that builds trees, migrating
many assets at once, or driving BH3 from a script.

---

## Read this first: four things that will break your output

Each of these produces a tree that looks correct and fails at runtime.

### 1. Five type names exist in both namespaces

`Literal`, `ValueInput`, `ValueOutput`, `ValueInputDefinition` and `ValueOutputDefinition` are defined by
**both** `ArcaneOnyx.BehaviorTree` and `Unity.VisualScripting`, and so is `Cooldown`. Inside the
`ArcaneOnyx.BehaviorTree` namespace the BH3 one silently wins, even with `using Unity.VisualScripting;`
present; outside it, the compiler reports an ambiguous reference. Alias the BH3 ones
(`using ValueInput = ArcaneOnyx.BehaviorTree.ValueInput;`) in node files, and when authoring script graphs,
fully qualify:

```csharp
var literal = new Unity.VisualScripting.Literal(typeof(bool), true);
var output  = new Unity.VisualScripting.ValueOutputDefinition { key = "Result", type = typeof(float) };
```

### 2. An unset port throws; it does not default

`GetValue()` throws when a port has neither a connection nor a declared default. Do not keep a list of
those ports in your head: `bt_list_nodes` flags every defaultless port on every node, derived from the
nodes themselves, and `bt_list_nodes --type FaceTarget` narrows it to one. Ports read through
`GetComponent<T>(port)` are the exception; they fall back to the agent's own component.

### 3. Inline values do not survive a reload on a defaultless port

This one costs an afternoon, because it works right up until the asset reloads.

A node's default values live in a dictionary that is cleared and rebuilt by `Definition()` every time the
node deserializes. **Only keys `Definition()` itself puts back survive.** Setting a port that was declared
without a default writes a key nothing will re-create:

```csharp
// FaceTarget.RotationSpeed is declared with no default
faceNode.RotationSpeed.SetDefaultValue(90f);   // holds in memory, GONE after save + reload → throws on first tick

// Cooldown.Duration is declared with a default
cooldown.Duration.SetDefaultValue(2f);         // the key exists, so this persists
```

Nothing warns you. A dump taken in the generating run reports the value correctly; reopen the project and
the same port reads `(unset)`.

**To give a bare port a value, connect a literal node.** `BehaviorTreeAuthoring.SetValue` checks which case
the port is and does the right thing, so using it makes this trap unreachable. `SetDefaultValue` is also a
silent no-op for any type without inline-value support (anything beyond primitives, `Vector2/3/4`, `Color`,
`AnimationCurve`, `Rect`, `Ray`, `Type` and `UnityEngine.Object`).

### 4. Containers cap their children, but only the CLI enforces it

`Entry` and every `Decorator` accept exactly one child. `bt_connect` refuses a second;
`BehaviorTreeAuthoring.Connect` does not, and a decorator only ever enters child 0, so the extra becomes
dead weight that still draws on the canvas. Generated code must respect the limit itself.

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

---

## The authoring helpers

`ArcaneOnyx.BehaviorTree.Authoring.BehaviorTreeAuthoring` (editor assembly) is the supported way to do this.
Each helper has a `bt_` command beside it, so the same recipe runs from C# or step by step from the command
line. **Prefer C# for anything substantial**: a forty-node tree is one call instead of forty, with typed
locals instead of guids. Use the commands to inspect, to verify, and to make small edits to a tree that
already exists.

| Helper | Command | Why it exists |
|---|---|---|
| `AddNode<T>(asset, x, y)` | `bt_add_node` | Sizes the node from its own `StartingSize`, the way the create menu does |
| `SetValue(asset, port, value, x, y)` | `bt_set_value` | Routes to an inline value or a connected literal depending on whether the port declares a default. **Use this instead of `SetDefaultValue`** |
| `Connect(asset, parent, child, index)` | `bt_connect` | Parent → child transition; `index` is the priority, next free slot when omitted |
| `GuardOnVariable(asset, owner, name, expected, fallback, x, y, kind)` | `bt_guard_on_variable` | A reactive guard (or `GuardKind.Conditional`) fed by a variable read, adding a `Not` when `expected: false`, with an On Key Changed trigger seeded from the name |
| `GuardOnFunction(asset, owner, function, expected, x, y, kind)` | `bt_guard_on_function` | The same fed by a Function, with the trigger seeded from the Function's declared keys. Refuses a Function whose `Result` is not `bool` |
| `AddSubTree(asset, subTree, x, y)` | `bt_add_sub_tree` | A `RunBehaviorTreeGraphNode` pointed at another tree, parameters already refreshed |
| `Declare(asset, name, value, scope)` | `bt_declare` | Declares a variable: `Instance`, `Required` or `Optional` |
| `FeedFloat` / `FeedVector3` | `bt_feed_float` / `bt_feed_vector3` | Feed a port from a literal node whatever its declaration |
| `AddSticky(asset, title, body, x, y, w, h, color)` | `bt_add_sticky` | Canvas documentation; replaces a note with the same title rather than stacking duplicates |
| `SetComment(node, comment)` | `bt_set_comment` | The node's inspector comment |
| `CreateTree(path)` / `LoadTree(path)` | `bt_create_tree` | **Creating overwrites**, see below |
| `Save(asset)` | `bt_save` | Write to disk. Not undoable |
| `CreateVariableReadFunction(asset, name, fallback)` | `bt_add_variable_read` | A Function at `<TreeFolder>/Functions/<Tree>.<name>Read.asset` that reads one variable, reused if it already exists |
| `BehaviorTreeVerification.VerifyAndLog(paths)` | `bt_verify` | Reloads each tree and reports everything in [Checking your tree](../2-building-trees/09-checking-your-tree.md) |
| `BehaviorTreeDump.ToJson(asset)` | `bt_describe_tree` | The tree as JSON, with guids |

On the sub-tree node itself, `RefreshParameters()` is `bt_refresh_sub_tree_ports` and
`DescribeContractDrift()` is part of `bt_verify`. The full command list is in
[Command-line tools](05-command-line-tools.md).

### Generate and verify in one run

A dump taken in the generating run is blind to trap 3: values written by `SetDefaultValue` are still in
memory. `BehaviorTreeVerification.Reload` re-imports and instantiates the asset, forcing the
serialize/deserialize cycle the machine performs at Awake, so what survives it is what reaches gameplay:

```csharp
Generate();                                   // whatever builds your assets
BehaviorTreeVerification.VerifyAndLog(paths); // sees them as the machine will
```

`bt_verify` and `bt_describe_tree` both reload before reading.

### Editing a tree that already exists

`CreateTree` calls `AssetDatabase.CreateAsset`, which **replaces the file**, hand-arranged layout included.
To add to an existing tree, `LoadTree` it; every other helper works the same on a loaded asset.

Nodes are addressable by guid, which the dump emits:

```csharp
var asset = BehaviorTreeAuthoring.LoadTree(path);
var node  = asset.graph.Nodes.First(n => n.guid.ToString() == guidFromTheDump);
```

Prefer that over matching on position or type.

---

## Building a tree

```csharp
using ArcaneOnyx.BehaviorTree;
using ArcaneOnyx.BehaviorTree.Authoring;
using UnityEngine;

var asset = BehaviorTreeAuthoring.CreateTree("Assets/AI/Trees/Draugr.asset");
var entry = asset.graph.EntryNode;                       // created with the asset

var repeater = BehaviorTreeAuthoring.AddNode<Repeater>(asset, 0f, 150f);
var selector = BehaviorTreeAuthoring.AddNode<Selector>(asset, 0f, 300f);
var attack   = BehaviorTreeAuthoring.AddSubTree(asset, attackTree, -200f, 450f);
var idle     = BehaviorTreeAuthoring.AddSubTree(asset, idleTree,    200f, 450f);

BehaviorTreeAuthoring.Connect(asset, entry, repeater);
BehaviorTreeAuthoring.Connect(asset, repeater, selector);
BehaviorTreeAuthoring.Connect(asset, selector, attack, 0);      // priority 1 on the badge
BehaviorTreeAuthoring.Connect(asset, selector, idle, 1);        // priority 2

// a required parameter on the Attack sub-tree, fed from a literal
BehaviorTreeAuthoring.SetValue(asset, attack.valueInputs.First(p => p.key == "attackRange"), 1.8f, -400f, 450f);

// a reactive guard on the call site, waking when hasTarget changes
BehaviorTreeAuthoring.GuardOnVariable(asset, attack, "hasTarget", true, false, -200f, 340f);

BehaviorTreeAuthoring.AddSticky(asset, "Draugr", "Attack when a target exists, otherwise idle.", -400f, 0f, 260f, 80f, ArcaneOnyx.GraphCore.StickyNote.ColorEnum.Teal);   // StickyNote also exists in Unity.VisualScripting
BehaviorTreeAuthoring.Save(asset);
BehaviorTreeVerification.VerifyAndLog(new[] { "Assets/AI/Trees/Draugr.asset" });
```

Priority is the third argument to `Connect`, stored on the transition. Lay generated nodes out left to
right in priority order anyway, or the badges turn amber and a person reviewing the tree has to read them
instead of the picture. See [Execution order](../2-building-trees/05-execution-order.md).

### Under the helpers

If you need to go below `BehaviorTreeAuthoring`, this is what it does:

```csharp
var graph = asset.graph;

var selector = new Selector();
selector.Position = new Rect(new Vector2(0f, 200f), selector.StartingSize);
graph.Nodes.Add(selector);                                   // Nodes.Add fires Define(), so ports exist after this

var transition = new BehaviorTreeTransition();
transition.SetupTransition(entry, selector, 0);              // third argument is the priority index
graph.Transitions.Add(transition);

randomPosition.Position.ValidlyConnectTo(setNavAgentPosition.NavPosition);   // value wire

var guard = new BooleanConditionalExecution();
guard.Position = new Rect(new Vector2(-150f, 180f), guard.StartingSize);
graph.Nodes.Add(guard);
guard.UpdateOwner(attackSequence);                           // the node this guard protects
hasAttackToken.Value.ValidlyConnectTo(guard.Value);          // hasAttackToken is a BoolLiteral
```

Three things to know at this level:

- **`UpdateOwner` records intent; it does not arm the guard.** The link is made during graph awake, so a
  test exercising guard *behaviour* must awake the graph first, or the branch runs unguarded and the test
  passes for the wrong reason. Awake appends, so calling it twice registers every guard twice.
- **Value nodes are not parented.** Literals and `Get Variable` are pulled through ports, never given a
  transition. They appear under `unreachable` in a dump, noted as read through a port. That is correct.
- **A sub-tree node needs `RefreshParameters()`** after `SetBehaviorTreeGraphAsset` before it has any
  parameter ports; `AddSubTree` does that for you.

---

## Building a Visual Scripting graph

Structurally identical, because BH3's port system is modelled on Unity's:

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

A port typed `object`, Unity's `GetVariable.fallback` for one, fails `SupportsDefaultValue`, so
`SetDefaultValue` on it is a silent no-op. Feed it a `Unity.VisualScripting.Literal` unit instead.

A graph a tree reads is a **Function**: an ordinary asset at its own path, referenced by the node that reads
it. `fn_create` makes one that already runs end to end;
`BehaviorTreeAuthoring.CreateVariableReadFunction` makes the common one-variable read.

---

## Checklist before calling a generated tree correct

- Every composite's children carry the intended priorities, and their X positions agree, so no badge is amber.
- No `(unset)` on a port the node actually reads.
- Nothing unexpected under `unreachable`.
- Each decorator has exactly one child.
- Guards list the owner you intended, and were built through `GuardOnVariable` or `GuardOnFunction` so the
  Why panel can name them rather than reading "a guard was false".
- Every sub-tree expanded to a real asset: no `(none assigned)` and no `recursion`.
- There is a `Repeater` under `Entry`, unless the tree really is meant to run once.
- Guard graphs are pure: guard tracing re-pulls each `ValueOutput` when a guard flips, so a guard graph that
  writes fires its side effects twice per transition.

---

## Related

Tactical Position Selection queries are authored the same way, with their own `tps_` helpers and their own
traps: [Authoring TPS queries from code](https://github.com/platinio/Unity-TacticalPositionSelection/blob/main/docs/authoring-queries-from-code.md).

## Next

- [Command-line tools](05-command-line-tools.md)
- [API reference](02-api-reference.md)
