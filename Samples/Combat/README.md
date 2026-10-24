# Combat

An arena with a player character and three kinds of enemy. Left-click the ground to move, left-click an
enemy to attack it: the player chases until in range, stops, and strikes on a cooldown until the target
dies. **Grunts** use swords, the **archer** shoots from range and keeps its distance, the **brute** stays
where it is and swings a hammer with knockback. Walking into a chest takes its weapon, sword or hammer;
walking over a potion heals. A dead unit flashes, shrinks and respawns two seconds later. Kenney characters
and primitives; every animation is a tween on a body part.

Open `Combat.unity` and press Play.

## What each piece demonstrates

| Feature | Where | What to watch |
|---|---|---|
| **Enemies as data** | `EnemyKinds/Grunt`, `Archer`, `Brute` | Weapon, health, speed, sight, keep-distance, body size and the root tree, one asset each. The `Enemy` component applies it on Awake. Grunt and archer run the same tree; the brute runs its own. |
| **Walking is one branch, not nine** | `Common/Trees/WalkTo`, `StepToward` | Nine branches across four samples each spelled out the same race: walk to a point while a Repeater hops the body. They call one of two sub-trees now, so how a unit moves — and how high it hops — is one asset to open rather than nine to keep in step. The only thing a caller passes is where to go. |
| **One branch, every agent** | `Engage` | The player and all three kinds run the same asset. The player's `target` comes from a click, an enemy's from `Sight`; the archer kites because its kind publishes `keepDistance` 3, nobody else does. |
| **The weapon owns its attack** | `WeaponDefinition.attack`, Engage's Attack node | Each weapon's rhythm lives in its own tree, and the weapon asset names it. Engage runs one sub-tree node whose `Tree` port is fed by the `attackTree` fact, so Engage never learns that swords exist: a new weapon is an asset and a tree, and nothing shared is edited. Take a hammer from a chest and the swing in progress finishes, then the next one is the hammer's. A unit whose weapon names no tree falls back to the node's own pick, `PunchAttack`. |
| **Swings on body parts** | `Swing Part`, `Charge`, `Recover` | A Kenney character is six parts. Swing Part turns one about its own pivot and leaves it there, Charge leans the whole body away and slams it in, Recover eases everything back. The sword runs Charge and the arm swing side by side in a Parallel Sequence, so the body winds up as the arm rises and slams as it slashes; Strike lands when both are done. Interrupt any of them and the body snaps back on its own, so a kill mid-swing never leaves the attacker frozen. |
| **Facing before the blow** | `Face Target` in the three attack trees | BH3's own node, fed by the `TransformOf(target)` Function. |
| **Committed swing** | Attack's guard, Stops Its Own Branch off | A started charge finishes even if the target steps out of range during the wind-up. Chase and Kite carry the opposite guards. |
| **Time-triggered guards** | The three `IsTargetInRange` guards, `IsFarFrom` | Distance changes without any variable write, so these poll. Every other guard in the samples wakes on a key. |
| **A leash** | `ReturnHome` above `Engage` | Stray more than twelve metres from home, mid-fight or not, and the enemy walks back. It is why the grunts that killed you do not camp your spawn. |
| **Top-priority takeover** | `Die` on every root | `isDead` flips and whatever was running is abandoned the same frame. |
| **A tree on a prop, an effect in a Function** | `Pickup`, `GiveWeapon`, `Heal` | Chests and health packs run the same tree: wait for a player who can take it, pop, apply, shrink while spent, refill. Two Functions picked on the Pickup component decide what that means, run from code with the player as `taker`: `canPickup` is `CanEquipWeapon(taker, weapon)` on a chest and `IsHurt(taker)` on a potion, so a chest ignores a player already holding its weapon and a potion ignores a player at full health; `effect` is `GiveWeapon(taker, weapon)` or `Heal(taker, amount)`. The hand-out keeps Stops Its Own Branch off so it finishes even if you walk away. |
| **Hit back** | `Retaliation`, `UnitTargeting`, `Health.Damaged` | Damage carries the attacker. When the player is hit with no target, `Retaliation` makes the attacker the target and Engage takes over; `UnitTargeting` drops the target when it dies, and a pending move order resumes once the fight is done. |
| **Functions reading objects** | `PositionOf`, `IsTargetInRange`, `IsFarFrom`, `RetreatPointFrom` | Pure graphs with GameObject and Vector3 inputs, reading transforms through member units. |

## The cast

| Piece | Role |
|---|---|
| `Combatant` | Faction, spawn point, the live roster, `Respawn()`. Publishes `home`. |
| `Enemy`, `EnemyKind` | Applies a kind's data to an enemy on Awake. |
| `Health` | Hit points, the red hurt flash; publishes `isDead` and `health01`. |
| `Weapon`, `WeaponDefinition` | The held weapon and its asset (its attack tree, range, cooldown, damage, knockback, projectile, and the prefab shown in the hand). `Equip` publishes `attackTree`, `attackRange`, `attackCooldown` and spawns the visual on the unit's hand socket. |
| `Knockback` | Shoves a NavMeshAgent along the mesh; the hammer uses it. |
| `Projectile` | The bow's bolt. |
| `Sight` | Enemy perception: radius plus a line-of-sight ray, every 0.2 s. |
| `Pickup`, `Pickups` | Publishes `canPickup` while a living player in reach passes its canPickup Function; `Apply()` runs the effect Function on that player. `Pickups` holds the four static helpers those Functions call. |
| `PlayerOrders` | Clicks become move or attack orders; an attack order cancels a pending move; death forgets everything. |
| `NavMeshPathLine` | On the player only: draws the NavMesh path being walked as a line, and hides it on arrival. |
| `Retaliation`, `UnitTargeting` | A hit with no target makes the attacker the target; a dead target is dropped. Shared with the RTS sample. |
| `HealthBar`, `Unit` | A bar over each head; the body, its renderers and its named parts that the tween nodes and the hurt flash act on, the hand socket the weapon visual hangs from, and the resting pose that Recover and Respawn return to. |
| `Strike`, `Respawn`, `Apply Pickup`, `Scale To`, `Swing Part`, `Charge`, `Recover` | The custom nodes this sample adds. |

## Notes

- Facts a guard may read before anyone has published them (`attackRange`, `attackCooldown`,
  `keepDistance`, `home`) go through variable-read Functions with a fallback.
- Every tunable is a port default on a canvas, a field on a `WeaponDefinition` or `EnemyKind` asset, or a
  NavMeshAgent setting.
