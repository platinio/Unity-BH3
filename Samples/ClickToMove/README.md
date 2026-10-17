# Click to move

A capsule on a small arena. Left-click the ground and it hops its way there along the NavMesh, then settles
back into a gentle breathing idle. Click somewhere else while it is on its way and it turns toward the new
point on the next frame.

Open `ClickToMove.unity` and press Play. Everything you see is primitives and tweens; there is no art and no
animation clip in this sample.

## The trees

```
ClickToMove
└─ Repeater → Selector            (priority left → right)
   ├─ MoveTo    Reactive Guard on hasDestination, woken by that key changing
   │            tripDestination ← Function ClickToMove.destinationRead
   └─ Idle      no guard, the fallback

MoveTo   (Required parameter: tripDestination)
└─ Parallel Selector              ends when any child succeeds
   ├─ Sequence: Set Nav Agent Position(tripDestination) → Wait Until Reach → Set Variable hasDestination = false
   ├─ Until Success → Boolean Condition ← Function HasNewDestination(destination, tripDestination)
   └─ Repeater → Hop

Idle
└─ Repeater → Punch Scale         the breathing
```

Each tree is its own asset. `ClickToMove` is the only one that knows the other two exist.

## What each piece demonstrates

| Feature | Where | What to watch |
|---|---|---|
| **Takeover** | MoveTo's Reactive Guard | The capsule is breathing in Idle, which checks nothing. The click publishes `hasDestination`, the guard wakes on that key and takes the slot the same frame. |
| **Self-terminating branch** | Last node of MoveTo's Sequence | Arrival clears `hasDestination` through the Set Variable node. That write is observable, so the guard turns false and aborts MoveTo; the Selector falls back to Idle. |
| **A Function feeding a port** | `Functions/ClickToMove.destinationRead` | Reads the agent's `destination` fact and feeds MoveTo's `tripDestination` port at the call site. MoveTo never learns the variable name. |
| **A Function with inputs** | `Functions/HasNewDestination` | Two Vector3 inputs, true when they differ. Its ports are fed by a read of the agent fact and a read of the branch parameter, so the graph itself is pure. |
| **Parallel Selector** | MoveTo | Travel, watch for a newer order, and hop all run at once. The first child to succeed ends the trip. |
| **Multi-tick custom nodes** | `Hop`, `Punch Scale` | Each starts a tween on enter, returns Running until it finishes, and restores the body on exit. Abort Idle mid-breath and the scale snaps back; abort MoveTo mid-hop and the body lands. |
| **Required parameter** | MoveTo's `tripDestination` | Declared Required, so an unfed call site is a `bt_verify` finding rather than a silent zero. |

## How a click becomes a trip

1. `ClickToMoveInput` raycasts the click, snaps it to the NavMesh, and writes `destination` and
   `hasDestination = true` through the `AgentVariableWriter`.
2. The guard on MoveTo wakes on `hasDestination`, MoveTo enters, and its parameter is read once from the
   `destination` fact.
3. A second click only rewrites `destination`. `HasNewDestination` compares that against the parameter the
   trip accepted, the Until Success child succeeds, the Parallel Selector ends, and the outer Repeater
   re-enters MoveTo with the new point. Clicking the same spot twice changes nothing.

## The cast

| Piece | Role |
|---|---|
| `ClickToMoveInput` | The only script in the sample. Reads the mouse through the Input System, publishes the two facts, pops the marker. |
| `ClickToMoveFacts` | The two variable names as constants. |
| `Hop` | Lifts the agent's body and lands it again. Lives in `Samples/Common/Tweening/Nodes` with Punch Scale and Flash Color. |
| `Agent` | NavMeshAgent, BehaviorTreeMachine and AgentVariableWriter on the root; the capsule is a `Body` child the tween nodes animate. |

## Notes

- The sample reads the mouse through `com.unity.inputsystem`. A project on the legacy input manager only
  needs that package added.
- Every tunable is a port default on the canvas (hop height, breath strength) or a NavMeshAgent setting.
