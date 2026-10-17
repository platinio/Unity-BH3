# Combat

A small arena. Your blue capsule starts at the bottom between two chests. Left-click the ground to move,
left-click an enemy to attack it: your unit chases until in range, stops, and strikes on a cooldown until
the target dies. Three kinds of enemy roam the far side. Red **grunts** charge with swords, the orange
**archer** shoots and keeps its distance, the big dark **brute** holds its ground and swings a hammer that
knocks you back. Walk into a chest to take its weapon: sword, bow, or the brute's hammer. Whoever dies
flashes, shrinks away and respawns a couple of seconds later, so the fight never ends. Primitives and tweens
only.

Open `Combat.unity` and press Play.

## The trees

```
Enemy (grunt, archer)                    Brute                                Player
└─ Repeater → Selector                   └─ Repeater → Selector               └─ Repeater → Selector
   ├─ Die         RG: isDead                ├─ Die      RG: isDead               ├─ Die      RG: isDead
   ├─ ReturnHome  RG: IsFarFrom(home, 12)   ├─ Engage   RG: hasTarget            ├─ Engage   RG: hasTarget
   ├─ Engage      RG: hasTarget             └─ Idle                              ├─ MoveTo   RG: OrdersDiffer
   └─ Wander                                                                      └─ Idle

Engage   (Required: target)
└─ Repeater → Selector
   ├─ Kite      RG: IsTargetInRange(target, keepDistance), every 0.1 s
   ├─ Attack    RG: IsTargetInRange(target, attackRange), every 0.1 s, Stops Its Own Branch OFF
   │   └─ Selector [ SwordAttack (RG HoldsWeapon "Sword") | HammerAttack ("Hammer") | BowAttack ("Bow") ]
   └─ Chase     RG: not IsTargetInRange(target, attackRange), every 0.1 s

SwordAttack / HammerAttack (target):  Stop Nav Agent → Cooldown(attackCooldown) → [ Charge(target) → Strike(target) → Recover ]
BowAttack (target):                   Stop Nav Agent → Cooldown(attackCooldown) → [ Punch Scale → Strike(target) → Hop ]
Chase / Kite / ReturnHome:            Parallel Selector [ Set Nav Agent Position(…) → wait | Repeater → Hop ]
Wander:                               random NavMesh point, hop there, linger 1–3 s
Die:                                  Flash Color → Scale To 0 → Wait 2 → Respawn

Chest
└─ Repeater → Selector
   ├─ HandOut   RG: playerNearby, Stops Its Own Branch OFF
   │   └─ Punch Scale → Give Weapon → Scale To 0.5 → Wait 20 → Scale To 1
   └─ Wait 0.2
```

`Die`, `Engage`, `Chase`, `Kite` and the attack trees are the same assets on every combatant. `MoveTo` and
`Idle` come from the Click to move sample.

## What each piece demonstrates

| Feature | Where | What to watch |
|---|---|---|
| **Enemies as data** | `EnemyKinds/Grunt`, `Archer`, `Brute` | Colour, weapon, health, speed, sight, keep-distance, body size and the root tree, one asset each. The `Enemy` component applies it on Awake. Grunt and archer run the same tree; the brute runs its own. |
| **One branch, every agent** | `Engage` | The player and all three kinds run the same asset. The player's `target` comes from a click, an enemy's from `Sight`; the archer kites because its kind publishes `keepDistance` 3, nobody else does. |
| **The weapon owns its attack** | `SwordAttack`, `HammerAttack`, `BowAttack` | Each weapon's rhythm lives in its own tree. Engage picks one with a `HoldsWeapon` guard whose `wanted` port is an inline literal; take a different weapon from a chest and the guard swaps trees on the `weaponId` key. |
| **Charge** | `Charge`, `Recover` | The body pivots at its feet, leans away from the target, slams into it; Strike lands at the slam; Recover eases it upright. The hammer uses the same two nodes with a longer lean and a slower recovery. |
| **Committed swing** | Attack's guard, Stops Its Own Branch off | A started charge finishes even if the target steps out of range during the wind-up. Chase and Kite carry the opposite guards. |
| **Time-triggered guards** | The three `IsTargetInRange` guards, `IsFarFrom` | Distance changes without any variable write, so these poll. Every other guard in the samples wakes on a key. |
| **A leash** | `ReturnHome` above `Engage` | Stray more than twelve metres from home, mid-fight or not, and the enemy walks back. It is why the grunts that killed you do not camp your spawn. |
| **Top-priority takeover** | `Die` on every root | `isDead` flips and whatever was running is abandoned the same frame. |
| **A tree on a box** | `Chest` | A chest has a machine too: wait for the player, pop, hand over the weapon, shrink while empty, refill. The hand-out keeps Stops Its Own Branch off so it finishes even if you walk away. |
| **Functions reading objects** | `PositionOf`, `IsTargetInRange`, `IsFarFrom`, `RetreatPointFrom` | Pure graphs with GameObject and Vector3 inputs, reading transforms through member units. |

## The cast

| Piece | Role |
|---|---|
| `Combatant` | Faction, spawn point, the live roster, `Respawn()`. Publishes `home`. |
| `Enemy`, `EnemyKind` | Applies a kind's data to an enemy on Awake. |
| `Health` | Hit points, the red hurt flash; publishes `isDead` and `health01`. |
| `Weapon`, `WeaponDefinition` | The held weapon and its asset (range, cooldown, damage, knockback, projectile). Publishes `weaponId`, `attackRange`, `attackCooldown`. |
| `Knockback` | Shoves a NavMeshAgent along the mesh; the hammer uses it. |
| `Projectile` | The bow's bolt. |
| `Sight` | Enemy perception: radius plus a line-of-sight ray, every 0.2 s. |
| `Chest` | Publishes `playerNearby`; `GiveWeapon()` is what the chest tree calls. |
| `PlayerOrders` | Clicks become move or attack orders; an attack order cancels a pending move; death forgets both. |
| `HealthBar`, `Unit` | A bar over each head; the body and renderers the tween nodes and the hurt flash act on, with the resting pose that Charge, Recover and Respawn return to. |
| `Strike`, `Respawn`, `Give Weapon`, `Scale To`, `Charge`, `Recover` | The custom nodes this sample adds. |

## Notes

- Facts a guard may read before anyone has published them (`attackRange`, `attackCooldown`, `weaponId`,
  `keepDistance`, `home`) go through variable-read Functions with a fallback.
- Every tunable is a port default on a canvas, a field on a `WeaponDefinition` or `EnemyKind` asset, or a
  NavMeshAgent setting.
