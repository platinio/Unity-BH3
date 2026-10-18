# RTS

A small real-time strategy map. You start with a Town Hall, three Workers, a little wood and gold, two
forests, two gold outcrops and three enemy camps in the far corners. Workers gather wood and gold and
carry it home; wood raises a Barracks or a Keep; buildings train Soldiers, Archers and Knights for gold;
your fighters attack-move across the map and clear the camps. When no enemy is left standing the HUD says
**Victory** and the game keeps running. There is no way to lose: the Town Hall cannot be destroyed and a
dead Worker respawns where it was made. Kenney blocky characters, Kenney castle and forest pieces,
primitives and tweens; every animation is a tween on a body part.

Open `RTS.unity` and press Play.

## Controls

| Input | What it does |
|---|---|
| Left drag | Selects every unit inside the rectangle. A click selects one unit, or a building, or clears the selection on ground. |
| Right click | The order depends on what is under the cursor: an enemy is attacked, a tree or rock is gathered (Workers), an unfinished site is built (Workers), the ground is walked to. Fighters walking to a point fight anything they see on the way. |
| **1**, **2** with Workers selected | Pick a Barracks or a Keep. A translucent ghost follows the cursor, red where the footprint is taken; left click places it, spends the wood and sends the selected Workers to build it. Right click or Escape cancels. |
| **1**, **2** with a building selected | Queue a unit that building trains, if the treasury can pay. The HUD shows the queue. |
| WASD, screen edge | Pan the camera inside the map. |

**Automation is the point.** An order is given once and runs until it is finished or replaced. A Worker sent
to a tree gathers, walks the load home, walks back and keeps going; when the tree is bare it moves to the
nearest tree of the same kind; it stops only when nothing is left or when you tell it to do something else.
A Worker sent to a site builds until the building stands. Any unit that is hit while doing something else
fights back and then **resumes what it was doing**, which falls out of the tree shape below rather than out
of code.

## The trees

```
Worker                                               Soldier / Archer / Knight (Fighter)
└─ Repeater → Selector                               └─ Repeater → Selector
   ├─ Die        RG: isDead                             ├─ Die       RG: isDead
   ├─ FightBack  RG: hasTarget        (Combat's Engage)  ├─ Engage    RG: hasTarget   (Combat's Engage)
   ├─ Build      RG: HasNewOrder(buildOrder, completedBuildOrder)      ├─ AttackMove RG: HasNewOrder(orderNumber, completedOrder)
   ├─ Gather     RG: HasNewOrder(gatherOrder, completedGatherOrder)    │             (Click to move's MoveTo)
   ├─ MoveTo     RG: HasNewOrder(orderNumber, completedOrder)          └─ Idle
   └─ Idle

Gather (resource, acceptedGatherOrder)                            one trip per run
└─ Sequence
   ├─ Parallel Selector [ Set Nav Agent Position(ApproachPoint(resource, 2)) → Wait Until Reach | Repeater → Hop ]
   ├─ Until Failure → Sequence [ Face Target(TransformOf(resource)) → Swing Part up → Swing Part down → Harvest(resource) → Wait 0.5 ]
   ├─ Return Success → Sequence [ Boolean Condition(IsCarryingSomething) → walk to ApproachPoint(depot, 3.5) → Deposit(depot) ]
   └─ Selector
      ├─ Boolean Condition(HasResource(resource))                                   same node next time
      ├─ Sequence [ Boolean Condition(Is Not Null(NearestResource(resource))) → Set Variable resource ← NearestResource(resource) ]
      └─ Set Variable completedGatherOrder ← acceptedGatherOrder                   nothing left: the guard drops

Build (site, acceptedBuildOrder)
└─ Sequence
   ├─ walk to ApproachPoint(site, 3.5)
   ├─ Until Failure → Sequence [ Face Target(TransformOf(site)) → Swing Part up → Swing Part down → Construct(site, 1 s) → Wait 0.3 ]
   └─ Set Variable completedBuildOrder ← acceptedBuildOrder

Building
└─ Repeater → Selector
   ├─ UnderConstruction  RG: not isBuilt      → Grow → Wait 0.1
   ├─ Produce            RG: HasQueuedUnit    → Wait(trainSeconds) → Spawn Unit → Dequeue
   └─ Wait 0.2

Enemy (grunt, archer)                                  Brute
└─ Repeater → Selector                                 └─ Repeater → Selector
   ├─ Perish      RG: isDead      (Defense's Perish)      ├─ Perish   RG: isDead
   ├─ ReturnHome  RG: IsFarFrom(home, 12), every 0.25 s   ├─ Engage   RG: hasTarget
   ├─ Engage      RG: hasTarget                           └─ Idle
   └─ Wander
```

`Die`, `Engage`, the three attack trees, `Chase`, `Kite`, `ReturnHome` and `Wander` are Combat's assets;
`MoveTo` and `Idle` are Click to move's; `Perish` is Defense's. Combat's `Engage` gained a fourth branch,
`PunchAttack` behind a `HoldsWeapon` guard on `"Fists"`, so a Worker can fight back with its hands.

## What each piece demonstrates

| Feature | Where | What to watch |
|---|---|---|
| **Interrupt and resume without code** | The Worker tree | FightBack sits above the long jobs. A hit sets `target` and `hasTarget`, the guard takes over and the Gather or Build branch is aborted mid-swing, its tween snapping the body back. When the target dies `UnitTargeting` drops `hasTarget`, the Selector falls through, and the job re-enters with the **same parameters**: the order facts were never cleared, and the load is a fact on the Worker, not state inside the tree. |
| **Numbered orders, three channels** | `PlayerCommands`, the Worker's three guards | A move writes `destination` and `orderNumber`, a gather writes `resource` and `gatherOrder`, a build writes `site` and `buildOrder`. Each branch completes **its own** number, so an order given on the frame a branch finishes is never lost. One Function, Click to move's `HasNewOrder`, guards all three channels. |
| **A branch that runs itself** | `Gather` | One trip per run. The branch decides what comes next at the end: the same node, the nearest node of the same kind, or done. Re-pointing writes the agent's `resource` fact, so the caller re-enters pointing at the new tree. |
| **Functions as swappable pieces** | `Functions/`, `Common/Functions/ApproachPoint` | `HasResource` reads a node's `remaining` fact, `IsCarryingSomething` the agent's `carriedAmount`, `HasQueuedUnit` a building's `queueCount`; all three are three-unit graphs. `ApproachPoint` is `Vector3.MoveTowards` between two positions, so a unit stands beside a thing instead of on it, and the same Function serves trees, rocks, sites and the depot. `NearestResource` is the one that is better in code, a loop over a list, wrapped so the tree can swap the policy. |
| **Kinds as data** | `UnitKinds/`, `BuildingKinds/`, `EnemyKinds/` | A unit kind is its prefab, material, weapon, tree, health, speed, sight, cost and training time; a building kind its model, costs, build time, footprint, what it trains and where it rallies. One `Worker` prefab and one `Fighter` prefab serve every kind; one `Building` prefab serves every building, dressed by `Apply`. |
| **A tree on a building** | `Building`, `Grow`, `Spawn Unit` | While `isBuilt` is false the body grows with `progress01`, which the Workers' `Construct` swings write. While `HasQueuedUnit` is true the building waits the head unit's `trainSeconds`, spawns it at the rally point and dequeues it; the guard wakes on `queueCount`, so the first order starts the branch and the last Dequeue ends it. |
| **Two Workers build twice as fast** | `Construct` | Each swing adds one second of work. Nothing counts builders; the sum does. |
| **Attack-move is a move with Sight** | The Fighter tree | A right click on the ground is Click to move's `MoveTo`. `Sight` publishes what it sees on the way, Engage takes the slot, and when the target dies `MoveTo` resumes toward the same point with the same order. |
| **Camps that stay cleared** | `Enemy`, `Brute`, `RtsGame` | Combat's enemies with Defense's `Perish` instead of `Die`. `RtsGame` scans the roster once a second and the HUD shows Victory once no enemy is alive. |
| **Hitting back, shared** | `Retaliation`, `UnitTargeting` | Lifted out of Combat's `PlayerOrders` into `Common`: a hit with no target makes the attacker the target; a dead target is dropped. Workers, fighters, enemies and Combat's player all carry both. |
| **Placing with a ghost** | `BuildingPlacer` | Keys pick a kind, a translucent copy of its model follows the cursor and turns red over a building or a resource, and a click raises a site and orders the selected Workers to it. Selection and commands are switched off while placing so the click reaches only the placer. |

## The cast

| Piece | Role |
|---|---|
| `RtsUnit`, `UnitKind` | Applies a kind to a player unit on Awake through `CombatantKindApplier`, shared with Combat's `Enemy`. Publishes `depot`. |
| `Building`, `BuildingKind` | Dresses the Building prefab with its kind's model, sizes its footprint and carving obstacle, publishes `isBuilt` and `progress01`, takes work from `Construct`. |
| `ProductionQueue` | What a building has been asked to train; publishes `queueCount`, `trainSeconds` and `nextKind`. |
| `ResourceNode`, `Resource` | A tree or a gold outcrop: kind, what is left (published as `remaining`), shrinks as it empties, hides when bare. `NearestLike` serves the Function. |
| `Carrier` | What a Worker holds, twenty of one kind at most; publishes `carriedKind`, `carriedAmount`, `isCarryingFull`. |
| `Depot`, `Treasury` | The Town Hall takes a carrier's load into the Treasury on the Game object, which publishes `wood` and `gold`. |
| `Selectable`, `SelectionRing`, `SelectionBox` | The roster of what can be selected, the ring under a selected unit, and the drag rectangle that owns the selection. |
| `PlayerCommands` | Right clicks become attack, build, gather or move orders for the selection, each numbered; the only writer of order facts. |
| `BuildingPlacer`, `TrainingCommands` | Keys 1 and 2: a ghost and a site with Workers selected, a queued unit with a building selected. Both pay the Treasury. |
| `CameraRig`, `RtsHud`, `RtsGame` | The pan, the readout and the win check. |
| `Retaliation`, `UnitTargeting` | In `Common`: hit back when there is no target, drop a dead one. |
| `Harvest`, `Deposit`, `Construct`, `Grow`, `Spawn Unit`, `Dequeue` | The custom nodes this sample adds. |

## Notes

- Facts a guard may read before anyone has written them (`gatherOrder`, `buildOrder`, `completedOrder`,
  `queueCount`, `trainSeconds`, `home`) go through variable-read Functions with a fallback, as in every
  sample. `resource` and `site` are read with a plain Get Variable, because they are only read at a branch's
  entry, after the order that wrote them.
- A Function handed a null GameObject throws inside Visual Scripting's variable lookup, so the candidate from
  `NearestResource` is tested with Is Not Null before anything asks `HasResource` about it.
- Every tunable is a port default on a canvas, a field on a kind asset, a serialized field on `Treasury`,
  `ResourceNode`, `Carrier` or `CameraRig`, or a NavMeshAgent setting.
- Units never pick up weapons here; a kind arrives armed. Shift attack-move for Workers was left out:
  Workers have no `Sight`, so an attack-move would be a plain move.
