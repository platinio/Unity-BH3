using System;
using System.Linq;
using System.Text.RegularExpressions;
using ArcaneOnyx.BehaviorTree.Authoring;
using ArcaneOnyx.GraphCore;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.TestTools;

namespace ArcaneOnyx.BehaviorTree.Tests
{
    /// <summary>
    /// A guard is attached to its owner; it is never a child. Nothing enforced that, so a transition could be
    /// wired <em>into</em> a guard — the canvas accepted the drop, the authoring API accepted the call, and at
    /// runtime the guard became child 0 of its parent and returned Success on every tick. The owner it was
    /// meant to gate sat orphaned beside it, and the tree ran and did nothing, with no error anywhere.
    ///
    /// <para>
    /// These pin every place that has to agree on the rule: the node's own transition flags, the authoring
    /// API, the verification report, the runtime child collection, where a canvas drop over a guard lands,
    /// and the repair that mends a tree saved before the rule existed.
    /// </para>
    /// </summary>
    [TestFixture]
    public class GuardTransitionDestinationTests
    {
        private const string Folder = "Assets/__GuardTransitionDestinationTests";
        private const string TreePath = Folder + "/Tree.asset";

        [SetUp]
        public void SetUp()
        {
            if (!AssetDatabase.IsValidFolder(Folder))
            {
                AssetDatabase.CreateFolder("Assets", "__GuardTransitionDestinationTests");
            }
        }

        [TearDown]
        public void TearDown()
        {
            AssetDatabase.DeleteAsset(Folder);
        }

        /// <summary>
        /// Entry -> Selector, plus a Sequence with a guard stacked on it that nothing reaches. Returns the
        /// pieces so a test can wire the Selector into the guard the way it wants to.
        /// </summary>
        private static BehaviorTreeGraphAsset TreeWithAGuardedOrphan(
            out Selector selector, out Sequence owner, out BooleanConditionalExecution guard)
        {
            var asset = BehaviorTreeAuthoring.CreateTree(TreePath);
            selector = BehaviorTreeAuthoring.AddNode<Selector>(asset, 0.0f, 100.0f);
            owner = BehaviorTreeAuthoring.AddNode<Sequence>(asset, -200.0f, 300.0f);
            guard = BehaviorTreeAuthoring.AddNode<BooleanConditionalExecution>(asset, -200.0f, 250.0f);
            guard.UpdateOwner(owner);
            BehaviorTreeAuthoring.Connect(asset, asset.graph.EntryNode, selector);

            return asset;
        }

        // ------------------------------------------------------------------ the node

        private static Type[] ConcreteGuards() =>
            typeof(ConditionalExecution).Assembly.GetTypes()
                .Where(t => typeof(ConditionalExecution).IsAssignableFrom(t) && t.IsClass && !t.IsAbstract)
                .Where(t => t.GetConstructor(Type.EmptyTypes) != null)
                .OrderBy(t => t.Name)
                .ToArray();

        [Test]
        public void EveryGuardRefusesToBeATransitionDestinationOrSource()
        {
            var guards = ConcreteGuards();
            Assert.IsNotEmpty(guards, "the scan found no concrete guard, so the rule below would pass on nothing");

            foreach (var type in guards)
            {
                var guard = (ConditionalExecution)Activator.CreateInstance(type);

                Assert.IsFalse(guard.CanBeUsedAsTransitionDestination,
                    $"{type.Name} accepts an incoming transition. A guard attaches to its owner and is never a child; " +
                    "accepting one is what let the canvas wire a Selector into a guard instead of the node it stacks on.");
                Assert.IsFalse(guard.CanBeUsedAsTransitionSource,
                    $"{type.Name} can start a transition. A guard has no children to point at.");
            }
        }

        // ------------------------------------------------------------------ authoring

        [Test]
        public void ConnectRefusesAGuardAsTheChild()
        {
            var asset = TreeWithAGuardedOrphan(out var selector, out _, out var guard);

            Assert.Throws<InvalidOperationException>(() => BehaviorTreeAuthoring.Connect(asset, selector, guard),
                "Connect accepted a guard as the child. bt_connect goes through here, so a scripted author gets " +
                "the same silent dead tree the canvas produced.");

            Assert.AreEqual(1, asset.graph.Transitions.Count, "the refused transition must not have been added");
        }

        [Test]
        public void ConnectRefusesAGuardAsTheParent()
        {
            var asset = TreeWithAGuardedOrphan(out _, out _, out var guard);
            var leaf = BehaviorTreeAuthoring.AddNode<WaitTime>(asset, -200.0f, 500.0f);

            Assert.Throws<InvalidOperationException>(() => BehaviorTreeAuthoring.Connect(asset, guard, leaf),
                "a guard has no children to point at");

            Assert.AreEqual(1, asset.graph.Transitions.Count);
        }

        // ------------------------------------------------------------------ verification

        [Test]
        public void VerifyReportsATransitionThatEndsOnAGuard()
        {
            var asset = TreeWithAGuardedOrphan(out var selector, out _, out var guard);
            asset.graph.WireUnchecked(selector, guard, 0);
            BehaviorTreeAuthoring.Save(asset);

            var findings = BehaviorTreeVerification.Verify(TreePath);

            Assert.IsTrue(findings.Any(f => f.Contains("ends on guard")),
                "bt_verify only reported the orphaned owner, which sends the reader looking for a missing wire " +
                "rather than at the wire that landed on the wrong node. Findings:\n" + string.Join("\n", findings));
        }

        // ------------------------------------------------------------------ runtime

        /// <summary>
        /// The tutorial tree, reduced: Entry -> Selector -> [guard of an orphaned Sequence, a second Sequence].
        /// The second branch is the only one that can do anything, and before the fix it never ran.
        /// </summary>
        [Test]
        public void ATransitionIntoAGuardIsNotAChildAndTheSiblingBranchStillRuns()
        {
            var asset = TreeWithAGuardedOrphan(out var selector, out _, out var guard);
            var sibling = BehaviorTreeAuthoring.AddNode<Sequence>(asset, 200.0f, 300.0f);
            var work = BehaviorTreeAuthoring.AddNode<ScriptedNode>(asset, 200.0f, 500.0f);

            asset.graph.WireUnchecked(selector, guard, 0);
            // Explicit index: the next-free-slot default may or may not count the bad wire above, and this
            // test is not about which.
            BehaviorTreeAuthoring.Connect(asset, selector, sibling, 1);
            BehaviorTreeAuthoring.Connect(asset, sibling, work);

            // Skipped loudly, not silently: an asset saved in this state fails where someone can see it.
            LogAssert.Expect(LogType.Error, new Regex("ends on guard"));

            asset.graph.OnAwake();

            var children = selector.GetChildren();
            Assert.IsFalse(children.Contains(guard),
                "the guard became a child of the Selector; as a plain child it returns Success every tick and " +
                "no other branch is ever tried");
            Assert.IsTrue(children.Contains(sibling), "the legitimate branch must still be a child");

            Assert.AreEqual(ExecutionStatus.Success, selector.RunToCompletion());
            Assert.AreEqual(1, work.UpdateCalls, "the sibling branch is the only one that can do anything, and it must run");
        }

        // ------------------------------------------------------------------ the canvas drop

        [Test]
        public void ADropOnAGuardLandsOnItsOwner()
        {
            var asset = TreeWithAGuardedOrphan(out _, out var owner, out var guard);

            Assert.AreSame(owner, BehaviorTreeCanvas.TransitionDestinationFor(asset.graph, guard),
                "the guard box sits exactly where a line aimed at the owner lands, so a release there is a " +
                "release on the owner");
        }

        [Test]
        public void ADropOnAnOrdinaryNodeLandsOnThatNode()
        {
            var asset = TreeWithAGuardedOrphan(out _, out var owner, out _);

            Assert.AreSame(owner, BehaviorTreeCanvas.TransitionDestinationFor(asset.graph, owner));
        }

        [Test]
        public void ADropOnAGuardWhoseOwnerIsGoneLandsNowhere()
        {
            var asset = TreeWithAGuardedOrphan(out _, out var owner, out var guard);
            asset.graph.elements.Remove(owner);

            Assert.IsNull(BehaviorTreeCanvas.TransitionDestinationFor(asset.graph, guard),
                "nothing to redirect to; the drop is cancelled and the dangling repair removes the guard");
        }

        // ------------------------------------------------------------------ the repair on open

        [Test]
        public void TheRepairRepointsATransitionIntoAGuardAtTheOwner()
        {
            var asset = TreeWithAGuardedOrphan(out var selector, out var owner, out var guard);
            var wire = asset.graph.WireUnchecked(selector, guard, 2);

            Assert.AreEqual(1, BehaviorTreeCanvas.RepointTransitionsIntoGuards(asset.graph));

            Assert.AreSame(owner, wire.destination, "the wire was aimed at the owner, so that is where it now ends");
            Assert.AreSame(selector, wire.source);
            Assert.AreEqual(2, wire.TransitionIndex, "its priority is kept; only the endpoint was wrong");
            Assert.IsFalse(asset.graph.Transitions.Any(BehaviorTreeGraph.EndsOnANodeThatCannotBeAChild),
                "and the runtime has nothing left to skip");
            Assert.DoesNotThrow(() => asset.graph.elements.Remove(wire),
                "the transition collection indexes wires by endpoint; a wire re-pointed in place leaves that " +
                "index keyed on the guard, and the next removal throws KeyNotFound");
        }

        [Test]
        public void TheRepairDropsTheWireWhenTheOwnerIsAlreadyConnected()
        {
            var asset = TreeWithAGuardedOrphan(out var selector, out var owner, out var guard);
            BehaviorTreeAuthoring.Connect(asset, selector, owner);
            var wire = asset.graph.WireUnchecked(selector, guard, 1);

            Assert.AreEqual(1, BehaviorTreeCanvas.RepointTransitionsIntoGuards(asset.graph));

            Assert.IsFalse(asset.graph.Transitions.Contains(wire),
                "a second wire from the same parent to the same child is dead weight the priority order would " +
                "then have to explain");
            Assert.AreEqual(1, asset.graph.ChildrenInPriorityOrder(selector).Count);
        }

        [Test]
        public void TheRepairLeavesAHealthyTreeAlone()
        {
            var asset = TreeWithAGuardedOrphan(out var selector, out var owner, out _);
            BehaviorTreeAuthoring.Connect(asset, selector, owner);

            Assert.AreEqual(0, BehaviorTreeCanvas.RepointTransitionsIntoGuards(asset.graph));
            Assert.AreEqual(2, asset.graph.Transitions.Count);
        }

        [Test]
        public void TheRepairLeavesAnOwnerlessGuardToTheDanglingRepair()
        {
            var asset = TreeWithAGuardedOrphan(out var selector, out var owner, out var guard);
            asset.graph.WireUnchecked(selector, guard, 0);
            asset.graph.elements.Remove(owner);

            Assert.AreEqual(0, BehaviorTreeCanvas.RepointTransitionsIntoGuards(asset.graph));
            Assert.IsTrue(BehaviorTreeCanvas.IsDangling(asset.graph, guard),
                "the guard is what the dangling repair removes, and the wire into it follows on the next pass");
        }
    }
}
