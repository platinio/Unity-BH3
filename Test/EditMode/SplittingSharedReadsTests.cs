using System.Linq;
using ArcaneOnyx.BehaviorTree.Authoring;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace ArcaneOnyx.BehaviorTree.Tests
{
    /// <summary>
    /// One node reading a variable for consumers at opposite ends of a canvas is a wire nobody can follow.
    /// The layout gives the far ones a read of their own, because two reads of one key are one fact read
    /// twice — and leaves the near ones sharing, because a short wire is easier to read than a duplicate.
    /// </summary>
    public class SplittingSharedReadsTests
    {
        private const string Folder = "Assets/__SplittingSharedReadsTests";
        private const string CallerPath = Folder + "/Caller.asset";
        private const string BranchPath = Folder + "/Branch.asset";

        private BehaviorTreeGraphAsset caller;
        private BehaviorTreeGraphAsset branch;
        private Selector selector;

        [SetUp]
        public void MakeTheFolder()
        {
            if (!AssetDatabase.IsValidFolder(Folder)) AssetDatabase.CreateFolder("Assets", "__SplittingSharedReadsTests");
        }

        [TearDown]
        public void DeleteTheTrees()
        {
            AssetDatabase.DeleteAsset(Folder);
        }

        [Test]
        public void GivesAFarConsumerAReadOfItsOwn()
        {
            GuardTwoBranchesOnOneRead();

            var added = BehaviorTreeLayout.Arrange(caller).Single(tree => tree.tree == CallerPath).readsAdded;

            Assert.That(added, Is.EqualTo(1));
            Assert.That(ReadsIn(caller).Count, Is.EqualTo(2), "the far guard did not get a read of its own");

            foreach (var read in ReadsIn(caller))
            {
                Assert.That(FarthestConsumerOf(read), Is.LessThanOrEqualTo(BehaviorTreeLayout.ReachOfAWire),
                    "a read is still wired to something out of reach");
            }
        }

        [Test]
        public void LeavesNeighboursSharingTheOneRead()
        {
            GuardTwoLeavesOnOneRead();

            var added = BehaviorTreeLayout.Arrange(caller).Single(tree => tree.tree == CallerPath).readsAdded;

            Assert.That(added, Is.Zero);
            Assert.That(ReadsIn(caller).Count, Is.EqualTo(1), "a short wire was replaced by a duplicate");
        }

        [Test]
        public void TheNewReadReadsWhatTheOldOneReads()
        {
            GuardTwoBranchesOnOneRead();

            BehaviorTreeLayout.Arrange(caller);

            var reads = ReadsIn(caller);
            Assert.That(reads[1].GetType(), Is.EqualTo(reads[0].GetType()));
            Assert.That(reads[1].NodeName, Is.EqualTo(reads[0].NodeName), "the copy reads a different variable");
        }

        [Test]
        public void EveryGuardStillHasSomethingWiredIntoIt()
        {
            GuardTwoBranchesOnOneRead();

            BehaviorTreeLayout.Arrange(caller);

            foreach (var guard in caller.graph.Nodes.OfType<ConditionalExecution>())
            {
                Assert.That(caller.graph.valueConnections.Any(wire => wire.destination.behaviorTreeNode as BehaviorTreeNode == guard),
                    Is.True, guard.NodeName + " was left reading nothing");
            }
        }

        /// <summary>Two branches wide enough that one read cannot sit beside both.</summary>
        private void GuardTwoBranchesOnOneRead()
        {
            branch = BehaviorTreeAuthoring.CreateTree(BranchPath);
            var sequence = BehaviorTreeAuthoring.AddNode<Sequence>(branch, 0.0f, 0.0f);
            BehaviorTreeAuthoring.Connect(branch, branch.graph.EntryNode, sequence);
            foreach (var _ in Enumerable.Range(0, 6))
            {
                BehaviorTreeAuthoring.Connect(branch, sequence, BehaviorTreeAuthoring.AddNode<WaitTime>(branch, 0.0f, 0.0f));
            }
            BehaviorTreeAuthoring.Save(branch);

            caller = BehaviorTreeAuthoring.CreateTree(CallerPath);
            selector = BehaviorTreeAuthoring.AddNode<Selector>(caller, 0.0f, 0.0f);
            BehaviorTreeAuthoring.Connect(caller, caller.graph.EntryNode, selector);

            var left = BehaviorTreeAuthoring.AddSubTree(caller, branch, 0.0f, 0.0f);
            var right = BehaviorTreeAuthoring.AddSubTree(caller, branch, 0.0f, 0.0f);
            BehaviorTreeAuthoring.Connect(caller, selector, left);
            BehaviorTreeAuthoring.Connect(caller, selector, right);

            ShareOneReadBetween(left, right);
        }

        /// <summary>Two leaves side by side, where a single read is beside both of them.</summary>
        private void GuardTwoLeavesOnOneRead()
        {
            caller = BehaviorTreeAuthoring.CreateTree(CallerPath);
            selector = BehaviorTreeAuthoring.AddNode<Selector>(caller, 0.0f, 0.0f);
            BehaviorTreeAuthoring.Connect(caller, caller.graph.EntryNode, selector);

            var left = BehaviorTreeAuthoring.AddNode<WaitTime>(caller, 0.0f, 0.0f);
            var right = BehaviorTreeAuthoring.AddNode<WaitTime>(caller, 0.0f, 0.0f);
            BehaviorTreeAuthoring.Connect(caller, selector, left);
            BehaviorTreeAuthoring.Connect(caller, selector, right);

            ShareOneReadBetween(left, right);
        }

        /// <summary>
        /// Guards both owners on the same variable, then throws the second guard's read away and wires the
        /// first one into it — which is the state a generator leaves behind when it reuses a read.
        /// </summary>
        private void ShareOneReadBetween(BehaviorTreeNode first, BehaviorTreeNode second)
        {
            BehaviorTreeAuthoring.GuardOnVariable(caller, first, "hasTarget", true, false, 0.0f, 0.0f);
            BehaviorTreeAuthoring.GuardOnVariable(caller, second, "hasTarget", true, false, 0.0f, 0.0f);

            var secondGuard = caller.graph.Nodes.OfType<ConditionalExecution>().Last();
            var wire = caller.graph.valueConnections.Single(each => each.destination.behaviorTreeNode as BehaviorTreeNode == secondGuard);
            var itsRead = wire.source.behaviorTreeNode as BehaviorTreeNode;
            var destination = wire.destination;

            caller.graph.valueConnections.Remove(wire);
            caller.graph.Nodes.Remove(itsRead);

            ReadsIn(caller).Single().Output.ValidlyConnectTo(destination);
            BehaviorTreeAuthoring.Save(caller);
        }

        private static System.Collections.Generic.List<VisualScriptGraphVariable> ReadsIn(BehaviorTreeGraphAsset tree) =>
            tree.graph.Nodes.OfType<VisualScriptGraphVariable>().ToList();

        private float FarthestConsumerOf(VisualScriptGraphVariable read)
        {
            var farthest = 0.0f;

            foreach (var wire in caller.graph.valueConnections.Where(each => each.source.behaviorTreeNode as BehaviorTreeNode == read))
            {
                var consumer = wire.destination.behaviorTreeNode as BehaviorTreeNode;
                var owner = consumer is ConditionalExecution guard ? guard.Owner : consumer;
                var gap = Mathf.Max(0.0f, Mathf.Max(read.Position.xMin - owner.Position.xMax, owner.Position.xMin - read.Position.xMax));

                farthest = Mathf.Max(farthest, gap);
            }

            return farthest;
        }
    }
}
