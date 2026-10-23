using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using ArcaneOnyx.BehaviorTree.Debugging;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace ArcaneOnyx.BH3.Samples.Tests.PlayMode
{
    public sealed class DefenseSceneTests : SampleSceneFixture
    {
        private const int Arrow = 0;
        private const int Cannon = 1;

        private static readonly Guid FollowPath = NodeIn("Defense", "Walker", "FollowPath");
        private static readonly Guid Breach = NodeIn("Defense", "Walker", "Breach");
        private static readonly Guid Perish = NodeIn("Defense", "Walker", "Perish");
        private static readonly Guid Idle = NodeIn("Defense", "Turret", "Idle");
        private static readonly Guid Shoot = NodeIn("Defense", "Turret", "ArrowShot (dynamic)");
        private static readonly Guid ArrowStrike = NodeIn("Defense", "ArrowShot", "Strike");
        private static readonly Guid CannonStrike = NodeIn("Defense", "CannonShot", "Strike");
        private static readonly Guid SpawnWave = NodeIn("Defense", "WaveDirector", "Spawn Wave");
        private static readonly Guid NextWave = NodeIn("Defense", "WaveDirector", "Next Wave");

        private DefenseGame game;
        private Spawner spawner;
        private TurretBuilder builder;

        protected override string SampleName => "Defense";

        [UnitySetUp]
        public IEnumerator FindTheCast()
        {
            game = TheOnly<DefenseGame>();
            spawner = TheOnly<Spawner>();
            builder = TheOnly<TurretBuilder>();
            yield break;
        }

        [UnityTest]
        public IEnumerator AWalkerToldItReachedTheEndBreachesAndCostsALife()
        {
            Silence(spawner);
            Walker walker = SpawnAWalker();
            BehaviorTreeFlightRecorder recorder = RecorderOf(walker);
            yield return Frames(5);
            Assert.IsTrue(Entered(recorder, FollowPath), "A fresh walker follows the path.");
            Assert.IsFalse(Entered(recorder, Breach), "Nothing has reached the end yet.");

            int livesBefore = game.Lives;
            int mark = Mark(recorder);
            Publish(walker, DefenseFacts.ReachedEnd, true);
            yield return Frames(2);
            Assert.IsTrue(EnteredSince(recorder, mark, Breach), "Breach wakes on the published fact and takes the slot.");
            Assert.IsTrue(TakenOverSince(recorder, mark, FollowPath), "FollowPath is the branch that lost it.");

            yield return Until(() => walker == null, 2f, "The breaching walker being destroyed");
            Assert.AreEqual(livesBefore - 1, game.Lives, "A breach costs one life.");
        }

        [UnityTest]
        public IEnumerator AWalkerKilledPaysItsBountyAndPerishes()
        {
            Silence(spawner);
            Walker walker = SpawnAWalker();
            BehaviorTreeFlightRecorder recorder = RecorderOf(walker);
            yield return Frames(5);

            int pointsBefore = game.Points;
            int bounty = walker.GetComponent<Enemy>().Kind.Bounty;
            int mark = Mark(recorder);
            walker.GetComponent<Health>().Damage(100000, game.gameObject);
            yield return Frames(2);
            Assert.IsTrue(EnteredSince(recorder, mark, Perish), "Health publishes isDead and Perish takes over.");
            Assert.IsTrue(TakenOverSince(recorder, mark, FollowPath), "FollowPath is abandoned mid-leg.");
            Assert.AreEqual(pointsBefore + bounty, game.Points, "The bounty is paid on death.");

            yield return Until(() => walker == null, 3f, "The dead walker shrinking away");
        }

        [UnityTest]
        public IEnumerator AnArrowTurretRunsArrowShotWhenAnEnemyIsInSight() =>
            ATurretRunsTheTreeItsWeaponNames(Arrow, ArrowStrike, CannonStrike);

        [UnityTest]
        public IEnumerator ACannonTurretRunsCannonShotWhenAnEnemyIsInSight() =>
            ATurretRunsTheTreeItsWeaponNames(Cannon, CannonStrike, ArrowStrike);

        private IEnumerator ATurretRunsTheTreeItsWeaponNames(int kind, Guid itsStrike, Guid theOtherStrike)
        {
            Silence(spawner);
            Turret turret = Build(kind);
            BehaviorTreeFlightRecorder recorder = RecorderOf(turret);
            yield return Frames(5);
            Assert.IsTrue(Entered(recorder, Idle), "With nothing in sight the turret idles,");
            Assert.IsFalse(Entered(recorder, Shoot), "and has no reason to shoot.");

            int mark = Mark(recorder);
            Walker enemy = ParkAnEnemyBeside(turret, Vector3.right * 2f);
            yield return Seconds(1.5f);
            Assert.IsTrue(EnteredSince(recorder, mark, Shoot), "Shoot takes over once Sight publishes a target.");
            Assert.IsTrue(TakenOverSince(recorder, mark, Idle), "Idle is the branch that lost the slot.");
            Assert.IsTrue(EnteredSince(recorder, mark, itsStrike), "The shot tree the weapon names strikes.");
            Assert.IsFalse(EnteredSince(recorder, mark, theOtherStrike), "The other weapon's tree never runs.");

            mark = Mark(recorder);
            enemy.GetComponent<Health>().Damage(int.MaxValue, turret.gameObject);
            yield return Seconds(0.5f);
            Assert.IsTrue(EnteredSince(recorder, mark, Idle), "With the target dead the turret idles again.");
        }

        [UnityTest]
        public IEnumerator StrikesAreSpacedByTheWeaponsCooldown()
        {
            Silence(spawner);
            Turret turret = Build(Arrow);
            ParkAnEnemyBeside(turret, Vector3.right * 2f);
            float cooldown = turret.GetComponent<Weapon>().Definition.Cooldown;
            BehaviorTreeFlightRecorder recorder = RecorderOf(turret);

            yield return Seconds(cooldown * 4f + 1f);

            List<float> strikeTimes = recorder.Events
                .Where(recorded => recorded.Kind == BehaviorTreeEventKind.NodeEnter && recorded.NodeGuid == ArrowStrike)
                .Select(recorded => recorded.Time)
                .ToList();
            Assert.GreaterOrEqual(strikeTimes.Count, 3, "The turret keeps striking while the target stands there.");
            for (int i = 1; i < strikeTimes.Count; i++)
            {
                Assert.GreaterOrEqual(strikeTimes[i] - strikeTimes[i - 1], cooldown - FrameSeconds,
                    "Two strikes came closer together than the cooldown allows.");
            }
        }

        [UnityTest]
        public IEnumerator ATurretFacesItsTargetBeforeItStrikes()
        {
            Silence(spawner);
            Turret turret = Build(Arrow);
            Walker enemy = ParkAnEnemyBeside(turret, new Vector3(2f, 0f, 2f));
            BehaviorTreeFlightRecorder recorder = RecorderOf(turret);

            yield return Until(() => Entered(recorder, ArrowStrike), 3f, "The first strike");

            Assert.Less(FlatAngleBetween(turret.transform, enemy.transform.position), 15f,
                "The engine was turned onto the target before the strike.");
        }

        [UnityTest]
        public IEnumerator ATurretBuiltTheMomentPlayStartsIsFullSizeASecondLater()
        {
            Turret turret = Build(Arrow);
            Unit unit = Unit.Of(turret.gameObject);

            yield return Seconds(1f);

            Assert.Greater(unit.Body.lossyScale.y, unit.RestingPose.Scale.y * 0.5f,
                "A turret built before its tree's first tick used to be held at scale zero by its own idle punch.");
        }

        [UnityTest]
        public IEnumerator TheWaveDirectorSpawnsTheFirstWaveThenCallsTheNext()
        {
            BehaviorTreeFlightRecorder director = RecorderOf(spawner);
            int firstWave = game.Wave;
            int expected = spawner.WaveSize(firstWave);

            yield return Until(() => UnityEngine.Object.FindObjectsByType<Walker>(FindObjectsSortMode.None).Length >= expected, 12f,
                "The first wave spawning");
            Assert.IsTrue(Entered(director, SpawnWave), "Spawn Wave is the node that put them there.");

            yield return Until(() => game.Wave == firstWave + 1, 20f, "The wave counter advancing");
            Assert.IsTrue(Entered(director, NextWave), "Next Wave is the node that bumped it.");
        }

        private Walker SpawnAWalker()
        {
            spawner.Spawn(spawner.EnemyFor(1, 0));
            return TheOnly<Walker>();
        }

        private Turret Build(int kind)
        {
            game.AddPoints(1000);
            BuildPad pad = TheOnly<BuildPad>();
            Assert.IsTrue(pad.TryBuild(builder.Kinds[kind], game), "The pad takes the turret.");
            return TheOnly<Turret>();
        }

        private Walker ParkAnEnemyBeside(Turret turret, Vector3 offset)
        {
            Walker enemy = SpawnAWalker();
            Silence(enemy);
            enemy.GetComponent<Health>().SetMaxHealth(100000);
            Teleport(enemy, OnTheNavMesh(turret.transform.position + offset));
            return enemy;
        }
    }
}
