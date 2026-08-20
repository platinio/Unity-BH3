using NUnit.Framework;

namespace ArcaneOnyx.BehaviorTree.Tests
{
    /// <summary>
    /// Which timeline panel an editor-wide scrub request moves.
    ///
    /// <para>
    /// The subject is a static, and these tests exercise it directly rather than through a graph window.
    /// That is deliberate: the bug being fixed is that the static named the panel that was constructed most
    /// recently instead of the one on screen, and construction is exactly what a test can drive. Drawing is
    /// stood in for by <see cref="BehaviorTreeTimelinePanel.ClaimScrubTarget"/>, which is the single line
    /// <c>OnGUI</c> calls.
    /// </para>
    ///
    /// <para>
    /// No assertion depends on the claim starting out empty, because it does not: a graph window open in the
    /// editor running these tests has a real panel claiming it every repaint. That is also why the panels
    /// these tests leave behind are harmless — the next real panel to draw takes the claim straight back.
    /// </para>
    /// </summary>
    [TestFixture]
    public class BehaviorTreeTimelineScrubTargetTests
    {
        private static BehaviorTreeTimelinePanel Panel() => new BehaviorTreeTimelinePanel(null);

        [Test]
        public void AFreshPanelIsLiveRatherThanScrubbed()
        {
            Assert.AreEqual(-1, Panel().ScrubTick, "Otherwise every other assertion here could pass by accident.");
        }

        /// <summary>
        /// The regression itself. The order matters and is the whole point: the second panel is constructed
        /// <em>after</em> the first has drawn, which is what a second graph window opening — or a reference
        /// switch rebuilding the context — actually looks like.
        /// </summary>
        [Test]
        public void ConstructingAnotherPanelDoesNotTakeTheTargetFromThePanelOnScreen()
        {
            var onScreen = Panel();
            onScreen.ClaimScrubTarget();

            var constructedLater = Panel();

            BehaviorTreeTimelinePanel.RequestScrub(42);

            Assert.AreEqual(42, onScreen.ScrubTick);
            Assert.AreEqual(-1, constructedLater.ScrubTick,
                "A second graph window's panel is constructed after the first window's and may never draw — "
                + "its collapsed dock must not swallow a tick link clicked in the window you can see.");
        }

        [Test]
        public void DrawingTakesTheTargetBackFromAPanelThatHasStoppedDrawing()
        {
            var first = Panel();
            var second = Panel();

            first.ClaimScrubTarget();
            BehaviorTreeTimelinePanel.RequestScrub(10);

            second.ClaimScrubTarget();
            BehaviorTreeTimelinePanel.RequestScrub(20);

            Assert.AreEqual(20, second.ScrubTick);
            Assert.AreEqual(10, first.ScrubTick,
                "The claim is self-healing: whatever stale panel holds it, the next one to draw corrects it.");
        }

        [Test]
        public void ANegativeTickIsRefusedRatherThanTreatedAsLive()
        {
            var panel = Panel();

            panel.ClaimScrubTarget();
            BehaviorTreeTimelinePanel.RequestScrub(7);
            BehaviorTreeTimelinePanel.RequestScrub(-1);

            Assert.AreEqual(7, panel.ScrubTick,
                "-1 is the panel's own word for 'live', so accepting it here would let a caller unscrub by "
                + "asking for a tick that does not exist.");
        }
    }
}
