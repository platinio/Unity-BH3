using System.Collections;
using NUnit.Framework;
using UnityEngine.TestTools;

namespace ArcaneOnyx.BehaviorTree.Tests.PlayMode
{
    /// <summary>
    /// Pins the port contracts of the navigation nodes — the kind of defect where a port's declared type and
    /// the cast in <c>OnUpdate</c> disagree, which no verifier can see because the tree is perfectly well
    /// formed right up until the first tick.
    /// </summary>
    public class NavigationNodePortTests : PlayModeAgentFixture
    {
        /// <summary>
        /// <c>SampleDistance</c> is declared <c>ValueInput&lt;float&gt;</c>, so the boxed value arriving at
        /// <c>OnUpdate</c> is a <c>float</c> — unboxing it as <c>int</c> throws <c>InvalidCastException</c>
        /// on every tick, including with the port's own declared default. The node then can never return
        /// anything, which presents as an agent that simply stops in whatever branch reached it.
        /// </summary>
        [UnityTest]
        public IEnumerator SampleDistance_IsReadAsTheFloatItsPortDeclares()
        {
            var tree = NewTree();
            var graph = tree.graph;

            var node = Add<GenerateRandomNavMeshPosition>(graph, 0.0f, 200.0f);
            FeedFloat(graph, node, node.SampleDistance, 4.0f);
            FeedString(graph, node, node.PositionKey, "probePoint");
            Connect(graph, graph.EntryNode, node);

            var guid = node.guid;
            var machine = Spawn(tree);

            // One real frame is enough: the cast sits in OnUpdate's argument evaluation, so a broken node
            // throws before it can succeed or fail. With no NavMesh in the test scene the healthy outcome is
            // a clean Failure — what matters is that the node exits at all instead of throwing every tick.
            yield return null;
            yield return null;

            Assert.IsTrue(Entered(machine.FlightRecorder, guid),
                "The node never entered — the fixture wiring is wrong, not the cast under test.");
            Assert.GreaterOrEqual(ExitCount(machine.FlightRecorder, guid), 1,
                "The node entered but never exited: its OnUpdate cannot complete, which is exactly what an "
                + "InvalidCastException on a port read looks like from outside.");
        }
    }
}
