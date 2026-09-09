# Glossary

The words the documentation uses, defined once. Terms are grouped, not alphabetical, so that each definition
can lean on the ones before it.

---

## The tree

| Term | Meaning |
|---|---|
| **Behavior tree** | A tree of nodes that decides what an agent does. Every tick, execution starts at the root and walks down; each node returns a status that tells its parent what to do next. |
| **Tree asset** | A `BehaviorTreeGraphAsset` in the Project window, created with **Create → Visual Scripting → Behavior Tree**. The serialized definition of a tree. |
| **Agent** | The GameObject running a tree. It carries a **Behavior Tree Machine** and a **Variables** component. |
| **Machine** | The `BehaviorTreeMachine` component. It loads the tree asset on `Awake`, enters it on `Start`, and ticks it every `Update`. Each agent runs its own private copy of the asset. |
| **Tick** | One update of one agent's tree. Ticks are counted per agent from when it started, so two agents' tick numbers do not line up. |
| **Status** | What a node returns from a tick: `Success`, `Failure` or `Running`. The runtime also uses `Inactive` (not started), `None` and `Exception`. |
| **Entry** | The root node. Every tree has exactly one, created with the asset; it cannot be deleted and takes one child. |
| **Composite** | A node with several children that decides which of them run: `Sequence`, `Selector`, their `Parallel` and `Random` variants. |
| **Container** | Any node with children: a composite, a decorator, or Entry. |
| **Decorator** | A node with exactly one child that changes how that child is run or how its result is read: `Repeater`, `Cooldown`, `Until Success`, and so on. |
| **Action** | A leaf node that does something in the game: move, wait, play an animation, write a variable. In C# these derive from `GameplayNode`. |
| **Condition** | A leaf node that answers a yes/no question as `Success` or `Failure` when execution reaches it. Different from a guard, which is asked without being reached. |
| **Priority** | The order in which a composite tries its children. Stored on each parent-to-child connection, shown as a numbered badge on the child, counted from 1. Dragging a child sideways rewrites it. |

## Data

| Term | Meaning |
|---|---|
| **Port** | A typed input or output on a node. A node reads its inputs and never knows where the value came from. |
| **Wire** | A connection from an output port to an input port. Drawn by dragging pin to pin. |
| **Default** | The value an input port uses when nothing is wired to it. A port declared without a default **must** be wired, or reading it throws. |
| **Literal** | A node whose only job is to hold a typed value and offer it on an output port: `Float`, `Vector3`, `String`, and so on. |
| **Value node** | Any node that exists to be read through a port, such as a literal or a `Get Variable`. Value nodes have no parent connection. |
| **Variable** | A named value in one of five stores, or *kinds*: `Graph`, `Object`, `Scene`, `Application`, `Saved`. Written in lower case, "the blackboard" means these stores as a whole. |
| **Variables component** | Unity Visual Scripting's `Variables` component. It holds an agent's Object variables, and the machine requires one. |
| **Graph variable** | A variable belonging to a running tree instance. Reads walk outward through the callers; writes stay in the branch that made them. |
| **Object variable** | A variable on the agent's `Variables` component. Agent-wide facts, visible in the inspector, and the only kind a reactive guard can watch. |
| **Blackboard** | The panel in the graph editor that lists and edits variables in every kind. |
| **Scope** | The set of Graph variables a node can see: its own tree instance first, then the caller's, out to the root. Agent facts are Object variables and are read as such. |
| **Fact** | An `Object` variable that something outside the tree, usually a sensor, keeps up to date: `hasTarget`, `lastKnownPosition`. |
| **Sensor** | A MonoBehaviour that publishes facts. Writing through `AgentVariableWriter` makes the write visible to guards and to the recorder. |

## Control

| Term | Meaning |
|---|---|
| **Guard** | A precondition attached to a node, drawn beside it rather than above it. The node is the guard's **owner**. Several guards on one owner are ANDed. |
| **Conditional Execution** | A guard that is asked once, when its owner is about to start. If it says no, the owner is skipped. It is not asked again while the owner runs. |
| **Reactive Guard** | A guard that keeps watching while its owner runs. It can **stop its own branch** when its condition drops, and **take over** a lower-priority sibling's slot when its condition rises. |
| **Trigger** | When a reactive guard is allowed to recompute: when a watched key changes, on an interval, or every frame. An empty trigger list means every tick. |
| **Watched key** | The name of an `Object` variable a reactive guard wakes on. Typed by hand or inherited from the Function feeding the guard. |
| **Abort** | A running branch being stopped because its own guard turned false. Every node inside it gets `OnExit`. |
| **Take over** | A running branch losing its slot because a higher-priority sibling's reactive guard turned true. Recorded separately from an abort. `bt_verify` calls it preemption. |
| **Skip** | A node that was never entered because a guard said no at the door. |
| **Dirty** | A reactive guard whose trigger has fired since it last evaluated. Only a dirty guard runs its condition on the next tick. |
| **Armed** | A guard that has been attached to its owner by the machine at load, so it is actually asked. Also a breakpoint that is switched on. |

## Reuse

| Term | Meaning |
|---|---|
| **Sub-tree** | A tree asset run from inside another tree by a **Run Behavior Tree Graph** node. Instantiated per call site, per agent. |
| **Call site** | One `Run Behavior Tree Graph` node in one parent tree. The same branch used twice has two call sites and two separate scopes. |
| **Parameter** | A variable the sub-tree declares as **Required** or **Optional**. Each becomes an input port on the call site. |
| **Contract** | What a sub-tree or Function declares to its callers: its parameters or inputs, their types and defaults, and, for a Function, its result. Callers keep a copy of it. |
| **Contract drift** | The caller's copy of a contract no longer matching the asset. Reported on the node; repaired with **Refresh Parameters** or **Refresh Ports**. |
| **Function** | A named Visual Scripting graph asset with a declared contract, owned by the Visual Scripting Extension module. What a Script Graph Variable node reads. |
| **Script Graph Variable** | The node that runs a Function and offers its result on an output port. The bridge from Visual Scripting into any port. |
| **Script Graph** | The node whose lifecycle hooks (`OnAwake`, `OnEnter`, `OnUpdate`, `OnExit`) are Functions. In lower case, "a script graph" is any Unity Visual Scripting flow graph. |
| **Unit** | Visual Scripting's word for a node inside a script graph. BH3 ships two: **Set BT Variable** and **Get BT Variable**. |
| **Finder** | The searchable menu that opens when you right-click inside a script graph. Also called the fuzzy finder. |
| **Pin** | The small circle on a port that you drag a wire from or to. |
| **Headless** | Done from the command line or a script, with no editor window open. |

## Debugging

| Term | Meaning |
|---|---|
| **Flight recorder** | The per-agent ring buffer that records every node entry and exit, guard flip, variable write and take-over while the agent runs. On by default in the editor. |
| **Recording** | The contents of a flight recorder, live or saved to a JSON file from the Timeline panel. |
| **Timeline** | The panel under the canvas that draws the recording as lanes per depth, with a playhead you can drag. |
| **Playhead** | The tick the debugger is looking at. Every debugging panel, and the ghosted canvas, describes that tick. |
| **Ghosting** | The canvas showing node states as they were at the playhead, dimmed, with a banner saying so, while the agent keeps running behind it. |
| **Why panel** | The panel that explains why a node did what it did, as a headline and labelled clauses, read from the recording. |
| **Guard chain** | The values on every wire feeding a guard, captured at the instant it changed its mind. |
| **Variable Watch** | The panel that shows what every variable held at the playhead, and the history of who wrote it. |
| **Breakpoint** | A condition that pauses the editor when it happens: a node moment, a guard flip, or a variable write. |
| **Problem badge** | The red or amber marker on a node that cannot work as authored, with the reason on hover and the fix in the inspector. |
| **`bt_verify`** | The command that reloads a tree from disk and reports everything the problem badges would, plus checks that need the whole tree. |

## Code

| Term | Meaning |
|---|---|
| **`BTContext`** | The struct every lifecycle hook receives. Everything a node knows about the agent it is running on, including its per-agent memory. |
| **Memory** | `ctx.Memory<T>()`. Per-agent state for a node, in place of instance fields. |
| **Lifecycle hooks** | `OnAwake`, `OnEnter`, `OnUpdate`, `OnExit`. `OnUpdate` only runs on a node that entered. |
| **`GraphCreateMenu`** | The attribute that puts a node type in the canvas right-click menu, under the category path you give it. |
| **Placeholder** | What a node becomes when its C# type no longer exists. It keeps every value and wire until you retarget or delete it, and ticks as `Success` in the meantime. |
