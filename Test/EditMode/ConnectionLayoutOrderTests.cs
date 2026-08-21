using System.Linq;
using ArcaneOnyx.BehaviorTree.Authoring;
using ArcaneOnyx.GraphCore;
using NUnit.Framework;
using UnityEditor;

namespace ArcaneOnyx.BehaviorTree.Tests
{
    /// <summary>
    /// A wire's <c>CachePosition</c> reads <c>handlePosition</c> off <em>both</em> of its endpoints, so both
    /// have to be laid out before it. <c>positionDependencies</c> is the only thing that says so —
    /// <c>CacheWidgetPositions</c> topologically sorts by it.
    ///
    /// <para>
    /// The list used to name the source twice and the destination never, and nothing showed: the canvas
    /// re-lays out every widget every repaint, so a wire positioned too early just read a rect a previous
    /// frame had already made correct. These pin the declaration itself rather than the symptom, because the
    /// symptom only appears once that per-frame relayout goes away — which is exactly when a silent
    /// dependency bug is most expensive to diagnose.
    /// </para>
    /// </summary>
    public class ConnectionLayoutOrderTests
    {
        private const string Folder = "Assets/__ConnectionLayoutOrderTests";

        private BehaviorTreeGraphAsset tree;
        private BehaviorTreeCanvas canvas;
        private PortValueConnection connection;

        [SetUp]
        public void SetUp()
        {
            if (!AssetDatabase.IsValidFolder(Folder))
                AssetDatabase.CreateFolder("Assets", "__ConnectionLayoutOrderTests");

            tree = BehaviorTreeAuthoring.CreateTree($"{Folder}/Tree.asset");

            // Wait.Time declares no default, so feeding it is a real wire between two nodes rather than an
            // inline value — which is what makes a connection widget exist at all.
            var wait = BehaviorTreeAuthoring.AddNode<WaitTime>(tree, 0.0f, 0.0f);
            BehaviorTreeAuthoring.FeedFloat(tree, wait.Time, 1.0f, -200.0f, 0.0f);

            canvas = new BehaviorTreeCanvas(tree.graph);
            connection = tree.graph.elements.OfType<PortValueConnection>().Single();
        }

        [TearDown]
        public void TearDown()
        {
            AssetDatabase.DeleteAsset(Folder);
        }

        private IWidget[] Dependencies =>
            XCanvasProvider.Widget(canvas, connection).positionDependencies.ToArray();

        [Test]
        public void AConnection_DependsOnItsSourcePort()
        {
            Assert.That(Dependencies, Has.Member(XCanvasProvider.Widget(canvas, connection.source)),
                "the wire reads the source handle's rect, so the source has to be laid out first");
        }

        [Test]
        public void AConnection_DependsOnItsDestinationPort()
        {
            Assert.That(Dependencies, Has.Member(XCanvasProvider.Widget(canvas, connection.destination)),
                "the wire reads the destination handle's rect too; leaving it out lets the wire lay out " +
                "against last frame's rect, which reads as a wire lagging a frame behind the node it lands on");
        }

        [Test]
        public void AConnection_DoesNotNameTheSameEndpointTwice()
        {
            var dependencies = Dependencies;

            Assert.That(dependencies.Distinct().Count(), Is.EqualTo(dependencies.Length),
                "a duplicate entry is how the missing destination hid — it kept the list the right length " +
                "while covering only one end");
        }
    }
}
