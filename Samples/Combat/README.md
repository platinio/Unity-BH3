# Combat

A small arena. Your character starts at the bottom between two chests. Left-click the ground to move,
left-click an enemy to attack it: your unit chases until in range, stops, and strikes on a cooldown until
the target dies. Three kinds of enemy roam the far side. **Grunts** swing swords, the
**archer** throws from a distance and keeps it, the big **brute** holds its ground and swings a hammer that
knocks you back. Walk into a chest to take its weapon, sword or hammer, and over a potion to heal. Whoever dies
flashes, shrinks away and respawns a couple of seconds later, so the fight never ends. Kenney blocky
characters, primitives and tweens; every animation is a tween on a body part.

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

SwordAttack (target):   Stop Nav Agent → Cooldown(attackCooldown) → [ Face Target → Parallel Sequence [ Charge(target) | Swing Part(arm-right up) → Swing Part(arm-right down) ] → Strike(target) → Recover ]
HammerAttack (target):  Stop Nav Agent → Cooldown(attackCooldown) → [ Face Target → Parallel Sequence [ both arms up ] → Parallel Sequence [ both arms down | Charge(target) ] → Strike(target) → Recover ]
BowAttack (target):     Stop Nav Agent → Cooldown(attackCooldown) → [ Face Target → Swing Part(arm-left aims) → Swing Part(arm-right draws) → Strike(target) → Recover ]
Chase / Kite / ReturnHome:            Parallel Selector [ Set Nav Agent Position(…) → wait | Repeater → Hop ]
Wander:                               random NavMesh point, hop there, linger 1–3 s
Die:                                  Flash Color → Scale To 0 → Wait 2 → Respawn

Pickup (chests and health packs)
└─ Repeater → Selector
   ├─ HandOut   RG: playerNearby, Stops Its Own Branch OFF
   │   └─ Punch Scale → Apply Pickup → Scale To 0.5 → Wait 20 → Scale To 1
   └─ Wait 0.2
```

`Die`, `Engage`, `Chase`, `Kite` and the attack trees are the same assets on every combatant. `MoveTo` and
`Idle` come from the Click to move sample.

## What each piece demonstrates

| Feature | Where | What to watch |
|---|---|---|
| **Enemies as data** | `EnemyKinds/Grunt`, `Archer`, `Brute` | Weapon, health, speed, sight, keep-distance, body size and the root tree, one asset each. The `Enemy` component applies it on Awake. Grunt and archer run the same tree; the brute runs its own. |
| **One branch, every agent** | `Engage` | The player and all three kinds run the same asset. The player's `target` comes from a click, an enemy's from `Sight`; the archer kites because its kind publishes `keepDistance` 3, nobody else does. |
| **The weapon owns its attack** | `SwordAttack`, `HammerAttack`, `BowAttack` | Each weapon's rhythm lives in its own tree. Engage picks one with a `HoldsWeapon` guard whose `wanted` port is an inline literal; take a different weapon from a chest and the guard swaps trees on the `weaponId` key. |
| **Swings on body parts** | `Swing Part`, `Charge`, `Recover` | A Kenney character is six parts. Swing Part turns one about its own pivot and leaves it there, Charge leans the whole body away and slams it in, Recover eases everything back. The sword runs Charge and the arm swing side by side in a Parallel Sequence, so the body winds up as the arm rises and slams as it slashes; Strike lands when both are done. Interrupt any of them and the body snaps back on its own, so a kill mid-swing never leaves the attacker frozen. |
| **Facing before the blow** | `Face Target` in the three attack trees | BH3's own node, fed by the `TransformOf(target)` Function. |
| **Committed swing** | Attack's guard, Stops Its Own Branch off | A started charge finishes even if the target steps out of range during the wind-up. Chase and Kite carry the opposite guards. |
| **Time-triggered guards** | The three `IsTargetInRange` guards, `IsFarFrom` | Distance changes without any variable write, so these poll. Every other guard in the samples wakes on a key. |
| **A leash** | `ReturnHome` above `Engage` | Stray more than twelve metres from home, mid-fight or not, and the enemy walks back. It is why the grunts that killed you do not camp your spawn. |
| **Top-priority takeover** | `Die` on every root | `isDead` flips and whatever was running is abandoned the same frame. |
| **A tree on a prop, an effect in a Function** | `Pickup`, `GiveWeapon`, `Heal` | Chests and health packs run the same tree: wait for the player, pop, apply, shrink while spent, refill. What apply does is a Function picked on the Pickup component and run from code with the player as its `taker` input: `GiveWeapon(taker, weapon)` on a chest, `Heal(taker, amount)` on a potion. The hand-out keeps Stops Its Own Branch off so it finishes even if you walk away. |
| **Hit back** | `PlayerOrders`, `Health.Damaged` | Damage carries the attacker. When the player is hit with no target, the attacker becomes the target and Engage takes over; a pending move order resumes once the fight is done. |
| **Functions reading objects** | `PositionOf`, `IsTargetInRange`, `IsFarFrom`, `RetreatPointFrom` | Pure graphs with GameObject and Vector3 inputs, reading transforms through member units. |

## The cast

| Piece | Role |
|---|---|
| `Combatant` | Faction, spawn point, the live roster, `Respawn()`. Publishes `home`. |
| `Enemy`, `EnemyKind` | Applies a kind's data to an enemy on Awake. |
| `Health` | Hit points, the red hurt flash; publishes `isDead` and `health01`. |
| `Weapon`, `WeaponDefinition` | The held weapon and its asset (range, cooldown, damage, knockback, projectile, and the prefab shown in the hand). `Equip` publishes `weaponId`, `attackRange`, `attackCooldown` and spawns the visual on the unit's hand socket. |
| `Knockback` | Shoves a NavMeshAgent along the mesh; the hammer uses it. |
| `Projectile` | The bow's bolt. |
| `Sight` | Enemy perception: radius plus a line-of-sight ray, every 0.2 s. |
| `Pickup`, `Pickups` | Publishes `playerNearby`; `Apply()` runs the effect Function on the player in reach. `Pickups` holds the two static helpers those Functions call. |
| `PlayerOrders` | Clicks become move or attack orders; an attack order cancels a pending move; a hit with no target makes the attacker the target; death forgets everything. |
| `HealthBar`, `Unit` | A bar over each head; the body, its renderers and its named parts that the tween nodes and the hurt flash act on, the hand socket the weapon visual hangs from, and the resting pose that Recover and Respawn return to. |
| `Strike`, `Respawn`, `Apply Pickup`, `Scale To`, `Swing Part`, `Charge`, `Recover` | The custom nodes this sample adds. |

## Notes

- Facts a guard may read before anyone has published them (`attackRange`, `attackCooldown`, `weaponId`,
  `keepDistance`, `home`) go through variable-read Functions with a fallback.
- Every tunable is a port default on a canvas, a field on a `WeaponDefinition` or `EnemyKind` asset, or a
  NavMeshAgent setting.
