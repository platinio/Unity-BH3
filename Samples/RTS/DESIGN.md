# RTS sample design

A small real-time strategy demo for the BH3 Samples: gather wood and gold, raise buildings, train
soldiers, and clear the enemy camps from the map. It is the fourth sample after Click to move, Combat
and Defense, and it is built on the same shared code. This document is the brief for whoever builds it,
human or AI. Read it whole before writing anything; the rules in section 1 and the traps in section 12
are the parts that cost the most when skipped.

## 1. Rules of the sample work

- **Primitives, Kenney CC0 models and tweens only.** No animation clips, no Animator. Every motion is a
  tween through `Samples/Common/Tweening` (Hop, Punch Scale, Scale To, Flash Color, Charge, Swing Part,
  Recover). Kenney's Blocky Characters ship clips; ignore them.
- **Readability first.** Load the `clean-code:clean-code` and `unity-dev:arcane-onyx-toolbox` skills before
  writing C#, and `unity-dev:unity-environment` before the first Editor call. One component, one job. No
  comments that restate code. Names from this document are the names to use; the user's own names win.
- **Author trees and Functions with the BH3 authoring tools** (`bt_*`, `fn_*`, or `eval_file` scripts
  against `BehaviorTreeAuthoring` and `FunctionGraphAuthoring`), never by hand-editing `.asset` files.
  Run `bt_verify` on every tree and `fn_describe` on every Function before calling anything done.
- **No test cases for the demo.** Verify with `bt_verify`, `fn_describe`, a compile check and a scripted
  play probe that writes facts through `AgentVariableWriter` and reads them back (real mouse input does
  not reach an unfocused Editor).
- **Branching.** Work on a branch cut from `examples` in platinio/Unity-BH3 and open the PR against
  `examples`, never `main`. Commits are authored solely by James Roman, no attribution lines or trailers,
  staged by path. Editor side effects (material fixups, icon metas) stay out of commits.
- **All Unity calls target the main checkout** named in `.claude/unity-project.json`, never a worktree.
- **Where things go.** `Assets/ArcaneOnyx/BH3/Samples/RTS/` for everything RTS-only; anything a second
  sample could use goes to `Samples/Common/` (Gameplay, Tweening, Functions). A `README.md` in
  `Samples/RTS/` in the shape of `Samples/Combat/README.md`: the trees as ASCII, a "what each piece
  demonstrates" table, a cast table, notes.

## 2. What already exists, and what to reuse

Everything below lives on `examples` today. Reuse it; extend it where section 6 says so; copy its shape
for anything new.

| Piece | Where | What it gives the RTS |
|---|---|---|
| `Unit` | `Common/Unit.cs` | Body transform, renderers, named parts (`arm-right`, `head`, …), hand socket, resting pose. Every agent and prop with tween nodes has one. |
| `Health`, `Combatant`, `Weapon`, `WeaponDefinition`, `Sight`, `Knockback`, `Projectile` | `Common/Gameplay/` | Hit points with a `Damaged(attacker)` event, factions, held weapon with a visual in the hand, perception with line of sight, ranged attacks. |
| `Enemy`, `EnemyKind` | `Common/Gameplay/` | A kind asset applied on Awake: tree, weapon, health, speed, sight, keep-distance, body scale, bounty. The model for `UnitKind` and `BuildingKind`. |
| Tween nodes | `Common/Tweening/Nodes/` | Hop (walking), Punch Scale (idle breath, pop), Scale To (shrink, grow), Flash Color (hurt), Charge, Swing Part, Recover (attacks). All snap back if interrupted. |
| `Pickup`, `Pickups` and the `FunctionCall` pattern | `Combat/Pickup.cs`, `Common/Gameplay/Pickups.cs` | A component that runs Functions picked in the inspector (`canPickup`, `effect`) with a per-call `taker`. The model for anything configurable per object. |
| Functions | `Common/Functions/` | `PositionOf`, `TransformOf`, `IsTargetInRange`, `IsFarFrom`, `RetreatPointFrom`, `GiveWeapon`, `Heal`, `CanEquipWeapon`, `IsHurt`. |
| Combat trees | `Combat/Trees/` | `Engage` (kite, attack with held weapon, chase), `SwordAttack`, `HammerAttack`, `BowAttack`, `Die`, `Chase`, `Kite`, `ReturnHome`, `Wander`, `Enemy`, `Brute`. The enemy side of the RTS runs these unchanged. |
| Click to move trees | `ClickToMove/Trees/` | `MoveTo` with numbered orders (a trip completes only its own order), `Idle`, the `HasNewOrder` Function. |
| Defense pieces | `Defense/` | `Spawner` (spawn an inactive prefab, apply a kind, activate), `DefenseGame` (points, lives, facts), `DefenseHud` (uGUI text in Screen Space Camera), `BuildPad` + `TurretBuilder` (keys pick a kind, click places it), `TurretKind`. |
| Facts | `Common/Gameplay/CombatFacts.cs`, `ClickToMove/ClickToMoveFacts.cs` | `target`, `hasTarget`, `isDead`, `health01`, `attackTree`, `attackRange`, `attackCooldown`, `keepDistance`, `home`, `canPickup`, `slowed`; `destination`, `orderNumber`, `completedOrder`. |
| Core nodes worth knowing | BH3 `Runtime/Nodes` | Wait Until, Cooldown, Parallel Sequence / Selector, Repeater, Random Chance, Wait Range, Set Variable, Set Nav Agent Position, Wait Until Reach Nav Target Position, Generate Random NavMesh Position, Stop Nav Agent, Face Target, Run Behavior Tree Graph. |

The RTS adds no new core BH3 node unless section 9 lists it. Everything else is a sample node under
`Samples/RTS/Nodes/` or `Samples/Common/…/Nodes/`.

## 3. The game

**Loop.** The player starts with a Town Hall, three Workers, a little wood and gold, and a map with
forests, gold rocks and three enemy camps. Workers gather wood and gold and bring it to the Town Hall.
Wood raises buildings: a Barracks trains Soldiers and Archers for gold; a Keep trains Knights for gold
and wood. Combat units attack-move across the map and clear the camps. **Win** when no enemy
combatant is alive; a banner says so and the game keeps running. **Lose** is not modelled: the Town Hall
cannot be destroyed and Workers respawn at it after a few seconds, like Combat's respawn.

**Automation is the point.** An order is given once and runs until it is finished or replaced: a
Worker sent to a tree gathers wood, walks it home, walks back, and keeps going; when the tree is gone it
moves to the nearest tree of the same kind; it only stops when told to do something else or when nothing
is left. A Worker sent to a build site builds until the building stands, then goes idle. A combat unit
told to attack-move walks toward the point and engages anything it sees on the way, then idles there.
Any unit that is hit while doing something else fights back and then **resumes what it was doing**:
that is the showcase, and it falls out of the tree shape in section 7 rather than out of code.

**Enemies.** Combat's grunts, archers and brutes, as `EnemyKind` assets, in three camps. They wander
around a camp centre, attack what their `Sight` sees, and `ReturnHome` keeps them leashed within twelve
metres, exactly as in Combat. They do not build, train or attack buildings.

## 4. Map and art

- **Size.** About 60 by 60 metres of flat ground on a baked NavMesh (a `NavMeshSurface`, baked by the
  builder as Click to move and Defense do; the bake asset is renamed after baking, see traps). The Town
  Hall sits near one corner, the three camps near the other three. Two forests and two gold outcrops lie
  between, each outcrop nearer to a camp than to the Town Hall so expansion meets resistance.
- **Camera.** Combat's angle (about 50 degrees down) on a `CameraRig` component: pan with WASD and with
  the mouse at the screen edge, clamped to the map bounds, speed as a serialized field. No zoom.
- **Models**, all Kenney CC0, placed under `Samples/Common/Kenney/<Pack>/` the way Blocky Characters
  and Mini Dungeon already are. Commit only the files a scene or prefab references, with their `.meta`
  files, as the earlier samples did.
  - Blocky Characters (already present): units. Worker = character-a, Soldier = character-b,
    Archer = character-c, Knight = character-e; enemies keep Combat's p, r and o.
  - Mini Dungeon (already present): gold outcrops from `rocks` and `coin`, chests are not used here.
  - Castle pack (to import): Town Hall from a tower piece, Barracks from a wall-and-gate piece, Keep from
    a larger tower. Whichever pieces are picked, a building is one prefab with a `Unit` whose body is the
    model, so Scale To can grow it during construction.
  - Mini Forest pack (to import): trees for wood. If the pack as named is not available, any Kenney
    nature or tree kit with a CC0 licence does; the design needs a tree model and nothing else from it.
- **Facing.** Kenney blocky characters face +Z. The hand socket, part names and swing angles from
  Combat apply unchanged.

## 5. Player control

Input System, as in the other samples (`Mouse.current`, `Keyboard.current`).

- **Selection.** Left press-and-drag draws a screen-space rectangle; on release every player `Selectable`
  whose screen position is inside it is selected. A left click without a drag selects the one
  `Selectable` under the cursor, or clears the selection on empty ground. Buildings are selectable too,
  one at a time; selecting a building deselects units.
- **Selection feedback.** A flat ring primitive (a thin cylinder) under each selected unit, toggled by
  a `SelectionRing` component. No outline shaders.
- **Commands** go to every selected unit with a right click, and the meaning comes from what is under the
  cursor, in this order: an enemy `Combatant` → attack; a `ResourceNode` → gather (Workers only, combat
  units ignore it); a `BuildSite` → build (Workers only); a player building → nothing; ground → move
  (Workers) or attack-move (combat units). Holding Shift while right-clicking ground makes Workers
  attack-move as well, so they can be sent through danger deliberately.
- **Building placement.** With one or more Workers selected, keys 1 (Barracks) and 2 (Keep) pick a
  `BuildingKind`; a translucent ghost of its model follows the cursor on the ground; left click places a
  `BuildSite` if the kind is affordable and the footprint is free of other buildings and resource nodes,
  spends the wood, and orders the selected Workers to build it. Escape or right click cancels placement.
- **Training.** With a building selected, keys 1 and 2 enqueue the units that building trains, if the
  Treasury can pay; the HUD shows the queue length. Nothing is refunded; nothing is cancelled.
- **HUD.** Top left: wood, gold, selected summary ("3 Workers", "Barracks, queue 2"). Top centre: the
  placement or training hint for the current selection. Centre: "Victory" when the last enemy dies.
  uGUI `Text` on a Screen Space Camera canvas, as `DefenseHud` does.

Everything the player does ends as a **fact written on the unit** through `AgentVariableWriter`; the
trees react to facts, never to input. `PlayerCommands` (the input component) is the only writer of order
facts, the way `PlayerOrders` is in Combat.

## 6. Data

Kinds are assets, like `EnemyKind`. Numbers below are starting values for the builder to tune in play.

**`UnitKind`** (`Samples/RTS/UnitKinds/`): body prefab (Kenney character), tree, weapon, max health,
speed, sight radius, keep distance, body scale, gold cost, wood cost, train seconds. A `Unit`
applier component `RtsUnit` applies it on Awake exactly as `Enemy` applies `EnemyKind`; the two share
their shape and may share code (section 9).

| Kind | Body | Weapon | Health | Speed | Cost | Trained at | Train s |
|---|---|---|---|---|---|---|---|
| Worker | character-a | none (fists: Strike with a 5-damage `Fists` definition, range 1.5) | 60 | 3.8 | 50 gold | Town Hall | 6 |
| Soldier | character-b | Sword | 120 | 3.6 | 80 gold | Barracks | 8 |
| Archer | character-c | Bow | 80 | 3.8 | 100 gold | Barracks | 10 |
| Knight | character-e, body scale 1.3 | Hammer | 250 | 3.0 | 150 gold + 50 wood | Keep | 15 |

**`BuildingKind`** (`Samples/RTS/BuildingKinds/`): model prefab, tree, wood cost, gold cost, build
seconds, footprint radius, the `UnitKind`s it trains (in key order), rally offset.

| Kind | Cost | Build s | Trains |
|---|---|---|---|
| Town Hall | pre-placed, cannot be built | — | Worker |
| Barracks | 100 wood | 20 | Soldier, Archer |
| Keep | 200 wood, 50 gold | 30 | Knight |

**`ResourceKind`**: an enum `Resource { Wood, Gold }` is enough; a `ResourceNode` carries the kind, a
starting amount (tree 60, outcrop 150), a harvest amount per swing (5), and a swing cooldown (1 s).
A Worker carries at most 20; the depot is any building with a `Depot` component, which only the Town
Hall has.

**Starting treasury**: 100 wood, 150 gold, on the `Treasury` component of the `Game` object.

**Enemy camps**: three `EnemyKind` assets reused from Combat; camp A two grunts, camp B two grunts and
an archer, camp C a brute, an archer and a grunt. Each enemy's `home` is its spawn position, as today.

## 7. Trees

Selectors run left to right by priority; a Reactive Guard on a higher child takes the slot from a
running lower one when its value turns true, which is how every "interrupt and resume" below works
without any code. Trees shared with Combat are the same assets.

```
Worker
└─ Repeater → Selector
   ├─ Die          RG: isDead                                   (Combat's Die, with Respawn)
   ├─ FightBack    RG: hasTarget                                (Engage from Combat, target from Health.Damaged)
   ├─ Build        RG: hasBuildOrder                            (sub-tree, Required: site, acceptedBuildOrder)
   ├─ Gather       RG: hasGatherOrder                           (sub-tree, Required: resource, acceptedGatherOrder)
   ├─ MoveTo       RG: HasNewOrder(orderNumber, completedOrder) (Click to move's MoveTo)
   └─ Idle                                                      (Click to move's Idle)

Soldier / Archer / Knight
└─ Repeater → Selector
   ├─ Die          RG: isDead
   ├─ Engage       RG: hasTarget                                (Combat's Engage; target from Sight or from a click)
   ├─ AttackMove   RG: HasNewOrder(orderNumber, completedOrder) (sub-tree below)
   └─ Idle

Gather (resource, acceptedGatherOrder)
└─ Repeater → Sequence
   ├─ Set Nav Agent Position(PositionOf(resource)) → Wait Until Reach (in parallel with Repeater → Hop)
   ├─ Repeater(until Failure) → Sequence [ Cooldown(harvestCooldown) → Face Target(resource) → Swing Part(arm-right) → Harvest(resource) ]
   │     Harvest fails when the Worker is full or the node is empty; failing ends the loop
   ├─ Selector [ IsCarryingSomething → ReturnToDepot ; Succeed ]
   │     ReturnToDepot: Set Nav Agent Position(PositionOf(depot)) → Wait Until Reach → Deposit
   └─ Selector [ node still has resource → Succeed ; NearestResource(resource kind) found → Set Variable resource ; Finish Gather Order ]
         Finish Gather Order writes completedGatherOrder = acceptedGatherOrder, which drops hasGatherOrder

Build (site, acceptedBuildOrder)
└─ Sequence
   ├─ Set Nav Agent Position(PositionOf(site)) → Wait Until Reach (with Hop)
   ├─ Repeater(until Failure) → Sequence [ Cooldown(0.8) → Face Target(site) → Swing Part(arm-right) → Construct(site) ]
   │     Construct adds progress to the site and fails once it is complete or gone
   └─ Finish Build Order

AttackMove (destination, acceptedOrder)
└─ Parallel Selector
   ├─ Sequence [ Set Nav Agent Position(destination) → Wait Until Reach → Set Variable completedOrder = acceptedOrder ]
   └─ Repeater → Hop
   (Engage sits above AttackMove in the root; Sight publishes a target on the way, Engage takes over, and when
    the target dies hasTarget drops and AttackMove resumes toward the same destination with the same order)

Building
└─ Repeater → Selector
   ├─ UnderConstruction  RG: not isBuilt    → Scale To(progress01) each tick via a Grow node, Wait 0.1
   ├─ Produce            RG: queueCount > 0 → Wait(trainSeconds) → Spawn Unit → Dequeue
   └─ Wait 0.2

Enemy, Brute, Engage, Chase, Kite, ReturnHome, Wander, Die, the three attack trees: Combat's, unchanged.
```

**Why it resumes.** FightBack and Engage sit above the long jobs. A hit sets `target` and `hasTarget`
(section 8), the guard takes over, the Gather or Build sub-tree is aborted mid-way, and its tween nodes
snap the body back on exit. When the target dies `hasTarget` turns false, the Selector falls through to
the next true guard, and Gather or Build re-enters from the top with the **same parameters**, because the
order facts were never cleared. A Worker that was carrying ten wood still carries it: `carriedAmount` is a
fact on the Worker, not state inside the tree.

**Orders are numbered**, as in Click to move: `PlayerCommands` bumps `gatherOrder` or `buildOrder` or
`orderNumber` and the sub-tree finishes its **own** order by writing `completedGatherOrder =
acceptedGatherOrder`. Never clear a shared "has order" flag from inside a branch; a same-tick new order
would be lost. `hasGatherOrder` is therefore a Function `OrdersDiffer(gatherOrder, completedGatherOrder)`,
not a written fact, exactly like `HasNewOrder`.

## 8. Facts

`RtsFacts` holds the constants; the Combat and Click to move facts stay where they are.

| Fact | Written by | Read by |
|---|---|---|
| `destination`, `orderNumber`, `completedOrder` | `PlayerCommands`, MoveTo / AttackMove | MoveTo, AttackMove, HasNewOrder |
| `target`, `hasTarget` | `Sight` (combat units), `Retaliation` on hit, `PlayerCommands` on an attack click; cleared when the target dies by `UnitTargeting` | Engage, FightBack |
| `resource`, `gatherOrder`, `completedGatherOrder` | `PlayerCommands`; Gather sub-tree completes its order and may re-point `resource` | Gather, OrdersDiffer guard |
| `site`, `buildOrder`, `completedBuildOrder` | `PlayerCommands`; Build completes | Build |
| `carriedKind`, `carriedAmount`, `isCarryingFull` | `Carrier` component (Harvest and Deposit nodes call it) | Gather, HUD |
| `depot` | `RtsUnit` on Awake (nearest `Depot`) | Gather |
| `home` | `Combatant` | ReturnHome (enemies) |
| `isBuilt`, `progress01` | `BuildSite` → `Building` | Building tree |
| `queueCount`, `trainSeconds`, `nextKind` | `ProductionQueue` | Building tree |
| `wood`, `gold` | `Treasury` (on the Game object) | HUD; `CanAfford` Functions |

Facts a guard may read before anyone has written them (`completedGatherOrder` on a fresh Worker,
`queueCount` on a new building) go through variable-read Functions with a fallback, as every sample does;
a bare `GetVariable` of Object kind throws on a missing variable.

## 9. Components and nodes to add

Components, one job each, in `Samples/RTS/` unless marked Common:

- `RtsUnit` + `UnitKind`: the applier (copy `Enemy`/`EnemyKind`; consider extracting the shared part
  into `Common/Gameplay/KindApplier` only if the two end up identical).
- `Carrier`: what a Worker holds; `Take(kind, amount)`, `Empty()`, publishes the three carry facts.
- `ResourceNode`: kind, remaining, `Harvest(amount)` returns what it gave; shrinks with Scale To as it
  empties and destroys itself at zero. Static `All` list for `NearestResource`.
- `Depot`: a marker on the Town Hall; `Treasury.Add` on deposit.
- `Treasury`: wood and gold, `CanAfford`, `Spend`, `Add`, publishes facts, on the Game object.
- `BuildSite` → `Building`: a site carries the kind and progress; at complete it swaps to the built
  state (same object, `isBuilt` fact flips). `ProductionQueue` on buildings that train.
- `Selectable`, `SelectionRing`, `SelectionBox` (the drag rectangle, as a uGUI image), `PlayerCommands`
  (right-click meanings and fact writes), `BuildingPlacer` (ghost, footprint check, spend, order),
  `CameraRig`, `RtsHud`, `RtsGame` (win check: a `Combatant.All` scan every second for a living enemy).
- `Retaliation` (Common): listens to `Health.Damaged` on its own unit and writes `target`/`hasTarget`
  when there is no target, for player units of any kind. Combat's `PlayerOrders` already does this for
  one unit; lift that behaviour out so both samples use it, and keep `PlayerOrders` thin.
- `UnitTargeting` (Common): drops `hasTarget` when the target dies, the loop `PlayerOrders` runs today.

Sample nodes, each a `GameplayNode` with ports and a one-line description, in `Samples/RTS/Nodes/`:
`Harvest(Resource)`, `Deposit`, `Construct(Site)`, `Finish Gather Order`, `Finish Build Order`,
`Grow` (Scale To driven by `progress01`), `Spawn Unit`, `Dequeue`. Functions: `OrdersDiffer` exists as
`HasNewOrder` in Click to move; add `NearestResource(kind)` (pure, returns the nearest `ResourceNode`
GameObject with remaining > 0 or null), `HasResource(node)`, `IsCarryingSomething`, `CanAfford(kind)`.

No new core BH3 node is expected. If the builder finds one is needed, it is a separate PR to `examples`
with its own tests and a node-reference row, like Wait Until was.

## 10. Scene composition

- `Samples/RTS/RTS.unity`, `Samples/RTS/NavMesh-RTS.asset`. One `Game` object: `Treasury`, `RtsGame`,
  `RtsHud` (its canvas), `PlayerCommands`, `BuildingPlacer`, `SelectionBox`. One `CameraRig` on the
  camera.
- Prefabs under `Samples/RTS/Prefabs/`: `Worker`, `Soldier`, `Archer`, `Knight` (or one `PlayerUnit`
  prefab dressed by `UnitKind`, which is what `Enemy` does; prefer one prefab per side), `Enemy` (reuse
  the Combat enemy shape), `TownHall`, `Barracks`, `Keep`, `Tree`, `GoldOutcrop`, `BuildGhost`.
- **Spawned agents are saved inactive**: `Instantiate` → apply kind → `SetActive(true)`, because the
  machine starts its tree in Awake. Appliers carry `[DefaultExecutionOrder(-50)]`.
- Each agent: `NavMeshAgent`, `Variables`, `AgentVariableWriter`, `Health`, `Combatant`, `Weapon`,
  `Knockback`, `Unit`, `Sight` (combat units and enemies), `Selectable` (player side), `RtsUnit` or
  `Enemy`, `BehaviorTreeMachine`, a `HealthBar` child and a Kenney body with a capsule collider on the
  model root and the health bar at 3.1.
- Buildings: `Unit` (body = model), `Variables`, `AgentVariableWriter`, `BuildSite`/`Building`,
  `ProductionQueue` where it trains, `Depot` on the Town Hall, `Selectable`, a collider (NavMesh
  obstacle carving, placed off the path), `BehaviorTreeMachine` with the Building tree.

## 11. Build order for the builder

Each phase ends compiled, `bt_verify` clean, and probed in play by script. Commit per phase on the
branch; one PR at the end, or one per phase if the user asks.

1. **Ground, camera, selection.** Map, NavMesh, `CameraRig`, `Selectable`, `SelectionBox`,
   `SelectionRing`, a Worker prefab reusing Combat's pieces with the Worker tree as Die + MoveTo + Idle.
   Probe: write `destination`/`orderNumber`, see the Worker arrive.
2. **Economy.** `ResourceNode`, `Carrier`, `Depot`, `Treasury`, the Gather sub-tree and its nodes, the
   Worker tree with Gather above MoveTo. Probe: write a gather order, watch wood rise, empty a tree and
   see the Worker move to the next one, then stop when none is left.
3. **Buildings.** `BuildingKind`, `BuildSite`/`Building`, `Construct`, `Grow`, the Build sub-tree,
   `BuildingPlacer`. Probe: place a Barracks, order a Worker, watch progress reach 1 and `isBuilt` flip.
4. **Production.** `ProductionQueue`, `Spawn Unit`, `Dequeue`, the Building tree, `UnitKind` for the
   three combat units, their trees (Engage + AttackMove + Idle). Probe: enqueue a Soldier, see it spawn
   at the rally offset and respond to an attack-move.
5. **Enemies and victory.** The three camps from Combat kinds, `Retaliation` and `UnitTargeting` lifted
   to Common and used by Combat too, `RtsGame` win check, the HUD banner. Probe: kill every enemy by
   script, see the banner.
6. **Resume behaviour.** The showcase: a gathering Worker hit by a grunt fights back and returns to the
   same tree with the same load; a building Worker does the same. Probe by writing `target` mid-gather.
7. **README and polish.** Tune numbers in play, write `Samples/RTS/README.md`, add the sample to the
   Samples overview if one exists.

## 12. Traps already paid for

These cost real time on the earlier samples. Each has a memory note or a GitHub issue behind it.

- **Regenerating a tree over an existing asset strands every loaded referrer** (parent trees, open
  scenes, prefabs holding a machine): `CreateTree` destroys the old object and in-memory references turn
  into "Object reference … does not match target type … Defaulting to null". Files on disk stay right.
  After regenerating, reimport the prefabs that reference the tree, `Resources.UnloadAsset` the parent
  trees, `EditorUtility.UnloadUnusedAssetsImmediate()`, and reopen the scene. Tracked as
  platinio/Unity-BH3#149; prefer `LoadTree` and edit in place when the change is small.
- **A spawned prefab's machine awakens before any code can set its tree.** Save prefabs inactive.
- **Bare `GetVariable` throws on a missing agent variable.** Guard inputs that may be read before the
  first write go through `CreateVariableReadFunction(asset, name, fallback)`.
- **Never clear a shared flag from inside a branch.** Number the orders; a branch completes its own.
- **Tween nodes must snap back when interrupted**, or an abort leaves a body frozen. The existing nodes
  do; new ones follow the same `wasInterrupted` shape in `OnExit`.
- **`FunctionCall` fields on a component added with `AddComponent` are null until serialized**; create
  them by reflection when wiring from a script. Load freshly created Function assets after `OpenScene`,
  or the handle comes back destroyed.
- **Simulated input never reaches `wasPressedThisFrame` in an unfocused Editor.** Probe by writing facts.
- **`NavMeshSurface` baking is asynchronous and writes `NavMesh-<Scene> 1.asset`**: delete the old asset
  and rename the new one. Keep spawn points off any carved collider.
- **The project is the built-in render pipeline**; Kenney's Standard materials render as they are.
- **`Face Target` needs `RotationSpeed` and `AcceptableRotation` set**; the defaults are zero and it
  never turns. 720 and 0.98 are the values Combat uses.

## 13. Decisions taken for this document

| Question | Decision |
|---|---|
| Resources | Two: wood from trees, gold from outcrops. Buildings cost wood, units cost gold, the Keep and the Knight cost both. |
| Control | Drag-box multi-select, right-click context commands, keys 1 and 2 for buildings and for training. |
| Enemies | Camps of Combat's kinds with a leash. No enemy economy, no attacks on buildings. |
| Camera | Scrolling top-down, WASD and edge pan, clamped. |
| Lose condition | None; Workers respawn at the Town Hall, the Town Hall is indestructible. |
| After a build finishes | The Worker goes idle. Resuming a previous gather is a possible later touch, not in scope. |
| Worker combat | Fists, weak, retaliation only; attack-move needs Shift. |
| Where the document lives | `Samples/RTS/DESIGN.md` on `examples`, beside the sample it describes; `docs/_design` is git-ignored on purpose. |
