# Click to move

A capsule on a small arena. Left-click the ground and it hops its way there along the NavMesh, then settles
back into a gentle breathing idle. Click somewhere else while it is on its way and it turns toward the new
point on the next frame. The path it is about to walk is drawn as a line ahead of it, bending around the blocks,
and goes away when it arrives.

Open `ClickToMove.unity` and press Play. Everything you see is primitives and tweens; there is no art and no
animation clip in this sample.

## The trees

```
ClickToMove
└─ Repeater → Selector            (priority left → right)
   ├─ MoveTo    Reactive Guard: OrdersDiffer(orderNumber, completedOrder), woken by either key changing
   │            tripDestination ← Function ClickToMove.destinationRead
   │            acceptedOrder   ← orderNumber
   └─ Idle      no guard, the fallback

MoveTo   (Required parameters: tripDestination, acceptedOrder)
└─ Parallel Selector              ends when any child succeeds
   ├─ Sequence: WalkTo(tripDestination) → Set Variable completedOrder = acceptedOrder
   └─ Until Success → Boolean Condition ← OrdersDiffer(orderNumber, acceptedOrder)

Idle
└─ Repeater → Punch Scale         the breathing
```

Each tree is its own asset. `ClickToMove` is the only one that knows the other two exist.

## What each piece demonstrates

| Feature | Where | What to watch |
|---|---|---|
| **Takeover** | MoveTo's Reactive Guard | The capsule is breathing in Idle, which checks nothing. The click bumps `orderNumber`, the guard wakes on that key and takes the slot the same frame. |
| **Self-terminating branch** | Last node of MoveTo's Sequence | Arrival writes `completedOrder` through the Set Variable node. That write is observable, so the guard turns false and aborts MoveTo; the Selector falls back to Idle. |
| **A Function feeding a port** | `Functions/ClickToMove.destinationRead` | Reads the agent's `destination` fact and feeds MoveTo's `tripDestination` port at the call site. MoveTo never learns the variable name. |
| **One Function, two jobs** | `Functions/OrdersDiffer` | Two int inputs, true when they differ. The guard feeds it `orderNumber` and `completedOrder`; the redirect feeds it `orderNumber` and `acceptedOrder`. The graph itself is pure and reads no variable. |
| **Parallel Selector** | MoveTo | Travel, watch for a newer order, and hop all run at once. The first child to succeed ends the trip. |
| **Multi-tick custom nodes** | `Hop`, `Punch Scale` | Each starts a tween on enter, returns Running until it finishes, and restores the body on exit. Abort Idle mid-breath and the scale snaps back; abort MoveTo mid-hop and the body lands. |
| **Required parameters** | MoveTo's `tripDestination`, `acceptedOrder` | Declared Required, so an unfed call site is a `bt_verify` finding rather than a silent zero. |

## How a click becomes a trip

1. `ClickToMoveInput` raycasts the click, snaps it to the NavMesh, and writes `destination` and a new
   `orderNumber` through the `AgentVariableWriter`.
2. `orderNumber` now differs from `completedOrder`, so the guard on MoveTo wakes, MoveTo enters, and its two
   parameters are read once: the destination, and the order number this trip is for.
3. On arrival the trip writes `completedOrder = acceptedOrder`. It completes **its own** order only, which is
   what makes a click landing on the very frame of arrival safe: the new order number still differs, so the
   guard stays true and the next trip starts.
4. A second click mid-trip bumps `orderNumber` again. `OrdersDiffer(orderNumber, acceptedOrder)` turns true,
   the Until Success child succeeds, the Parallel Selector ends, and the outer Repeater re-enters MoveTo with
   the new point.

## The cast

| Piece | Role |
|---|---|
| `ClickToMoveInput` | The only script in the sample. Reads the mouse through the Input System, publishes the destination and the order number, pops the marker. |
| `ClickToMoveFacts` | The three variable names as constants. |
| `Hop` | Lifts the agent's body and lands it again. Lives in `Samples/Common/Tweening/Nodes` with Punch Scale and Flash Color. |
| `Agent` | NavMeshAgent, BehaviorTreeMachine, AgentVariableWriter and `Unit` on the root; `Unit` points at the `Body` child and its renderers, which the tween nodes animate. |

## Notes

- The sample reads the mouse through `com.unity.inputsystem`. A project on the legacy input manager only
  needs that package added.
- Every tunable is a port default on the canvas (hop height, breath strength) or a NavMeshAgent setting.
