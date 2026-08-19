using ArcaneOnyx.BehaviorTree.Debugging;
using NUnit.Framework;

namespace ArcaneOnyx.BehaviorTree.Tests
{
    /// <summary>
    /// <see cref="BehaviorTreeRecordingStamp"/> is what every debug panel asks "has this recording moved?",
    /// so these tests are about the two ways that question is answered wrongly.
    ///
    /// <para>
    /// The first is the one that shipped: an event count on its own saturates at the ring's capacity, and a
    /// panel keyed on it alone stops rebuilding forever once the ring wraps. The second is the mirror image --
    /// a tick on its own misses everything written within a tick. The premise behind the first is asserted
    /// here directly against a real recorder rather than assumed, because the whole finding rests on it.
    /// </para>
    /// </summary>
    [TestFixture]
    public class BehaviorTreeRecordingStampTests
    {
        /// <summary>Small enough that a handful of writes wraps it, and the wrap is the subject.</summary>
        private const int Capacity = 4;

        [SetUp]
        public void SetUp()
        {
            BehaviorTreeFlightRecorders.Reset();
            BehaviorTreeFlightRecorders.GloballyEnabled = true;
        }

        [TearDown]
        public void TearDown()
        {
            BehaviorTreeFlightRecorders.Reset();
            BehaviorTreeFlightRecorders.GloballyEnabled = true;
        }

        private static BehaviorTreeFlightRecorder Saturated()
        {
            var recorder = new BehaviorTreeFlightRecorder("Agent", "Tree", Capacity);

            for (int i = 0; i < Capacity * 2; i++)
            {
                recorder.ExternalVariableWrite("sensor", "hasTarget", i, i + 1);
            }

            return recorder;
        }

        [Test]
        public void TheEventCountStopsMovingOnceTheRingHasWrapped()
        {
            var recorder = Saturated();

            Assert.AreEqual(Capacity, recorder.EventCount,
                "The premise of the whole finding: past capacity the count saturates and stops being a signal.");

            recorder.ExternalVariableWrite("sensor", "hasTarget", 99, 100);

            Assert.AreEqual(Capacity, recorder.EventCount);
            Assert.Greater(recorder.Dropped, 0, "Events really are still arriving; only the count has stopped.");
        }

        [Test]
        public void AStampChangesWhenTheTickAdvancesThoughTheEventCountCannot()
        {
            var recorder = Saturated();
            var before = BehaviorTreeRecordingStamp.Of(recorder);
            var countBefore = recorder.EventCount;

            recorder.BeginTick();
            recorder.ExternalVariableWrite("sensor", "hasTarget", 100, 101);

            Assert.AreEqual(countBefore, recorder.EventCount,
                "Guards the test itself: if the count moved, this would pass without the tick mattering.");
            Assert.AreNotEqual(before, BehaviorTreeRecordingStamp.Of(recorder),
                "A panel keyed on this must rebuild, or it freezes at the wrap while claiming to be live.");
        }

        [Test]
        public void AStampChangesWhenEventsArriveWithinOneTick()
        {
            var recorder = new BehaviorTreeFlightRecorder("Agent", "Tree", Capacity);

            recorder.BeginTick();
            recorder.ExternalVariableWrite("sensor", "hasTarget", 0, 1);

            var before = BehaviorTreeRecordingStamp.Of(recorder);
            var tickBefore = recorder.Tick;

            recorder.ExternalVariableWrite("sensor", "hasNoise", 0, 1);

            Assert.AreEqual(tickBefore, recorder.Tick, "The other half: the tick must not be what moved here.");
            Assert.AreNotEqual(before, BehaviorTreeRecordingStamp.Of(recorder));
        }

        [Test]
        public void AStampDoesNotChangeWhileTheRecordingSitsStill()
        {
            var recorder = Saturated();

            Assert.AreEqual(BehaviorTreeRecordingStamp.Of(recorder), BehaviorTreeRecordingStamp.Of(recorder),
                "Otherwise the caches this exists for would rebuild every repaint and buy nothing.");
        }

        [Test]
        public void TwoRecordingsThatLookAlikeDoNotShareAStamp()
        {
            var one = Saturated();
            var other = Saturated();

            Assert.AreEqual(one.Tick, other.Tick);
            Assert.AreEqual(one.EventCount, other.EventCount);
            Assert.AreNotEqual(BehaviorTreeRecordingStamp.Of(one), BehaviorTreeRecordingStamp.Of(other),
                "Switching agents has to rebuild even when the numbers line up, or one agent's table is "
                + "presented as the other's.");
        }

        [Test]
        public void NoneMatchesNothingButTheAbsenceOfARecording()
        {
            Assert.AreEqual(BehaviorTreeRecordingStamp.None, BehaviorTreeRecordingStamp.Of(null));
            Assert.AreNotEqual(BehaviorTreeRecordingStamp.None, BehaviorTreeRecordingStamp.Of(Saturated()));
        }
    }
}
