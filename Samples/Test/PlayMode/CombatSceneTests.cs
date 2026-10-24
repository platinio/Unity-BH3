using System;
using System.Collections;
using System.Linq;
using ArcaneOnyx.BehaviorTree;
using ArcaneOnyx.BehaviorTree.Debugging;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace ArcaneOnyx.BH3.Samples.Tests.PlayMode
{
    public sealed class CombatSceneTests : SampleSceneFixture
    {
        private static readonly Guid Wander = NodeIn("Combat", "Enemy", "Wander");
        private static readonly Guid ReturnHome = NodeIn("Combat", "Enemy", "ReturnHome");
        private static readonly Guid Engage = NodeIn("Combat", "Enemy", "Engage");
        private static readonly Guid Attack = NodeIn("Combat", "Engage", "PunchAttack (dynamic)");
        private static readonly Guid PlayerEngage = NodeIn("Combat", "Player", "Engage");

        private Combatant player;
        private Combatant grunt;

        protected override string SampleName => "Combat";

        [UnitySetUp]
        public IEnumerator FindTheCast()
        {
            player = Combatant.All.First(combatant => combatant.IsPlayer);
            grunt = Combatant.All.First(combatant => !combatant.IsPlayer && combatant.name.StartsWith("Grunt"));
            yield break;
        }

        [UnityTest]
        public IEnumerator AGruntWandersUntilThePlayerIsInSightThenSwingsAndGoesBackToItsOwnBusiness()
        {
            Silence(player);
            BehaviorTreeFlightRecorder recorder = RecorderOf(grunt);
            yield return Frames(5);
            Assert.IsTrue(Entered(recorder, Wander), "With nobody in sight the grunt wanders,");
            Assert.IsFalse(Entered(recorder, Engage), "and has nobody to engage.");

            Health playerHealth = player.GetComponent<Health>();
            float healthBefore = playerHealth.Fraction;
            int mark = Mark(recorder);
            Teleport(player, grunt.transform.position + Vector3.right * 1.5f);
            yield return Seconds(2f);
            Assert.IsTrue(EnteredSince(recorder, mark, Engage), "Sight publishes the player and Engage takes over.");
            Assert.IsTrue(TakenOverSince(recorder, mark, Wander), "Wander is the branch that lost the slot.");
            Assert.IsTrue(EnteredSince(recorder, mark, Attack), "In range, Engage runs the attack the weapon names,");
            Assert.Less(playerHealth.Fraction, healthBefore, "and the swing lands.");

            Guid strike = StrikeOf(grunt);
            int strikesSoFar = EntriesAtLeast(recorder, strike);
            yield return Until(() => EntriesAtLeast(recorder, strike) > strikesSoFar, 3f, "The next strike");
            mark = Mark(recorder);
            Teleport(player, grunt.transform.position + Vector3.right * 30f);
            yield return Seconds(1.5f);
            Assert.IsTrue(AbortedSince(recorder, mark, Engage), "Losing the target aborts Engage mid-fight.");
            Assert.IsTrue(EnteredSince(recorder, mark, Wander) || EnteredSince(recorder, mark, ReturnHome),
                "The grunt goes back to its own business.");
            Unit unit = Unit.Of(grunt.gameObject);
            Assert.Less(Quaternion.Angle(unit.Body.localRotation, unit.RestingPose.Rotation), 2f,
                "The interrupted swing left the body back in its resting pose.");
        }

        private static Guid StrikeOf(Combatant fighter)
        {
            var attackTree = (BehaviorTreeGraphAsset)Fact(fighter, CombatFacts.AttackTree);
            return attackTree.graph.Nodes.First(node => node is Strike).guid;
        }

        [UnityTest]
        public IEnumerator ThePlayerHitWithNoTargetTurnsOnTheAttacker()
        {
            Silence(grunt);
            BehaviorTreeFlightRecorder recorder = RecorderOf(player);
            Teleport(player, grunt.transform.position + Vector3.right * 12f);
            yield return Seconds(0.5f);
            int mark = Mark(recorder);
            yield return Seconds(0.5f);
            Assert.IsFalse(EnteredSince(recorder, mark, PlayerEngage), "Untouched and unordered, the player has no target.");

            player.GetComponent<Health>().Damage(1, grunt.gameObject);
            yield return Frames(2);
            Assert.IsTrue(EnteredSince(recorder, mark, PlayerEngage), "Retaliation publishes the attacker as the target and Engage takes over.");
            Assert.AreSame(grunt.gameObject, Fact(player, CombatFacts.Target), "The target is whoever hit it.");
        }
    }
}
