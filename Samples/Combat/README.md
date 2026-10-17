# Combat

A small arena. Your blue capsule starts at the bottom; four red ones wander the far side, three with swords
and one with a bow. Left-click the ground to move. Left-click an enemy to attack it: your unit chases until in
range, stops, and strikes on a cooldown until it dies. Enemies attack on sight. Press **1** for the sword and
**2** for the bow. Whoever dies flashes, shrinks away and respawns a couple of seconds later, so the fight
never ends. Primitives and tweens only.

Open `Combat.unity` and press Play.

## The trees

```
Enemy                                     Player
└─ Repeater → Selector                    └─ Repeater → Selector
   ├─ Die      RG: isDead                    ├─ Die      RG: isDead
   ├─ Engage   RG: hasTarget                 ├─ Engage   RG: hasTarget
   └─ Wander   no guard                      ├─ MoveTo   RG: OrdersDiffer(orderNumber, completedOrder)
                                             └─ Idle     no guard

Engage   (Required: target)
└─ Repeater → Selector
   ├─ Attack   RG: IsTargetInRange(target, attackRange), every 0.1 s, Stops Its Own Branch OFF
   │   └─ Selector
   │       ├─ SwordAttack   RG: HoldsWeapon(weaponId, "Sword")
   │       └─ BowAttack     RG: HoldsWeapon(weaponId, "Bow")
   └─ Chase    RG: not IsTargetInRange(target, attackRange), every 0.1 s

SwordAttack / BowAttack   (Required: target)
└─ Sequence: Stop Nav Agent → Cooldown(attackCooldown) → Sequence: wind-up tween → Strike(target) → recoil tween

Chase   (Required: target)
└─ Parallel Selector: [ Set Nav Agent Position(PositionOf(target)) → Wait 0.25 ]  |  Repeater → Hop

Wander
└─ Sequence: Parallel Selector [ Set Nav Agent Position(random point) → Wait Until Reach | Repeater → Hop ] → Wait 1–3 s

Die
└─ Sequence: Flash Color → Scale To 0 → Wait 2 → Respawn
```

`Die`, `Engage`, `Chase` and the two attack trees are the same assets on every combatant. `MoveTo` and
`Idle` come from the Click to move sample.

## What each piece demonstrates

| Feature | Where | What to watch |
|---|---|---|
| **One branch, two kinds of agent** | `Engage` | The player and every enemy run the same asset. The player's `target` comes from a click, an enemy's from `Sight`. Nothing in Engage knows which. |
| **Top-priority takeover** | `Die` on both roots | `isDead` flips and whatever was running, mid-swing or mid-chase, is abandoned the same frame. |
| **Committed swing** | Attack's guard, Stops Its Own Branch off | A started strike finishes even if the target steps out of range during the wind-up. Chase carries the opposite guard, so the two never run at once. |
| **A time-triggered guard** | `IsTargetInRange`, every 0.1 s | Distance changes without any variable write, so this is the one guard in the samples that polls. The sticky note on the canvas says why. |
| **The weapon owns its attack** | `SwordAttack`, `BowAttack` | Each weapon's rhythm and animation live in its own tree. Engage picks one with a `HoldsWeapon` guard; the `wanted` port is an inline literal, so the same Function serves both. Press 1 or 2 mid-fight and the other tree takes over on the key change. |
| **Cooldown fed by data** | `Cooldown` in both attack trees | Its Duration port reads the `attackCooldown` fact the Weapon publishes from its `WeaponDefinition` asset. Change the asset, change the rhythm. |
| **Functions reading objects** | `PositionOf`, `IsTargetInRange` | Pure graphs with a GameObject input, reading transforms through member units. No blackboard-sync code. |
| **Facts from a sensor** | `Sight` | Scans every 0.2 s with a line-of-sight ray and publishes `target` and `hasTarget`. The guard wakes on the key, never polls. |

## The cast

| Piece | Role |
|---|---|
| `Combatant` | Faction, spawn point, body scale, the live roster `Sight` scans. `Respawn()` is what the Die branch calls. |
| `Health` | Hit points; publishes `isDead` and `health01`. |
| `Weapon`, `WeaponDefinition` | The held weapon and its data asset (range, cooldown, damage, projectile). Publishes `weaponId`, `attackRange`, `attackCooldown` on equip. |
| `Projectile` | The bow's bolt: flies to its target, damages on arrival. |
| `Sight` | Enemy perception. |
| `PlayerOrders` | Clicks to move or attack orders, keys 1 and 2 to equip. An attack order cancels a pending move order. |
| `HealthBar` | A quad over each head, scaled by health. |
| `Strike`, `Respawn`, `Scale To` | The three custom nodes this sample adds. |

## Notes

- Facts the guards read before any fight exists (`attackRange`, `attackCooldown`, `weaponId`) go through
  variable-read Functions with a fallback, so an agent that has not published them yet is not an error.
- Every tunable is a port default on a canvas, a field on a `WeaponDefinition`, or a NavMeshAgent setting.
