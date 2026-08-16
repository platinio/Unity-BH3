using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace ArcaneOnyx.BehaviorTree.Tests.PlayMode
{
    /// <summary>
    /// Trees built in code and handed straight to a machine, with no asset on disk.
    ///
    /// <para>
    /// <b>"Embedded" here means the tree itself</b> — a <c>BehaviorTreeGraphAsset</c> created with
    /// <c>ScriptableObject.CreateInstance</c>, wired through <c>graph.Nodes</c> and
    /// <c>BehaviorTreeTransition.SetupTransition</c>, and never saved. It is unrelated to the embedded
    /// <em>script graph</em> sub-assets spec 10 talks about, which are a different thing that happens to share
    /// the word.
    /// </para>
    ///
    /// <para>
    /// This path is public API and entirely untested until now, which is how a demo built this way shipped
    /// with every node stacked at the origin without anything complaining. It matters because the parts a
    /// designer relies on — sibling priority, guards, ports fed by literals — are established by the editor's
    /// authoring helpers, and code that skips those helpers has to reproduce their guarantees itself.
    /// </para>
    ///
    /// <para>
    /// <b>The priority rule is the subject of most of these.</b> It has two halves and the tooling documents
    /// only one: <c>BehaviorTreeGraph.SortIntoPriorityOrder</c> uses transition indices when they form exactly
    /// <c>0..n-1</c> with each used once, and falls back to canvas X otherwise. Contiguity is the test because
    /// a gap means something was removed without renumbering and a duplicate means two children claim one
    /// priority — in both cases the recorded order is not trustworthy. A reader who knows only the "canvas X"
    /// half will mis-predict every tree built in code, and a reader who knows only the "index" half will
    /// mis-predict every tree whose transitions were edited.
    /// </para>
    /// </summary>
    public class EmbeddedTreeTests : PlayModeAgentFixture
    {
        /// <summary>Long enough that nothing ever finishes on its own, so only priority decides what runs.</summary>
        private const float Forever = 999.0f;

        /// <summary>
        /// Entry → Repeater → Selector → two leaves, connected in the given order and positioned as asked.
        /// Positions and connection order are separated so a test can put them deliberately in conflict.
        /// </summary>
        private BehaviorTreeGraphAsset TwoBranchTree(
            float firstX, float secondX, out System.Guid first, out System.Guid second, bool connectInOrder = true)
        {
            var asset = NewTree();
            var graph = asset.graph;

            var repeater = Add<Repeater>(graph, 0.0f, 150.0f);
            var selector = Add<Selector>(graph, 0.0f, 350.0f);

            var firstNode = Add<WaitTime>(graph, firstX, 650.0f);
            var secondNode = Add<WaitTime>(graph, secondX, 650.0f);

            Connect(graph, graph.EntryNode, repeater);
            Connect(graph, repeater, selector);

            if (connectInOrder)
            {
                Connect(graph, selector, firstNode);
                Connect(graph, selector, secondNode);
            }
            else
            {
                Connect(graph, selector, secondNode);
                Connect(graph, selector, firstNode);
            }

            FeedFloat(graph, firstNode, firstNode.Time, Forever);
            FeedFloat(graph, secondNode, secondNode.Time, Forever);

            first = firstNode.guid;
            second = secondNode.guid;

            return asset;
        }

        private static IEnumerator Settle()
        {
            for (var frame = 0; frame < 3; frame++)
            {
                yield return null;
            }
        }

        [UnityTest]
        public IEnumerator ATreeBuiltInCodeRunsUnderAMachine()
        {
            var asset = NewTree();
            var graph = asset.graph;

            var wait = Add<WaitTime>(graph, 0.0f, 200.0f);
            Connect(graph, graph.EntryNode, wait);
            FeedFloat(graph, wait, wait.Time, Forever);

            var machine = Spawn(asset);

            yield return Settle();

            Assert.IsTrue(Entered(machine.FlightRecorder, wait.guid),
                "A tree that was never an asset must still run: this is a public path, and the whole of it is "
                + "CreateInstance plus Nodes and Transitions.");
        }

        [UnityTest]
        public IEnumerator ContiguousTransitionIndicesDecidePriority_NotCanvasX()
        {
            // Connected first but positioned to the RIGHT. If canvas X decided, the other branch would win.
            var asset = TwoBranchTree(firstX: 900.0f, secondX: -900.0f, out var connectedFirst, out var leftmost);
            var machine = Spawn(asset);

            yield return Settle();

            var recorder = machine.FlightRecorder;

            Assert.IsTrue(Entered(recorder, connectedFirst),
                "Connect assigns CountTransitionsFromNode, so these indices are 0 and 1 -- a genuine order, "
                + "which outranks position.");
            Assert.IsFalse(Entered(recorder, leftmost),
                "The leftmost branch must NOT run: canvas X is the fallback, not the rule.");
        }

        [UnityTest]
        public IEnumerator SiblingsSharingAPosition_AreStillOrderedByTheirTransitionIndices()
        {
            // Every node at the same X -- visually wrong, and the shape a tree built in code lands in when
            // nobody passes positions. Priority is still well defined, because the indices are contiguous.
            var asset = TwoBranchTree(firstX: 0.0f, secondX: 0.0f, out var connectedFirst, out var connectedSecond);
            var machine = Spawn(asset);

            yield return Settle();

            var recorder = machine.FlightRecorder;

            Assert.IsTrue(Entered(recorder, connectedFirst),
                "Stacked positions are a readability problem, not a correctness one: the transition indices "
                + "still record a genuine order and that is what the runtime reads.");
            Assert.IsFalse(Entered(recorder, connectedSecond));
        }

        [UnityTest]
        public IEnumerator WhenIndicesDoNotRecordAGenuineOrder_CanvasXDecides()
        {
            var asset = NewTree();
            var graph = asset.graph;

            var repeater = Add<Repeater>(graph, 0.0f, 150.0f);
            var selector = Add<Selector>(graph, 0.0f, 350.0f);

            var right = Add<WaitTime>(graph, 900.0f, 650.0f);
            var left = Add<WaitTime>(graph, -900.0f, 650.0f);

            Connect(graph, graph.EntryNode, repeater);
            Connect(graph, repeater, selector);

            // Both claim priority 0. Duplicates mean the recorded order cannot be trusted, so position is
            // consulted instead -- and the right-hand branch was added first, so insertion order would give
            // the opposite answer to the one asserted here.
            AddTransitionWithIndex(graph, selector, right, 0);
            AddTransitionWithIndex(graph, selector, left, 0);

            FeedFloat(graph, right, right.Time, Forever);
            FeedFloat(graph, left, left.Time, Forever);

            var machine = Spawn(asset);

            yield return Settle();

            var recorder = machine.FlightRecorder;

            Assert.IsTrue(Entered(recorder, left.guid),
                "With duplicate indices the order is not trustworthy, so the leftmost branch wins on position.");
            Assert.IsFalse(Entered(recorder, right.guid));
        }

        [UnityTest]
        public IEnumerator AGuardBuiltInCodeGatesItsOwner()
        {
            var asset = TwoBranchTree(firstX: -900.0f, secondX: 900.0f, out var guarded, out var fallback);
            var graph = asset.graph;

            var guardedNode = NodeWithGuid(graph, guarded);

            var read = ReadAgentVariable(graph, "hasTarget", -1400.0f, 100.0f);
            var guard = Add<BooleanReactiveGuard>(graph, -900.0f, 450.0f);
            guard.UpdateOwner(guardedNode);
            read.Value.ValidlyConnectTo(guard.Value);
            guard.AddTrigger(GuardTrigger.KeyChanged("hasTarget"));

            var machine = Spawn(asset, (agent, variables) =>
            {
                variables.declarations.Set("hasTarget", false);
                agent.AddComponent<AgentVariableWriter>();
            });

            yield return Settle();

            var recorder = machine.FlightRecorder;

            Assert.IsFalse(Entered(recorder, guarded),
                "A guard wired in code must gate its owner, or the branch runs unguarded and every guard "
                + "authored this way is decoration.");
            Assert.IsTrue(Entered(recorder, fallback));

            PublishFact(machine, "hasTarget", true);

            yield return Settle();

            Assert.IsTrue(Entered(recorder, guarded),
                "and it must release its owner once the fact it watches moves.");
        }

        [UnityTest]
        public IEnumerator PortsFedByLiteralsSurviveTheMachinesInstantiate()
        {
            var asset = NewTree();
            var graph = asset.graph;

            var wait = Add<WaitTime>(graph, 0.0f, 200.0f);
            Connect(graph, graph.EntryNode, wait);
            FeedFloat(graph, wait, wait.Time, Forever);

            var machine = Spawn(asset);

            yield return Settle();

            // An unset Time throws when the node is entered, so entering it at all is the assertion: the
            // literal's connection was rebuilt on the clone rather than dropped. SetDefaultValue would not
            // survive here, which is why the fixture connects a literal instead.
            Assert.IsTrue(Entered(machine.FlightRecorder, wait.guid));
            Assert.AreEqual(ArcaneOnyx.GraphCore.ExecutionStatus.Running,
                RunningNode<WaitTime>(machine).LastExecutionStatus,
                "The node must be running rather than faulted, which is what an unresolved port would leave.");
        }

        // ------------------------------------------------------------------ helpers

        private static BehaviorTreeNode NodeWithGuid(BehaviorTreeGraph graph, System.Guid guid)
        {
            foreach (var node in graph.Nodes)
            {
                if (node.guid == guid) return node;
            }

            Assert.Fail($"No node with guid {guid}.");
            return null;
        }

        /// <summary>
        /// Connects with a chosen transition index rather than the next free one, so a test can build the
        /// untrustworthy-order case the fixture's <c>Connect</c> deliberately cannot produce.
        /// </summary>
        private static void AddTransitionWithIndex(
            BehaviorTreeGraph graph, BehaviorTreeNode parent, BehaviorTreeNode child, int index)
        {
            var transition = new BehaviorTreeTransition();
            transition.SetupTransition(parent, child, index);
            graph.Transitions.Add(transition);
        }
    }
}
