# Defense

A winding path from a purple gate to your blue base. Enemies walk it in waves; each one that reaches the
base costs a life. White pads beside the path are where turrets go: press **1**, **2** or **3** to pick
Arrow, Cannon or Frost, then click a pad. Killing enemies earns points, turrets cost them. Lose all ten
lives and the board resets to wave 1. Primitives and tweens only.

Open `Defense.unity` and press Play.

## The trees

```
Turret                                      Walker
└─ Repeater → Selector                      └─ Repeater → Selector
   ├─ Shoot   RG: hasTarget                    ├─ Perish      RG: isDead
   │   └─ ArrowShot (dynamic)   Tree <- attackTree
   └─ Idle                                     ├─ Breach      RG: reachedEnd
                                               └─ FollowPath

ArrowShot / CannonShot / FrostShot (target):  Cooldown(attackCooldown) → [ a tween → Strike(target) → … ]
FollowPath:  Sequence [ WalkTo(nextWaypoint) → Advance Waypoint ]
Perish:      Flash Color → Scale To 0 → Destroy Object(This)
Breach:      Breach Base → Destroy Object(This)

WaveDirector (on the Game object)
└─ Repeater → Sequence: Wait 5 → Spawn Wave → Wait 12 → Next Wave
```

`Idle` comes from Click to move. `Health`, `Weapon`, `Sight`, `Projectile` and the rest of
the gameplay components are the Combat sample's, now in `Samples/Common`.

## What each piece demonstrates

| Feature | Where | What to watch |
|---|---|---|
| **A tree running a game system** | `WaveDirector` | The spawner is a behavior tree: breathe, spawn this wave's enemies one by one, let them walk, bump the wave. Spawn Wave grows the count with the wave number. |
| **Combat's mechanism, a different game** | `Turret` | A turret cannot move, so there is no Engage: while `Sight` publishes a target, run the tree the held weapon names, through the same `attackTree` fact Combat's Engage reads. The weapon's range is the turret's sight radius. |
| **The weapon owns its shot** | `ArrowShot`, `CannonShot`, `FrostShot` | Three rhythms in three trees. The data does the rest: the Cannon asset carries knockback that shoves walkers back down the path, the Frost asset carries a two-second slow. |
| **Path following as a branch** | `FollowPath` | One leg at a time: walk to `nextWaypoint`, hop, advance. The `Walker` component publishes the waypoint; the tree never sees the route. |
| **Takeover on a published fact** | `Breach`, `Perish` | `reachedEnd` and `isDead` are written by components; the guards wake on the key and take over the walk. |
| **A status effect without a status system** | `Slowed` | Frost writes `slowed` on the walker and halves its speed; the fact is visible in the Variable Watch while it lasts. |
| **Kinds as data again** | `EnemyKinds/Runner`, `Brute`, `Swarmer`, `TurretKinds/Arrow`, `Cannon`, `Frost` | Health, speed, size, bounty and tree per enemy kind; weapon, cost and tree per turret kind. The `Enemy` and `Turret` components apply them when the prefab is spawned. |
| **Spawning an agent with a tree** | `Spawner`, `BuildPad` | The prefabs are saved inactive: apply the kind, then activate, so the machine awakens with its tree already set. |

## The cast

| Piece | Role |
|---|---|
| `DefenseGame` | Points, lives and wave; resets the board when lives run out. Publishes all three as facts. |
| `Spawner` | Which kind comes next, and `Spawn()`; the WaveDirector tree calls it. |
| `Walker` | Follows a `PathRoute`, publishes `nextWaypoint` and `reachedEnd`, pays its bounty when it dies, costs a life when it breaches. |
| `PathRoute` | The waypoints, as child transforms in order. |
| `Turret`, `TurretKind` | Applies a kind to a spawned turret. |
| `BuildPad`, `TurretBuilder` | One build spot each; the builder turns keys and clicks into builds. |
| `Slowed` | Halves a NavMeshAgent's speed for a while; `WeaponDefinition.Land` applies it. |
| `DefenseHud` | The readout at the top left. |
| `Spawn Wave`, `Next Wave`, `Advance Waypoint`, `Breach Base` | The four custom nodes this sample adds. |

## Notes

- Facts read before anyone publishes them (`attackCooldown`, `nextWaypoint`) go through
  variable-read Functions with a fallback.
- Every tunable is a port default on a canvas, a field on a `WeaponDefinition`, `EnemyKind` or `TurretKind`
  asset, or a NavMeshAgent setting. Wave size lives in `Spawner.WaveSize`.
