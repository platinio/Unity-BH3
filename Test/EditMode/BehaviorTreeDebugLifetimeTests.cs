using ArcaneOnyx.BehaviorTree.Debugging;
using NUnit.Framework;
using UnityEditor;

namespace ArcaneOnyx.BehaviorTree.Tests
{
    /// <summary>
    /// The debugger's two statics survive anything that does not explicitly clear them, including a domain
    /// reload when reload-on-play is disabled. These tests are about the one moment they must not survive.
    ///
    /// <para>
    /// The transition is driven by calling the handler rather than by entering play mode, which an EditMode
    /// test cannot do. That means the wiring itself -- that <see cref="EditorApplication"/> actually calls
    /// this -- is not covered here; it is a two-line static constructor copied from
    /// <c>BehaviorTreeBreakpointResponder</c>.
    /// </para>
    /// </summary>
    [TestFixture]
    public class BehaviorTreeDebugLifetimeTests
    {
        private BehaviorTreeFlightRecorder recorder;

        [SetUp]
        public void SetUp()
        {
            BehaviorTreeFlightRecorders.Reset();
            BehaviorTreeFlightRecorders.GloballyEnabled = true;

            recorder = new BehaviorTreeFlightRecorder("Agent", "Tree");
            recorder.BeginTick();
            recorder.ExternalVariableWrite("sensor", "hasTarget", false, true);
        }

        [TearDown]
        public void TearDown()
        {
            BehaviorTreeDebugLifetime.Reset();

            BehaviorTreeFlightRecorders.Reset();
            BehaviorTreeFlightRecorders.GloballyEnabled = true;
        }

        /// <summary>Parks the canvas on a moment, the way the timeline does when the playhead moves.</summary>
        private void Scrub()
        {
            var state = BehaviorTreeTreeState.At(recorder, recorder.Tick);

            BehaviorTreeScrubOverride.Set(state, recorder.AgentName, recorder);
            BehaviorTreeDebugSession.Publish(recorder, recorder.Tick, true);

            Assert.IsTrue(BehaviorTreeScrubOverride.IsActive, "Fixture check: nothing to clear otherwise.");
            Assert.IsNotNull(BehaviorTreeDebugSession.Recording, "Fixture check.");
        }

        [Test]
        public void LeavingPlayModeTakesTheScrubGhostOffTheCanvas()
        {
            Scrub();

            BehaviorTreeDebugLifetime.OnPlayModeStateChanged(PlayModeStateChange.ExitingPlayMode);

            Assert.IsFalse(BehaviorTreeScrubOverride.IsActive,
                "Otherwise the edit-mode canvas keeps drawing a dead run's statuses at ghost alpha, and the "
                + "only banner that would say so lives in a panel that may be collapsed.");
        }

        [Test]
        public void LeavingPlayModeStopsTheWatchBeingHandedADeadRecorder()
        {
            Scrub();

            BehaviorTreeDebugLifetime.OnPlayModeStateChanged(PlayModeStateChange.ExitingPlayMode);

            Assert.IsNull(BehaviorTreeDebugSession.Recording,
                "The panels drew this as live, and the static pinned its ring for as long as it held it.");
            Assert.AreEqual(-1, BehaviorTreeDebugSession.Tick);
            Assert.IsFalse(BehaviorTreeDebugSession.IsScrubbing);
        }

        /// <summary>
        /// The other half of the boundary, and the one that matters when reload-on-play is disabled: without
        /// it a new run starts wearing the previous run's ghost.
        /// </summary>
        [Test]
        public void EnteringPlayModeStartsFromThePresentToo()
        {
            Scrub();

            BehaviorTreeDebugLifetime.OnPlayModeStateChanged(PlayModeStateChange.EnteredPlayMode);

            Assert.IsFalse(BehaviorTreeScrubOverride.IsActive);
            Assert.IsNull(BehaviorTreeDebugSession.Recording);
        }

        /// <summary>
        /// The two are cleared together or not at all. Between them they say which recording and which tick,
        /// so one surviving the other is how the canvas and the panels end up describing different moments.
        /// </summary>
        [Test]
        public void TheCanvasAndTheSessionAreClearedTogether()
        {
            Scrub();

            BehaviorTreeDebugLifetime.Reset();

            Assert.IsFalse(BehaviorTreeScrubOverride.IsActive);
            Assert.IsNull(BehaviorTreeDebugSession.Recording);
        }
    }
}
