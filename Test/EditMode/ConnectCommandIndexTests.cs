#if UNITY_PIPELINE_EXIST
using System.Linq;
using ArcaneOnyx.BehaviorTree.Authoring;
using NUnit.Framework;
using UnityEditor;

namespace ArcaneOnyx.BehaviorTree.Tests
{
    /// <summary>
    /// <c>bt_connect</c> without <c>--index</c> gives the child the next free slot, as
    /// <see cref="BehaviorTreeAuthoring.Connect(BehaviorTreeGraphAsset, BehaviorTreeNode, BehaviorTreeNode, int)"/>
    /// does and as the docs say.
    ///
    /// <para>
    /// It used to default to 0, so every child a tool connected stored the same index. A set of equal
    /// indices is not an order, and the tree fell back to canvas position: connecting branches in the order
    /// they should be tried only worked when the X coordinates happened to agree.
    /// </para>
    ///
    /// <para>
    /// Compiled only with <c>com.unity.pipeline</c> installed, like the commands themselves.
    /// </para>
    /// </summary>
    public class ConnectCommandIndexTests
    {
        private const string Folder = "Assets/__ConnectCommandIndexTests";
        private const string TreePath = Folder + "/Tree.asset";

        private BehaviorTreeGraphAsset tree;
        private Selector parent;
        private WaitTime left, middle, right;

        [SetUp]
        public void SetUp()
        {
            if (!AssetDatabase.IsValidFolder(Folder))
                AssetDatabase.CreateFolder("Assets", "__ConnectCommandIndexTests");

            tree = BehaviorTreeAuthoring.CreateTree(TreePath);

            parent = BehaviorTreeAuthoring.AddNode<Selector>(tree, 200.0f, 0.0f);
            left = BehaviorTreeAuthoring.AddNode<WaitTime>(tree, 100.0f, 200.0f);
            middle = BehaviorTreeAuthoring.AddNode<WaitTime>(tree, 200.0f, 200.0f);
            right = BehaviorTreeAuthoring.AddNode<WaitTime>(tree, 300.0f, 200.0f);

            BehaviorTreeAuthoring.Save(tree);
        }

        [TearDown]
        public void TearDown()
        {
            AssetDatabase.DeleteAsset(Folder);
        }

        private void ConnectByCommand(BehaviorTreeNode child) =>
            BehaviorTreeCommands.ConnectCommand(TreePath, parent.guid.ToString(), child.guid.ToString());

        private int IndexOf(BehaviorTreeNode child) =>
            tree.graph.Transitions.Single(transition => transition.destination == child).TransitionIndex;

        private BehaviorTreeNode[] Order() =>
            tree.graph.ChildTransitionsInPriorityOrder(parent).Select(t => t.destination).ToArray();

        [Test]
        public void WithoutAnIndex_EachChildTakesTheNextFreeSlot()
        {
            ConnectByCommand(left);
            ConnectByCommand(middle);
            ConnectByCommand(right);

            Assert.That(new[] { IndexOf(left), IndexOf(middle), IndexOf(right) }, Is.EqualTo(new[] { 0, 1, 2 }));
        }

        [Test]
        public void WithoutAnIndex_TheCallOrderIsThePriority_WhateverTheLayout()
        {
            // Right to left, against the layout: equal indices would fall back to position and run left first.
            ConnectByCommand(right);
            ConnectByCommand(middle);
            ConnectByCommand(left);

            Assert.That(Order(), Is.EqualTo(new BehaviorTreeNode[] { right, middle, left }));
        }

        [Test]
        public void AnExplicitIndex_IsStoredAsGiven()
        {
            BehaviorTreeCommands.ConnectCommand(TreePath, parent.guid.ToString(), left.guid.ToString(), 1);
            BehaviorTreeCommands.ConnectCommand(TreePath, parent.guid.ToString(), right.guid.ToString(), 0);

            Assert.That(Order(), Is.EqualTo(new BehaviorTreeNode[] { right, left }));
        }
    }
}
#endif
