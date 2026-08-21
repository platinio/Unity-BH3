using ArcaneOnyx.BehaviorTree.Authoring;
using NUnit.Framework;
using UnityEditor;

namespace ArcaneOnyx.BehaviorTree.Tests
{
    /// <summary>
    /// The canvas's per-event bookkeeping — the dangling-element repair and the script-graph-asset sync —
    /// now runs when something has changed rather than on every GUI event.
    ///
    /// <para>
    /// <c>OnGUI</c> is not once a frame. It runs for layout, for repaint, and for every mouse-move, so a
    /// mouse crossing the canvas used to pay several times a frame for a full walk of the graph, a resync of
    /// the project-wide script-graph repository, a re-add of every element's assets and a sweep for unused
    /// ones. None of it is repaint work.
    /// </para>
    ///
    /// <para>
    /// The risk that buys is a repair that sleeps through something it should have caught, so these pin both
    /// directions: nothing changed means no repair, and a change means the repair happens on the next pass.
    /// <c>UpdateOwner(null)</c> is used as the probe because it is the one way to make an element dangling
    /// <em>without</em> touching <c>graph.elements</c> — which is exactly the "the canvas cannot know" case
    /// the pair needs to tell apart.
    /// </para>
    /// </summary>
    public class CanvasBookkeepingTests
    {
        private const string Folder = "Assets/__CanvasBookkeepingTests";

        private BehaviorTreeGraphAsset tree;
        private BehaviorTreeCanvas canvas;
        private Sequence owner;
        private ConditionalExecution guard;

        /// <summary>Set by the test that disposes the canvas itself, so teardown does not close it twice.</summary>
        private bool disposed;

        [SetUp]
        public void SetUp()
        {
            if (!AssetDatabase.IsValidFolder(Folder))
                AssetDatabase.CreateFolder("Assets", "__CanvasBookkeepingTests");

            tree = BehaviorTreeAuthoring.CreateTree($"{Folder}/Tree.asset");
            owner = BehaviorTreeAuthoring.AddNode<Sequence>(tree, 0.0f, 0.0f);
            guard = BehaviorTreeAuthoring.GuardOnVariable(tree, owner, "hasTarget", true, false, -200.0f, 0.0f);

            canvas = new BehaviorTreeCanvas(tree.graph);
            disposed = false;
        }

        [TearDown]
        public void TearDown()
        {
            if (!disposed) canvas.Close();

            AssetDatabase.DeleteAsset(Folder);
        }

        private bool GraphStillHasTheGuard => tree.graph.elements.Contains(guard);

        [Test]
        public void TheFirstPass_RepairsWhatIsAlreadyDangling()
        {
            tree.graph.elements.Remove(owner);

            canvas.SyncBookkeeping();

            Assert.That(GraphStillHasTheGuard, Is.False,
                "a canvas opening on an already-broken graph has to repair it, so the flag starts stale");
        }

        [Test]
        public void AChangeAfterAPass_IsPickedUpOnTheNextEvent()
        {
            canvas.SyncBookkeeping();

            tree.graph.elements.Remove(owner);
            canvas.SyncBookkeeping();

            Assert.That(GraphStillHasTheGuard, Is.False,
                "removing a node raises CollectionChanged, which is what re-arms the repair");
        }

        [Test]
        public void AnEventWithNothingChanged_DoesNotRepeatTheWork()
        {
            canvas.SyncBookkeeping();

            // Dangling, but by a route that raises no collection change -- so the canvas has been told
            // nothing, and skipping is the correct behaviour rather than a missed repair.
            guard.UpdateOwner(null);
            canvas.SyncBookkeeping();

            Assert.That(GraphStillHasTheGuard, Is.True,
                "if this repaired, the pass ran again with nothing to justify it -- which is the per-event " +
                "work this change exists to stop");
        }

        [Test]
        public void ThatSameDanglingElement_IsCaughtOnceSomethingElseChanges()
        {
            canvas.SyncBookkeeping();
            guard.UpdateOwner(null);
            canvas.SyncBookkeeping();

            // Any change at all re-arms the pass; the repair then sees everything, not just what changed.
            BehaviorTreeAuthoring.AddNode<WaitTime>(tree, 400.0f, 0.0f);
            canvas.SyncBookkeeping();

            Assert.That(GraphStillHasTheGuard, Is.False,
                "the flag re-arms a whole pass rather than tracking which element changed, so nothing is " +
                "permanently invisible to the repair");
        }

        /// <summary>
        /// A canvas is constructed and used without ever being opened by every sub-tree preview and every
        /// test, so <c>Close</c> is not what releases it — <c>Dispose</c> is. Until it did, those canvases
        /// stayed subscribed to two global editor events for the session.
        /// </summary>
        [Test]
        public void ADisposedCanvas_StopsListeningToTheGraph()
        {
            canvas.SyncBookkeeping();
            canvas.Dispose();

            // Raises CollectionChanged, which is what would re-arm the pass if the handler were still on.
            tree.graph.elements.Remove(owner);
            canvas.SyncBookkeeping();

            Assert.That(GraphStillHasTheGuard, Is.True,
                "a disposed canvas that still repaired would be one still subscribed to Undo and " +
                "projectChanged as well, which is what roots it for the session");

            disposed = true;
        }

        [Test]
        public void RepeatedEventsOnAnUnchangedGraph_LeaveItAlone()
        {
            for (int i = 0; i < 20; i++) canvas.SyncBookkeeping();

            Assert.That(GraphStillHasTheGuard, Is.True);
            Assert.That(tree.graph.elements.Contains(owner), Is.True,
                "a healthy graph must survive any number of GUI events untouched");
        }
    }
}
