using System.Reflection;
using ArcaneOnyx.BehaviorTree.Debugging;
using ArcaneOnyx.GraphCore;
using NUnit.Framework;
using UnityEngine;

namespace ArcaneOnyx.BehaviorTree.Tests
{
    /// <summary>
    /// What a guard is called everywhere the debugger names one — the why-inspector's sentences, the
    /// breakpoints panel's rows, the timeline's abort pins.
    ///
    /// <para>
    /// Every <c>BooleanReactiveGuard</c> in a project is called "Boolean Reactive Guard", so the topology
    /// names a guard after the value it reads instead. These pin where that walk stops: it steps past the
    /// operators in between, because naming a guard "Not" is no better than naming it after its own type, and
    /// it stops at the variable read — following the literal that supplies the <em>key</em> would name the
    /// guard "String Literal", which is the plumbing carrying the question rather than the thing being asked.
    /// </para>
    /// </summary>
    [TestFixture]
    public class BehaviorTreeGuardNamingTests
    {
        [Test]
        public void AGuardIsNamedAfterTheVariableItReads()
        {
            var graph = new BehaviorTreeGraph();
            var owner = AddNode<WaitTime>(graph);
            var guard = AddNode<BooleanReactiveGuard>(graph);

            guard.UpdateOwner(owner);
            ReadVariable(graph, "hasTarget").Value.ValidlyConnectTo(guard.Value);

            Assert.AreEqual("hasTarget", NameOf(graph, guard),
                "The guard is named by what it asks about. 'String Literal' is the node holding the key, and "
                + "'Get Variable' is the same string for every such guard in the project.");
        }

        [Test]
        public void TheWalkStepsPastAnOperatorButStillStopsAtTheRead()
        {
            // "not hasTarget" is one operator away from the read. The Not is plumbing — the guard is still
            // about hasTarget — but the walk must not carry on through the read into its key literal.
            var graph = new BehaviorTreeGraph();
            var owner = AddNode<WaitTime>(graph);
            var guard = AddNode<BooleanReactiveGuard>(graph);
            var not = AddNode<Not>(graph);

            guard.UpdateOwner(owner);
            ReadVariable(graph, "hasTarget").Value.ValidlyConnectTo(not.Value);
            not.Result.ValidlyConnectTo(guard.Value);

            Assert.AreEqual("hasTarget", NameOf(graph, guard));
        }

        [Test]
        public void AGuardWithNothingConnectedKeepsItsOwnName()
        {
            // There is no better answer available, and inventing one would be worse than the dull truth.
            var graph = new BehaviorTreeGraph();
            var owner = AddNode<WaitTime>(graph);
            var guard = AddNode<BooleanReactiveGuard>(graph);

            guard.UpdateOwner(owner);

            Assert.AreEqual(guard.NodeName, NameOf(graph, guard));
        }

        #region Fixtures

        private static string NameOf(BehaviorTreeGraph graph, BehaviorTreeNode guard)
        {
            Assert.IsTrue(BehaviorTreeGraphTopology.From(graph).TryGetNode(guard.guid, out var info),
                "The topology has to know the guard before it can name it.");

            return info.DisplayName;
        }

        /// <summary>A variable read whose key comes from a literal, which is how the authoring helpers build one.</summary>
        private static GetVariable ReadVariable(BehaviorTreeGraph graph, string key)
        {
            var keyLiteral = AddNode<StringLiteral>(graph);
            SetPrivateField(keyLiteral, "value", key);

            var read = AddNode<GetVariable>(graph);
            SetPrivateField(read, "VariableKind", BehaviorTreeVariableKind.Object);
            keyLiteral.Value.ValidlyConnectTo(read.Key);

            return read;
        }

        private static T AddNode<T>(BehaviorTreeGraph graph) where T : BehaviorTreeNode, new()
        {
            var node = new T { Position = new Rect(0.0f, 0.0f, 150.0f, 100.0f) };
            graph.Nodes.Add(node);

            return node;
        }

        private static void SetPrivateField(object target, string field, object value)
        {
            var info = target.GetType().GetField(field, BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.IsNotNull(info, $"{target.GetType().Name} has no field '{field}'.");

            info.SetValue(target, value);
        }

        #endregion
    }
}
