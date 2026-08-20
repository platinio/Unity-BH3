using System;
using System.Collections.Generic;
using ArcaneOnyx.BehaviorTree.Debugging;
using NUnit.Framework;

namespace ArcaneOnyx.BehaviorTree.Tests
{
    /// <summary>
    /// The why-panel's call-site cache: that it stops replaying the ring for an answer it already has, and
    /// that it does not go on serving that answer once the recording has moved.
    ///
    /// <para>
    /// Both halves matter and they pull against each other, which is the reason for a test rather than a
    /// comment. A cache that never rebuilds is the variable watch's freeze bug in a new place; a cache that
    /// always rebuilds is the finding this fixes. So every test here either counts scans or asserts a
    /// rebuild happened.
    /// </para>
    /// </summary>
    [TestFixture]
    public class BehaviorTreeWhyPanelCallSiteCacheTests
    {
        private static readonly Guid Branch = new("11111111-1111-1111-1111-111111111111");
        private static readonly Guid Other = new("22222222-2222-2222-2222-222222222222");

        /// <summary>
        /// A recording that says how many times its ring was walked, and that can be moved the two ways a
        /// live one moves: a new event, and a tick that advanced without one.
        /// </summary>
        private sealed class CountingRecording : IBehaviorTreeRecording
        {
            private readonly List<BehaviorTreeEvent> events = new();
            private readonly List<BehaviorTreeCallSite> callSites = new()
            {
                new BehaviorTreeCallSite(BehaviorTreeCallSite.RootId, BehaviorTreeCallSite.RootId, Guid.Empty, "Zombie"),
            };

            public int Reads { get; private set; }

            private int dropped;

            public string AgentName => "Zombie";
            public string TreeName => "ZombieTree";
            public int Tick { get; set; }
            public int EventCount => events.Count;
            public IReadOnlyList<BehaviorTreeCallSite> CallSites => callSites;
            public int Dropped => dropped;

            public BehaviorTreeEvent EventAt(int index)
            {
                Reads++;
                return events[index];
            }

            public GuardTrace TraceFor(int tick, int sequence) => null;

            public CountingRecording CallSite(int id, int parent, string asset)
            {
                callSites.Add(new BehaviorTreeCallSite(id, parent, Guid.NewGuid(), asset));
                return this;
            }

            public CountingRecording Enter(Guid node, int callSiteId)
            {
                events.Add(BehaviorTreeEvent.Create(
                    BehaviorTreeEventKind.NodeEnter, Tick, events.Count, Tick, Tick * 0.02f, callSiteId, node));

                return this;
            }

            /// <summary>
            /// One more event into a ring that is already full: it lands, the oldest falls off, and only
            /// <see cref="Dropped"/> moves. <see cref="EventCount"/> and <see cref="Tick"/> stay exactly
            /// where they were, which is the whole state the key has to be able to notice.
            /// </summary>
            public CountingRecording WrapWith(Guid node, int callSiteId)
            {
                events.RemoveAt(0);
                events.Add(BehaviorTreeEvent.Create(
                    BehaviorTreeEventKind.NodeEnter, Tick, events.Count, Tick, Tick * 0.02f, callSiteId, node));

                dropped++;

                return this;
            }

            public CountingRecording Forget()
            {
                Reads = 0;
                return this;
            }
        }

        private static CountingRecording OneBranchAtTwoCallSites()
        {
            var recording = new CountingRecording { Tick = 300 };

            recording.CallSite(1, BehaviorTreeCallSite.RootId, "Attack")
                .CallSite(2, BehaviorTreeCallSite.RootId, "Attack")
                .Enter(Branch, 1)
                .Enter(Branch, 2)
                .Enter(Other, 1);

            return recording;
        }

        private static BehaviorTreeWhyPanel Panel() => new BehaviorTreeWhyPanel(null);

        [Test]
        public void TheCachedAnswerIsTheOneTheExplainerWouldHaveGiven()
        {
            var recording = OneBranchAtTwoCallSites();

            CollectionAssert.AreEqual(
                BehaviorTreeExplainer.CallSitesFor(recording, Branch),
                Panel().CallSites(recording, Branch),
                "A faster wrong answer is not the point of any of this.");
        }

        [Test]
        public void AskingAgainForAnUnmovedRecordingDoesNotReplayTheRing()
        {
            var recording = OneBranchAtTwoCallSites();
            var panel = Panel();

            panel.CallSites(recording, Branch);

            var afterFirst = recording.Reads;
            Assert.Greater(afterFirst, 0, "Fixture check: the first ask has to actually scan something.");

            panel.CallSites(recording, Branch);
            panel.CallSites(recording, Branch);

            Assert.AreEqual(afterFirst, recording.Reads,
                "IMGUI asks for a height and then draws, and the picker asks a third time — so an uncached "
                + "answer is three full ring replays per repaint of a panel that is open all session.");
        }

        [Test]
        public void ANewEventRebuildsTheAnswer()
        {
            var recording = OneBranchAtTwoCallSites();
            var panel = Panel();

            panel.CallSites(recording, Branch);

            recording.CallSite(3, BehaviorTreeCallSite.RootId, "Flee").Enter(Branch, 3).Forget();

            CollectionAssert.Contains(panel.CallSites(recording, Branch), 3,
                "The branch has started running somewhere new, and the picker has to offer it.");
            Assert.Greater(recording.Reads, 0, "Which it cannot do without rescanning.");
        }

        /// <summary>
        /// The wrap case, in the shape finding 1.1 found it: a full ring reports a saturated
        /// <c>EventCount</c> forever, so the tick is the only part of the key still moving.
        /// </summary>
        [Test]
        public void ATickThatAdvancedWithoutANewEventStillRebuilds()
        {
            var recording = OneBranchAtTwoCallSites();
            var panel = Panel();

            panel.CallSites(recording, Branch);
            recording.Forget();

            recording.Tick += 1;
            panel.CallSites(recording, Branch);

            Assert.Greater(recording.Reads, 0,
                "Once the ring is full the event count stops changing, so a key without the tick freezes.");
        }

        /// <summary>
        /// The list can shrink, and nothing else in the key can see it happen.
        ///
        /// <para>
        /// Past the wrap <c>EventCount</c> has saturated, and on a machine that has stopped ticking the tick
        /// has stopped too — while an external writer keeps filling the ring, because it does not need the
        /// tree to be running. What those writes push off the back are this node's own oldest events, so a
        /// call site it no longer has any events in would go on being offered by the picker and resolve to
        /// an explanation with nothing in it.
        /// </para>
        /// </summary>
        [Test]
        public void AnEventEvictedFromAFullRingRebuildsTheAnswer()
        {
            var recording = OneBranchAtTwoCallSites();
            var panel = Panel();

            CollectionAssert.AreEquivalent(new[] { 1, 2 }, panel.CallSites(recording, Branch), "Fixture check.");

            var tickBefore = recording.Tick;
            var countBefore = recording.EventCount;

            // Evicts the branch's entry at call site 1, which is the oldest event held.
            recording.WrapWith(Other, 1);

            Assert.AreEqual(tickBefore, recording.Tick,
                "Guards the test: the tick must not be what the key noticed.");
            Assert.AreEqual(countBefore, recording.EventCount,
                "Nor the count, which is what a full ring stops moving.");

            CollectionAssert.AreEquivalent(new[] { 2 }, panel.CallSites(recording, Branch),
                "The branch has no events left at call site 1, so the picker must stop offering it.");
        }

        [Test]
        public void ADifferentNodeRebuildsTheAnswer()
        {
            var recording = OneBranchAtTwoCallSites();
            var panel = Panel();

            var branch = panel.CallSites(recording, Branch);
            var other = panel.CallSites(recording, Other);

            CollectionAssert.AreEquivalent(new[] { 1, 2 }, branch);
            CollectionAssert.AreEquivalent(new[] { 1 }, other,
                "Selecting a different node on the canvas must not be served the previous node's call sites.");
        }

        /// <summary>
        /// The second recording is built to be indistinguishable from the first on every part of the key
        /// except identity — same tick, same event count — because a test where the counts differ passes
        /// whether or not identity is checked at all.
        /// </summary>
        [Test]
        public void ADifferentRecordingRebuildsTheAnswer()
        {
            var panel = Panel();
            var first = OneBranchAtTwoCallSites();

            panel.CallSites(first, Branch);

            var second = new CountingRecording { Tick = first.Tick };
            second.CallSite(1, BehaviorTreeCallSite.RootId, "Attack")
                .Enter(Branch, 1)
                .Enter(Other, 1)
                .Enter(Other, 1);

            Assert.AreEqual(first.Tick, second.Tick, "Fixture check: the two must be a key collision.");
            Assert.AreEqual(first.EventCount, second.EventCount, "Fixture check.");

            CollectionAssert.AreEquivalent(new[] { 1 }, panel.CallSites(second, Branch),
                "Selecting a different agent in the hierarchy hands the panel a different recording that can "
                + "sit at the same tick with the same number of events, so identity has to be in the key.");
        }

        [Test]
        public void TheNamesAreBuiltOnceAndDroppedWithTheList()
        {
            var recording = OneBranchAtTwoCallSites();
            var panel = Panel();

            var names = panel.CallSiteNames(recording, Branch);

            Assert.AreEqual(2, names.Length);
            Assert.AreSame(names, panel.CallSiteNames(recording, Branch),
                "The picker allocated a fresh string[] and re-walked every call-site path per repaint.");

            recording.CallSite(3, BehaviorTreeCallSite.RootId, "Flee").Enter(Branch, 3);

            var rebuilt = panel.CallSiteNames(recording, Branch);

            Assert.AreNotSame(names, rebuilt, "A new call site has to reach the picker.");
            Assert.AreEqual(3, rebuilt.Length);
        }
    }
}
