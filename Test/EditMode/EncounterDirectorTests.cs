/*using ArcaneOnyx.BehaviorTree.Encounter;
using NUnit.Framework;
using UnityEngine;

namespace ArcaneOnyx.BehaviorTree.Tests
{
    /// <summary>
    /// Behaviour of the encounter director, the layer that decides who is allowed to attack and where
    /// everyone else stands. It takes its time through <see cref="EncounterDirector.Tick"/> instead of
    /// reading Unity's clock, so the token rotation can be stepped exactly here rather than watched in play
    /// mode and guessed at.
    /// </summary>
    [TestFixture]
    public class EncounterDirectorTests
    {
        /// <summary>A participant that is nothing but a position, which is all the director asks for.</summary>
        private sealed class FakeAgent : IEncounterAgent
        {
            public Vector3 Position { get; set; }

            public FakeAgent(Vector3 position) => Position = position;

            public FakeAgent(float x, float z) : this(new Vector3(x, 0.0f, z)) { }
        }

        private static EncounterDirector Director(
            int maxMeleeTokens = 1,
            int maxRangedTokens = 1,
            float maxTokenHoldSeconds = 3.0f,
            float reacquireCooldownSeconds = 1.5f,
            int slotCount = 8,
            float slotRadius = 4.0f)
        {
            return new EncounterDirector(new EncounterDirectorSettings(
                maxMeleeTokens,
                maxRangedTokens,
                maxTokenHoldSeconds,
                reacquireCooldownSeconds,
                slotCount,
                slotRadius,
                slotReassignIntervalSeconds: 1.0f));
        }

        #region TOKENS

        [Test]
        public void OnlyAsManyAgentsAsThereAreTokensMayAttack()
        {
            var director = Director(maxMeleeTokens: 1);
            var first = new FakeAgent(1.0f, 0.0f);
            var second = new FakeAgent(-1.0f, 0.0f);

            director.Register(first, AttackTokenType.Melee);
            director.Register(second, AttackTokenType.Melee);

            Assert.IsTrue(director.TryAcquireToken(first), "The first agent to ask should get the turn.");
            Assert.IsFalse(director.TryAcquireToken(second), "A second agent must wait rather than attack alongside the first.");
        }

        [Test]
        public void HoldingAgentKeepsSayingYesSoARunningBranchCanReAsk()
        {
            var director = Director(maxMeleeTokens: 1);
            var agent = new FakeAgent(1.0f, 0.0f);
            director.Register(agent, AttackTokenType.Melee);

            Assert.IsTrue(director.TryAcquireToken(agent));
            Assert.IsTrue(director.TryAcquireToken(agent), "Re-asking while holding the token must not be treated as a new request.");
            Assert.AreEqual(1, director.HeldTokenCount(AttackTokenType.Melee), "Re-asking must not consume a second token.");
        }

        [Test]
        public void MeleeAndRangedDrawFromSeparatePools()
        {
            var director = Director(maxMeleeTokens: 1, maxRangedTokens: 1);
            var brawler = new FakeAgent(1.0f, 0.0f);
            var thrower = new FakeAgent(-6.0f, 0.0f);

            director.Register(brawler, AttackTokenType.Melee);
            director.Register(thrower, AttackTokenType.Ranged);

            Assert.IsTrue(director.TryAcquireToken(brawler));
            Assert.IsTrue(director.TryAcquireToken(thrower), "A thrown attack must not be blocked by the melee turn.");
        }

        [Test]
        public void TokenIsTakenBackOnceItHasBeenHeldTooLong()
        {
            var director = Director(maxTokenHoldSeconds: 3.0f);
            var agent = new FakeAgent(1.0f, 0.0f);
            director.Register(agent, AttackTokenType.Melee);
            director.TryAcquireToken(agent);

            director.Tick(3.0f);

            Assert.IsFalse(director.HasToken(agent), "An agent that overstays its turn must lose it, or it pins the encounter.");
        }

        [Test]
        public void ReleasedTokenCannotBeGrabbedStraightBack()
        {
            var director = Director(reacquireCooldownSeconds: 1.5f);
            var agent = new FakeAgent(1.0f, 0.0f);
            director.Register(agent, AttackTokenType.Melee);
            director.TryAcquireToken(agent);

            director.ReleaseToken(agent);

            Assert.IsFalse(director.TryAcquireToken(agent), "Without a cooldown the same agent would attack forever.");
        }

        [Test]
        public void AgentMayAttackAgainOnceItsCooldownExpires()
        {
            var director = Director(reacquireCooldownSeconds: 1.5f);
            var agent = new FakeAgent(1.0f, 0.0f);
            director.Register(agent, AttackTokenType.Melee);
            director.TryAcquireToken(agent);
            director.ReleaseToken(agent);

            director.Tick(2.0f);

            Assert.IsTrue(director.TryAcquireToken(agent));
        }

        [Test]
        public void TurnPassesToAnotherAgentWhenTheHolderTimesOut()
        {
            var director = Director(maxMeleeTokens: 1, maxTokenHoldSeconds: 3.0f);
            var first = new FakeAgent(1.0f, 0.0f);
            var second = new FakeAgent(-1.0f, 0.0f);
            director.Register(first, AttackTokenType.Melee);
            director.Register(second, AttackTokenType.Melee);
            director.TryAcquireToken(first);

            director.Tick(3.0f);

            Assert.IsTrue(director.TryAcquireToken(second), "The freed turn should go to someone who has been waiting.");
            Assert.IsFalse(director.TryAcquireToken(first), "The agent that just had its turn must not take it straight back.");
        }

        [Test]
        public void UnregisteringFreesTheTurnForSomeoneElse()
        {
            var director = Director(maxMeleeTokens: 1);
            var dying = new FakeAgent(1.0f, 0.0f);
            var waiting = new FakeAgent(-1.0f, 0.0f);
            director.Register(dying, AttackTokenType.Melee);
            director.Register(waiting, AttackTokenType.Melee);
            director.TryAcquireToken(dying);

            director.Unregister(dying);

            Assert.AreEqual(0, director.HeldTokenCount(AttackTokenType.Melee), "An agent that leaves must not keep holding a turn.");
            Assert.IsTrue(director.TryAcquireToken(waiting));
        }

        [Test]
        public void UnknownAgentIsNeverGrantedATurn()
        {
            var director = Director();
            var stranger = new FakeAgent(1.0f, 0.0f);

            Assert.IsFalse(director.TryAcquireToken(stranger), "Nodes fail closed when their agent never joined an encounter.");
            Assert.IsFalse(director.HasToken(stranger));
        }

        #endregion

        #region SLOTS

        [Test]
        public void AgentsAreSpreadOntoDistinctStandingPositions()
        {
            var director = Director(slotCount: 4, slotRadius: 4.0f);
            var north = new FakeAgent(0.0f, 5.0f);
            var south = new FakeAgent(0.0f, -5.0f);
            var east = new FakeAgent(5.0f, 0.0f);
            var west = new FakeAgent(-5.0f, 0.0f);

            director.Register(north, AttackTokenType.Melee);
            director.Register(south, AttackTokenType.Melee);
            director.Register(east, AttackTokenType.Melee);
            director.Register(west, AttackTokenType.Melee);
            director.SetTargetPosition(Vector3.zero);

            director.Tick(0.1f);

            var slots = new[]
            {
                director.GetSlot(north), director.GetSlot(south),
                director.GetSlot(east), director.GetSlot(west)
            };

            CollectionAssert.AllItemsAreUnique(slots, "Two agents sharing a slot is exactly the dogpile this exists to prevent.");
            CollectionAssert.DoesNotContain(slots, EncounterDirector.UnassignedSlot, "Four agents fit a four slot ring.");
        }

        [Test]
        public void EachAgentIsSentToTheNearestFreePosition()
        {
            var director = Director(slotCount: 4, slotRadius: 4.0f);
            var north = new FakeAgent(0.0f, 5.0f);
            director.Register(north, AttackTokenType.Melee);
            director.SetTargetPosition(Vector3.zero);

            director.Tick(0.1f);

            Vector3 slotPosition = director.GetSlotPosition(north);

            Assert.Less((slotPosition - north.Position).magnitude, 2.0f,
                "An agent standing due north should be sent to the northern slot, not marched around the ring.");
        }

        [Test]
        public void StandingPositionsSitOnTheConfiguredRadius()
        {
            var director = Director(slotCount: 8, slotRadius: 4.0f);
            var agent = new FakeAgent(6.0f, 0.0f);
            director.Register(agent, AttackTokenType.Melee);
            director.SetTargetPosition(Vector3.zero);

            director.Tick(0.1f);

            Assert.AreEqual(4.0f, director.GetSlotPosition(agent).magnitude, 0.001f);
        }

        [Test]
        public void RingFollowsTheTarget()
        {
            var director = Director(slotCount: 4, slotRadius: 4.0f);
            var agent = new FakeAgent(0.0f, 5.0f);
            director.Register(agent, AttackTokenType.Melee);
            director.SetTargetPosition(Vector3.zero);
            director.Tick(0.1f);

            director.SetTargetPosition(new Vector3(10.0f, 0.0f, 0.0f));

            Assert.AreEqual(4.0f, (director.GetSlotPosition(agent) - new Vector3(10.0f, 0.0f, 0.0f)).magnitude, 0.001f,
                "Standing positions are relative to the target, so they must move when the player does.");
        }

        [Test]
        public void AgentsBeyondRingCapacityHoldTheirBearing()
        {
            var director = Director(slotCount: 2, slotRadius: 4.0f);
            var first = new FakeAgent(0.0f, 5.0f);
            var second = new FakeAgent(0.0f, -5.0f);
            var overflow = new FakeAgent(5.0f, 0.0f);

            director.Register(first, AttackTokenType.Melee);
            director.Register(second, AttackTokenType.Melee);
            director.Register(overflow, AttackTokenType.Melee);
            director.SetTargetPosition(Vector3.zero);

            director.Tick(0.1f);

            Assert.AreEqual(EncounterDirector.UnassignedSlot, director.GetSlot(overflow),
                "A two slot ring cannot seat three agents.");
            Assert.AreEqual(4.0f, director.GetSlotPosition(overflow).magnitude, 0.001f,
                "The overflow agent still holds the ring radius rather than crowding an occupied slot.");
        }

        #endregion
    }
}*/
