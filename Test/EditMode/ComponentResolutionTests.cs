using NUnit.Framework;
using UnityEngine;
using Object = UnityEngine.Object;

namespace ArcaneOnyx.BehaviorTree.Tests
{
    /// <summary>
    /// A node that resolves a component from a port has to resolve it again each time it is entered.
    ///
    /// <para>
    /// The navigation nodes did it once in <c>OnAwake</c>, which is indistinguishable from correct for the
    /// common case — a <c>Target</c> left unconnected, falling back to the agent the machine sits on, which
    /// never changes. It is wrong the moment the port is fed by anything whose value moves: a blackboard
    /// variable, a selector's output, a sub-tree parameter. The node then keeps acting on whatever the port
    /// held at wake time, for the lifetime of the object, with nothing to show that it is ignoring the wire.
    /// </para>
    ///
    /// <para>
    /// <c>SetPosition</c> stands in for all of them here because it is the one that can be driven without a
    /// machine: every <c>GameObject</c> has a <c>Transform</c>, so the lookup never reaches the
    /// self-fallback, which is the only part that needs a live agent.
    /// </para>
    /// </summary>
    [TestFixture]
    public class ComponentResolutionTests
    {
        private BehaviorTreeGraph graph;
        private GameObject first;
        private GameObject second;

        [SetUp]
        public void SetUp()
        {
            graph = new BehaviorTreeGraph();
            first = new GameObject("First Target");
            second = new GameObject("Second Target");
        }

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(first);
            Object.DestroyImmediate(second);
        }

        private T AddNode<T>() where T : BehaviorTreeNode, new()
        {
            // Ports only exist once the node is in a graph: Nodes.Add fires AfterAdd -> Define().
            var node = new T { Position = new Rect(0.0f, 0.0f, 150.0f, 100.0f) };
            graph.Nodes.Add(node);

            return node;
        }

        [Test]
        public void ANodeResolvesItsTargetAgainOnEveryEntry()
        {
            var target = AddNode<ValueSource<GameObject>>();
            var destination = AddNode<ValueSource<Vector3>>();
            var node = AddNode<SetPosition>();

            first.transform.position = Vector3.one;
            second.transform.position = Vector3.one * 2.0f;

            target.Value.ValidlyConnectTo(node.Target);
            destination.Value.ValidlyConnectTo(node.NewPosition);

            var firstDestination = new Vector3(9.0f, 9.0f, 9.0f);
            target.Published = first;
            destination.Published = firstDestination;

            node.OnEnter();
            node.OnUpdate();

            Assert.AreEqual(firstDestination, first.transform.position,
                "The node did not act on the target its port published, so the rest of this test would be "
                + "measuring the fixture rather than the node.");

            // A different destination for the second entry, so which object moved is answerable. With one
            // destination for both entries a stale target would land on the value it already had.
            var secondDestination = new Vector3(5.0f, 5.0f, 5.0f);
            target.Published = second;
            destination.Published = secondDestination;

            node.OnEnter();
            node.OnUpdate();

            Assert.AreEqual(secondDestination, second.transform.position,
                "The second entry did not move the target the port published by then. The node is still "
                + "holding the component it resolved on an earlier entry.");

            Assert.AreEqual(firstDestination, first.transform.position,
                "The second entry moved the first target, so the node never re-read its port -- which is "
                + "the OnAwake caching bug, just relocated.");
        }
    }
}
