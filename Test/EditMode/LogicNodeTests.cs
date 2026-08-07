using NUnit.Framework;
using UnityEngine;

namespace ArcaneOnyx.BehaviorTree.Tests
{
    /// <summary>
    /// <see cref="Not"/> and <see cref="IsNotNull"/> exist to be read by guards. Guards on one owner stack
    /// as an AND, so a branch states its full precondition — including "and no higher-priority state is
    /// active" — through its own guards rather than through its position among its siblings. These tests
    /// pin the two pieces that made that expressible.
    /// </summary>
    [TestFixture]
    public class LogicNodeTests
    {
        private static T AddNode<T>(BehaviorTreeGraph graph) where T : BehaviorTreeNode, new()
        {
            // ports only exist once the node is in a graph: Nodes.Add fires AfterAdd -> Define()
            var node = new T { Position = new Rect(0.0f, 0.0f, 150.0f, 100.0f) };
            graph.Nodes.Add(node);

            return node;
        }

        [Test]
        public void Not_InvertsItsInput()
        {
            var graph = new BehaviorTreeGraph();
            var not = AddNode<Not>(graph);

            not.Value.SetDefaultValue(true);
            Assert.IsFalse((bool) not.Result.GetPortValue());

            not.Value.SetDefaultValue(false);
            Assert.IsTrue((bool) not.Result.GetPortValue());
        }

        [Test]
        public void Not_DefaultsToTrue()
        {
            // an unwired Value reads false, so the inverse is true — a guard fed by an unfinished Not lets
            // its branch run, which is visible on the canvas rather than silently disabling a branch
            var graph = new BehaviorTreeGraph();
            var not = AddNode<Not>(graph);

            Assert.IsTrue((bool) not.Result.GetPortValue());
        }

        [Test]
        public void Not_FeedsAGuard()
        {
            var graph = new BehaviorTreeGraph();
            var sequence = AddNode<Sequence>(graph);
            var not = AddNode<Not>(graph);
            var guard = AddNode<BooleanConditionalExecution>(graph);

            guard.UpdateOwner(sequence);
            not.Result.ValidlyConnectTo(guard.Value);

            not.Value.SetDefaultValue(false);
            Assert.IsTrue(guard.Evaluate(), "Not(false) must let the guarded branch run.");

            not.Value.SetDefaultValue(true);
            Assert.IsFalse(guard.Evaluate(), "Not(true) must invalidate the guarded branch.");
        }

        [Test]
        public void IsNotNull_ReportsWhetherTheReferenceIsLive()
        {
            var graph = new BehaviorTreeGraph();
            var isNotNull = AddNode<IsNotNull>(graph);

            Assert.IsFalse((bool) isNotNull.Result.GetPortValue(), "An unwired reference is not a target.");

            var gameObject = new GameObject("LogicNodeTests_Target");

            try
            {
                isNotNull.Value.SetDefaultValue(gameObject.transform);
                Assert.IsTrue((bool) isNotNull.Result.GetPortValue());

                Object.DestroyImmediate(gameObject);

                // the C# reference is still non-null here; only Unity's own comparison knows better, which
                // is the whole reason the port is typed as a UnityEngine.Object
                Assert.IsFalse((bool) isNotNull.Result.GetPortValue(),
                    "A destroyed target must read as null, otherwise a branch keeps chasing a dead transform.");
            }
            finally
            {
                if (gameObject != null) Object.DestroyImmediate(gameObject);
            }
        }
    }
}
