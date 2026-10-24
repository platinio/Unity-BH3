# Samples

Four small games built on BH3, for learning how trees are put together. Each is a scene you press Play in
and a README that draws its trees and says what each piece demonstrates. This page is the order to read
them in, and where each BH3 idea is shown. Half an hour to play all four; the READMEs take longer.

The samples live here in the repository, not in the release package. They need Unity 6 with BH3,
`com.unity.inputsystem` and `com.unity.ai.navigation`. The reference project
[platinio/bh3-development](https://github.com/platinio/bh3-development) carries this repository as a
submodule at `Assets/ArcaneOnyx/BH3/` with all three in place; open it and the scenes are under `Samples/`.

---

## Read them in this order

| Sample | Teaches | Open |
|---|---|---|
| [Click to move](ClickToMove/README.md) | The smallest complete agent: a Selector with one guarded branch above an unguarded fallback, a Reactive Guard that wakes on a published fact, a branch as its own asset with Required parameters, a Function feeding a port, and a branch that ends itself by writing the fact its guard reads. Ten minutes. | `ClickToMove/ClickToMove.unity` |
| [Combat](Combat/README.md) | One `Engage` branch shared by the player and every enemy kind, an attack tree chosen at run time by a fact, guards that poll on a timer beside guards that wake on a key, **Stops Its Own Branch** off for a committed swing, `Die` at top priority, a tree on a prop, and custom nodes that run over several ticks. | `Combat/Combat.unity` |
| [Defense](Defense/README.md) | A tree running a game system rather than a character, reactive guards on facts that components publish, turrets reusing Combat's attack trees through the same fact, agents spawned with their tree already set, and Face Target inside the shot. | `Defense/Defense.unity` |
| [RTS](RTS/README.md) | Numbered orders on three channels, each branch completing its own number, interrupt and resume that falls out of the tree shape alone, a branch that re-targets itself, a tree on a building, and the widest reuse: branches from all three other samples on one map. | `RTS/RTS.unity` |

Every tree in every sample is laid out so that the picture agrees with the priority: children run left to
right. Open a tree in the graph editor while the scene plays and the running branch lights up.

---

## Where each BH3 idea is shown

The left column is the idea, the middle column the page that explains it, the right column where to watch
it in a sample.

| Idea | Explained in | Watch it in |
|---|---|---|
| A Selector with an unguarded fallback | [Execution order](../docs/2-building-trees/05-execution-order.md) | Every root tree. `Idle` and `Wander` carry no guard; whatever sits above them takes the slot by having a reason to |
| A Reactive Guard that takes over and aborts | [Guards](../docs/2-building-trees/06-guards.md) | Click to move's `MoveTo`, Combat's `Engage`, Defense's `Breach` and `Perish`, the Worker's `FightBack` |
| A guard woken by a key, beside one that polls | [Guards: triggers](../docs/2-building-trees/06-guards.md) | Combat's `hasTarget` guard wakes on the write; its three `IsTargetInRange` guards poll every 0.1 s, because distance changes without any write |
| Stops Its Own Branch off | [Guards](../docs/2-building-trees/06-guards.md) | Combat's `Attack`: a started charge finishes even if the target steps out of range. `Pickup`'s `HandOut` |
| A branch as its own asset, with parameters | [Sub-trees](../docs/2-building-trees/07-sub-trees.md) | `MoveTo(tripDestination, acceptedOrder)`, `Engage(target)`, `Gather(resource, acceptedGatherOrder)`. Required, so an unfed call site is a finding, not a silent zero |
| A sub-tree chosen at run time | [Sub-trees](../docs/2-building-trees/07-sub-trees.md) | `Engage`'s Attack node and Defense's Turret feed the `Tree` port from the `attackTree` fact the held weapon publishes |
| Facts published by components | [Variables and scope](../docs/2-building-trees/04-variables-and-scope.md) | `Sight` writes `hasTarget` and `target`, `Health` writes `isDead`, `Walker` writes `reachedEnd`, `ClickToMoveInput` writes `destination` and `orderNumber`, all through the `AgentVariableWriter` so guards wake |
| A branch that ends itself | [Variables and scope](../docs/2-building-trees/04-variables-and-scope.md) | `MoveTo` writes `completedOrder = acceptedOrder` on arrival and its own guard turns false. `Gather` and `Build` do the same with their order numbers |
| Functions feeding ports, with a fallback | [Functions](../docs/2-building-trees/08-functions.md) | `destinationRead`, `OrdersDiffer`, `TransformOf`, `IsTargetInRange`, `ApproachPoint`; every fact a guard may read before it is written goes through a read with a fallback |
| Parallel composites | [Node reference](../docs/2-building-trees/02-node-reference.md) | `MoveTo` and `WalkTo` race a walk against a watcher; `SwordAttack` runs a lean and an arm swing side by side |
| Decorators | [Node reference](../docs/2-building-trees/02-node-reference.md) | `Repeater` under every root, `Cooldown` on every attack, `Until Failure` around a swing loop, `Return Success` around an optional leg of `Gather` |
| Custom nodes that run over several ticks | [Custom nodes](../docs/4-extending-with-csharp/01-custom-nodes.md) | `Hop`, `Punch Scale`, `Charge`, `Swing Part` start a tween on enter, return Running until it ends, and restore the body on exit, so an abort never leaves a limb in the air |
| A tree on something that is not a character | [Core concepts](../docs/1-start-here/03-core-concepts.md) | Defense's `WaveDirector` on the Game object, RTS's `Building`, Combat's `Pickup` on chests and health packs |
| Checking a tree | [Checking your tree](../docs/2-building-trees/09-checking-your-tree.md) | Run `bt_verify` over `Defense/Trees` and nothing is reported; unset a Required port on a call site and it names it |
| Reading what ran | [Debugging](../docs/3-debugging/01-overview.md) | Open the Timeline on any agent while a scene plays. The play tests below assert on the same recording |

---

## What is not the point

The components under `Common/` exist so the trees have something to move: a tweener, `Unit` for a body and
its parts, `Health`, `Weapon`, `Sight`, `Projectile`. They are plain MonoBehaviours, kept small, and each
README lists them under *The cast*. Read them when a tree's node makes you curious, not before. The art is
Kenney's blocky characters and castle pieces, with primitives for the rest.

---

## The tests

`Test/PlayMode/` holds fourteen play-mode tests, one fixture per sample. Each loads the sample's scene,
drives one agent the way the game does, through a published fact, an enemy put in sight, damage dealt or
an order written, and asserts on the flight recorder which branch entered, which was taken over and which
was aborted. They are the shortest worked example of asking a tree what it did. `Test/EditMode/` covers the
tweener. Run both from **Window → General → Test Runner**.

---

## Next

- [Click to move](ClickToMove/README.md) — start here
- [Core concepts](../docs/1-start-here/03-core-concepts.md) — the mental model the samples assume
- [Best practices](../docs/2-building-trees/10-best-practices.md) — the rules the samples follow
