using System;
using System.Collections.Generic;
using ArcaneOnyx.BehaviorTree.Debugging;
using NUnit.Framework;

namespace ArcaneOnyx.BehaviorTree.Tests
{
    /// <summary>
    /// One recording open at a time: that the panels which read a recording read the one the timeline
    /// published, rather than each resolving — or opening — their own.
    ///
    /// <para>
    /// This is the invariant that replaced the why-inspector's own Load and Export buttons. With two Loads in
    /// the editor, two panels could be on two different recordings at once, and nothing on screen said so:
    /// the explanation named ticks from a file while the canvas beside it was ghosted to a live agent. Every
    /// test here is therefore about <em>which</em> recording a panel answers for, never about what it says.
    /// </para>
    ///
    /// <para>
    /// The session is a static that a real timeline panel republishes every repaint, so these tests clear it
    /// between cases rather than assuming it starts empty. Nothing they leave behind matters — the next panel
    /// to draw overwrites it, which is the same reason the scrub-target tests give.
    /// </para>
    /// </summary>
    [TestFixture]
    public class BehaviorTreeRecordingSourceTests
    {
        /// <summary>
        /// A recording with nothing in it. Every assertion here is about identity — which recording came
        /// back — so events would only be scenery.
        /// </summary>
        private sealed class NamedRecording : IBehaviorTreeRecording
        {
            private readonly List<BehaviorTreeCallSite> callSites = new()
            {
                new BehaviorTreeCallSite(
                    BehaviorTreeCallSite.RootId, BehaviorTreeCallSite.RootId, Guid.Empty, "Tree"),
            };

            public NamedRecording(string agentName)
            {
                AgentName = agentName;
            }

            public string AgentName { get; }
            public string TreeName => "Tree";
            public int Tick => 0;
            public int EventCount => 0;
            public IReadOnlyList<BehaviorTreeCallSite> CallSites => callSites;
            public int Dropped => 0;

            public BehaviorTreeEvent EventAt(int index) => throw new IndexOutOfRangeException();

            public GuardTrace TraceFor(int tick, int sequence) => null;
        }

        private static BehaviorTreeWhyPanel Panel() => new BehaviorTreeWhyPanel(null);

        [SetUp]
        public void ClearSession()
        {
            BehaviorTreeDebugSession.Clear();

            // The target memo outlives a test, and a remembered live agent would answer the fallback before
            // the fallback could be observed doing anything.
            BehaviorTreeDebugTarget.Forget();
        }

        [TearDown]
        public void ClearSessionAfter()
        {
            BehaviorTreeDebugSession.Clear();
            BehaviorTreeDebugTarget.Forget();
        }

        [Test]
        public void TheWhyPanelExplainsWhateverTheTimelineOpened()
        {
            var opened = new NamedRecording("Goblin");

            BehaviorTreeDebugSession.Publish(opened, 42, true, "File: ambush.json");

            Assert.AreSame(opened, Panel().CurrentRecording(),
                "The why panel has no Load of its own; it explains the recording the timeline published.");
        }

        [Test]
        public void TheWhyPanelFollowsTheTimelineToADifferentRecording()
        {
            var first = new NamedRecording("Goblin");
            var second = new NamedRecording("Ogre");
            var panel = Panel();

            BehaviorTreeDebugSession.Publish(first, 10, false, "Goblin  (shown on this canvas)");
            Assert.AreSame(first, panel.CurrentRecording());

            BehaviorTreeDebugSession.Publish(second, 10, false, "Ogre  (shown on this canvas)");

            Assert.AreSame(second, panel.CurrentRecording(),
                "Opening another recording in the timeline has to move the explanation with it, or the two "
                + "panels are back to describing different agents.");
        }

        [Test]
        public void TheWhyPanelStopsReadingARecordingTheTimelineHasClosed()
        {
            var closed = new NamedRecording("Goblin");
            var panel = Panel();

            BehaviorTreeDebugSession.Publish(closed, 42, true, "File: ambush.json");
            BehaviorTreeDebugSession.Clear();

            // Asserted as "not that one" rather than "null": the fallback resolves a live agent, and an editor
            // running these tests may have one selected. What must never happen is the closed file surviving.
            Assert.AreNotSame(closed, panel.CurrentRecording(),
                "Closing the file in the timeline has to close it everywhere — a panel still holding it is "
                + "the second open recording this design removes.");
        }

        [Test]
        public void ClosingTheRecordingAlsoDropsWhereItCameFrom()
        {
            BehaviorTreeDebugSession.Publish(new NamedRecording("Goblin"), 42, true, "File: ambush.json");

            Assert.AreEqual("File: ambush.json", BehaviorTreeDebugSession.Origin);

            BehaviorTreeDebugSession.Clear();

            // A surviving origin is worse than a missing one: the panels would go on captioning themselves
            // with a file that is no longer open, and a play-mode boundary clears through here.
            Assert.IsNull(BehaviorTreeDebugSession.Origin);
        }

        [Test]
        public void NothingToDescribeReadsAsNothingRatherThanAsAnEmptyCaption()
        {
            // The panels chain this behind the session's own origin with ??, so an empty string here would
            // win that chain and caption the panel with a blank line instead of falling through.
            Assert.IsNull(BehaviorTreeDebugTarget.Describe(null),
                "Describe returns null when nothing says which agent, so callers can fall through to their "
                + "own wording.");
        }
    }
}
