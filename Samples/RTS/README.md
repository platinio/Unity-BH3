# RTS

A real-time strategy map. You start with a Town Hall, three Workers, some wood and gold, two forests, two
gold outcrops and three enemy camps in the corners. Workers gather wood and gold and carry it home; wood
raises a Barracks or a Keep; buildings train Soldiers, Archers and Knights for gold; fighters attack-move
across the map and clear the camps. When no enemy is left the HUD shows **Victory** and the game keeps
running. The Town Hall cannot be destroyed. A dead unit is not replaced, so with no Workers and no gold
there is nothing left to do. Kenney characters, Kenney castle and forest pieces, primitives; every animation
is a tween on a body part.

Open `RTS.unity` and press Play.

## Controls

| Input | What it does |
|---|---|
| Left drag | Selects every unit inside the rectangle. A click selects one unit, or a building, or clears the selection on ground. |
| Right click | The order depends on what is under the cursor: an enemy is attacked, a tree or rock is gathered (Workers), an unfinished site is built (Workers), the ground is walked to. A right click on an **enemy** is an attack-move: fighters fight anything they see on the way. A right click on the **ground** is a withdrawal: they break off and walk, and pick nothing up on the way. |
| Action bar, Workers selected | The bar along the bottom lists the buildings they can raise, each with its cost and greyed out while the treasury cannot pay. Click one and a translucent ghost follows the cursor, red where the footprint is taken; a left click places it, spends the wood and sends the selected Workers to build it. Right click or Escape cancels. |
| Action bar, a building selected | The bar lists what that building trains. Click one to pay for it and put it in the queue; a bar over the building fills while the unit trains. |
| WASD, screen edge | Pan the camera inside the map. |

**Automation is the point.** An order is given once and runs until it is finished or replaced. A Worker sent
to a tree gathers, walks the load home, walks back and keeps going; when the tree is bare it moves to the
nearest tree of the same kind; it stops only when nothing is left or when you tell it to do something else.
A Worker sent to a site builds until the building stands. Any unit that is hit while doing something else
fights back and then **resumes what it was doing**, which falls out of the tree shape below rather than out
of code. The one order that outranks a fight is a plain move: right-click the ground and the unit breaks
off, walks there and does not swing at anything on the way, because that is what you asked for.

## What each piece demonstrates

| Feature | Where | What to watch |
|---|---|---|
| **A move order outranks the fight** | `StandsDownWhileMoving`, `PlayerCommands.OrderMove` | Engage sits above MoveTo in every fighter tree, which is what makes a unit defend itself mid-walk — and it also means the move branch is only reached while nothing is targeted. So a plain move drops the target and the unit stops looking: `Sight` and `Retaliation` are switched off until it arrives, then switched back on. Without that, `Sight` republishes a target every 0.2 s and a unit in combat could never be pulled out. Attack-move writes the same destination and order number and is told apart by `holdsFire` alone, so it still fights its way to the point. |
| **Interrupt and resume without code** | The Worker tree | FightBack sits above the long jobs. A hit sets `target` and `hasTarget`, the guard takes over and the Gather or Build branch is aborted mid-swing, its tween snapping the body back. When the target dies `UnitTargeting` drops `hasTarget`, the Selector falls through, and the job re-enters with the **same parameters**: the order facts were never cleared, and the load is a fact on the Worker, not state inside the tree. |
| **Numbered orders, three channels** | `PlayerCommands`, the Worker's three guards | A move writes `destination` and `orderNumber`, a gather writes `resource` and `gatherOrder`, a build writes `site` and `buildOrder`. Each branch completes **its own** number, so an order given on the frame a branch finishes is never lost. One Function, Click to move's `HasNewOrder`, guards all three channels. |
| **A branch that runs itself** | `Gather` | One trip per run. The branch decides what comes next at the end: the same node, the nearest node of the same kind, or done. Re-pointing writes the agent's `resource` fact, so the caller re-enters pointing at the new tree. |
| **Functions as swappable pieces** | `Functions/`, `Common/Functions/ApproachPoint` | `HasResource` reads a node's `remaining` fact, `IsCarryingSomething` the agent's `carriedAmount`, `HasQueuedUnit` a building's `queueCount`; all three are three-unit graphs. `ApproachPoint` is `Vector3.MoveTowards` between two positions, so a unit stands beside a thing instead of on it, and the same Function serves trees, rocks, sites and the depot. `NearestResource` is the one that is better in code, a loop over a list, wrapped so the tree can swap the policy. |
| **Kinds as data** | `UnitKinds/`, `BuildingKinds/`, `EnemyKinds/` | A unit kind is its prefab, weapon, tree, health, speed, sight, cost and training time; a building kind its prefab, costs, build time, footprint, what it trains and where it rallies. Every kind names the prefab to make, so a Worker, a Soldier, an Archer and a Knight each have their own Kenney character and a building its own shape, while the three fighters still share one `Fighter` tree and one `Engage`. Nothing is recoloured at runtime: a prefab keeps the material it was authored with. |
| **A tree on a building** | `Building`, `Grow`, `Spawn Unit` | While `isBuilt` is false the body grows with `progress01`, which the Workers' `Construct` swings write. While `HasQueuedUnit` is true the `Train` node works on the head unit for its training time, then Spawn Unit puts it at the rally point and Dequeue takes it off; the guard wakes on `queueCount`, so the first order starts the branch and the last Dequeue ends it. |
| **Two Workers build twice as fast** | `Construct` | Each swing adds one second of work. Nothing counts builders; the sum does. |
| **Attack-move is a move with Sight** | The Fighter tree | A right click on the ground is Click to move's `MoveTo`. `Sight` publishes what it sees on the way, Engage takes the slot, and when the target dies `MoveTo` resumes toward the same point with the same order. |
| **Camps that stay cleared** | `Enemy`, `Brute`, `RtsGame` | Combat's enemies with Defense's `Perish` instead of `Die`. `RtsGame` scans the roster once a second and the HUD shows Victory once no enemy is alive. |
| **Hitting back, shared** | `Retaliation`, `UnitTargeting` | Lifted out of Combat's `PlayerOrders` into `Common`: a hit with no target makes the attacker the target; a dead target is dropped. Workers, fighters, enemies and Combat's player all carry both. |
| **The selection decides the actions** | `ActionBar` | The bar along the bottom is rebuilt from whatever is selected: the buildings Workers can raise, or the units a building trains, each button greyed out while the Treasury cannot pay. A button calls the same `BuildingPlacer.BeginPlacing` and `TrainingCommands.Train` the sample has always used, so the UI is one more caller rather than a second way of doing things, and nothing reads the keyboard any more. |
| **A node that publishes its progress** | `Train`, `ProductionBar` | A `Wait` keeps its elapsed time to itself, so the Produce branch uses a `Train` node instead: it works on the head of the queue for that unit's training time and publishes `trainProgress01` as it goes. The bar over the building reads the queue, the Variable Watch reads the fact, and the tree still owns the timing. |
| **Placing with a ghost** | `BuildingPlacer`, `BuildingGhost` | The bar picks the kind; the ghost is a copy of that kind's prefab body with its colliders stripped, so it is exactly the building you are about to raise. It follows the cursor and turns red over a building or a resource, and a click raises the kind's prefab and orders the selected Workers to it. Selection and commands are switched off while placing so the click reaches only the placer. |

## The cast

| Piece | Role |
|---|---|
| `RtsUnit`, `UnitKind` | Applies a kind to a player unit on Awake through `CombatantKindApplier`, shared with Combat's `Enemy`. Publishes `depot`. |
| `Building`, `BuildingKind` | A kind names the prefab to raise and what it costs, trains and rallies at; the component sizes its footprint and carving obstacle from the kind, points its machine at the kind's tree, publishes `isBuilt` and `progress01`, and takes work from `Construct`. |
| `BuildingGhost` | The translucent copy that follows the cursor while placing, and the free-footprint test behind it. |
| `ProductionQueue` | What a building has been asked to train; publishes `queueCount`, `trainSeconds` and `nextKind`. |
| `ResourceNode`, `Resource` | A tree or a gold outcrop: kind, what is left (published as `remaining`), shrinks as it empties, hides when bare. `NearestLike` serves the Function. |
| `Carrier` | What a Worker holds, twenty of one kind at most; publishes `carriedKind`, `carriedAmount`, `isCarryingFull`. |
| `NavMeshPathLine` | On the four player units: draws the NavMesh path being walked as a line, and hides it on arrival. |
| `Depot`, `Treasury` | The Town Hall takes a carrier's load into the Treasury on the Game object, which publishes `wood` and `gold`. |
| `Selectable`, `SelectionRing`, `SelectionBox` | The roster of what can be selected, the ring under a selected unit or building, and the drag rectangle that owns the selection. A building's ring widens itself to its kind's footprint, so a Keep is ringed wider than a Barracks. |
| `PlayerCommands` | Right clicks become attack, build, gather or move orders for the selection, each numbered; the only writer of order facts. |
| `BuildingPlacer`, `TrainingCommands` | What the action bar calls: one raises a ghost and then a site, the other pays for a unit and queues it. Neither reads the keyboard. |
| `ActionBar`, `ActionButton` | The row of buttons, rebuilt whenever the selection changes and greyed out by what the Treasury holds. |
| `ProductionBar` | The bar over a training building, floating just above its roof whatever the building is. |
| `HudCursor` | Whether the pointer is over the HUD, so a click on the bar is not also a click on the world. |
| `CameraRig`, `RtsHud`, `RtsGame` | The pan, the readout and the win check. |
| `Retaliation`, `UnitTargeting` | In `Common`: hit back when there is no target, drop a dead one. |
| `Harvest`, `Deposit`, `Construct`, `Grow`, `Train`, `Spawn Unit`, `Dequeue` | The custom nodes this sample adds. |

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
