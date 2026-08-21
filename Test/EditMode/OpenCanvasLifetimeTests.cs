using ArcaneOnyx.BehaviorTree.Authoring;
using NUnit.Framework;
using UnityEditor;

namespace ArcaneOnyx.BehaviorTree.Tests
{
    /// <summary>
    /// <see cref="BehaviorTreeCanvas.OpenBehaviorTreeCanvas"/> is how property drawers outside the canvas ask
    /// which tree the user is looking at, so its lifetime has to match a window's.
    ///
    /// <para>
    /// It used to be set in <c>Open</c> and never cleared. That rooted the canvas — every widget, every port
    /// and the graph — for the rest of the session, and it left both accessors answering confidently about a
    /// tree whose window had been closed, because <c>context</c> is the ambient edited context rather than
    /// the canvas's own: the static being non-null was the only thing separating a closed window from a wrong
    /// answer.
    /// </para>
    ///
    /// <para>
    /// The clear is guarded on identity, and that guard is the half worth pinning: opening a second tree can
    /// run <c>Open</c> on the new canvas before <c>Close</c> on the old one, so an unconditional null would
    /// retract the newer canvas's claim and make the accessors answer null while a window is plainly open.
    /// </para>
    /// </summary>
    public class OpenCanvasLifetimeTests
    {
        private const string Folder = "Assets/__OpenCanvasLifetimeTests";

        private BehaviorTreeGraphAsset tree;

        [SetUp]
        public void SetUp()
        {
            if (!AssetDatabase.IsValidFolder(Folder))
                AssetDatabase.CreateFolder("Assets", "__OpenCanvasLifetimeTests");

            tree = BehaviorTreeAuthoring.CreateTree($"{Folder}/Tree.asset");
        }

        [TearDown]
        public void TearDown()
        {
            // The static is global, so a test that left it set would leak into the next one.
            BehaviorTreeCanvas.OpenBehaviorTreeCanvas?.Close();

            AssetDatabase.DeleteAsset(Folder);
        }

        private BehaviorTreeCanvas NewCanvas() => new BehaviorTreeCanvas(tree.graph);

        [Test]
        public void OpeningACanvas_ClaimsTheStatic()
        {
            var canvas = NewCanvas();
            canvas.Open();

            Assert.That(BehaviorTreeCanvas.OpenBehaviorTreeCanvas, Is.SameAs(canvas));
        }

        [Test]
        public void ClosingTheOpenCanvas_ReleasesIt()
        {
            var canvas = NewCanvas();
            canvas.Open();
            canvas.Close();

            Assert.That(BehaviorTreeCanvas.OpenBehaviorTreeCanvas, Is.Null,
                "left set, the static roots the canvas, its widgets and the graph for the whole session");
        }

        [Test]
        public void ClosingACanvasThatIsNoLongerTheOpenOne_LeavesTheNewerClaimAlone()
        {
            var first = NewCanvas();
            var second = NewCanvas();

            first.Open();
            second.Open();
            first.Close();

            Assert.That(BehaviorTreeCanvas.OpenBehaviorTreeCanvas, Is.SameAs(second),
                "Open on the new canvas can run before Close on the old one; clearing unconditionally would " +
                "leave the accessors answering null while a window is open");
        }

        // Not covered here: that the accessors stop answering once the canvas closes. Both return null in a
        // headless test whatever the static holds, because the ambient edited context they read is only set
        // by a real graph window -- so such a test would pass for the wrong reason and could never fail.
    }
}
