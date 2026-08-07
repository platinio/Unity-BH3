using System;
using System.Linq;
using ArcaneOnyx.BehaviorTree.Debugging;
using ArcaneOnyx.GraphCore;
using NUnit.Framework;
using Unity.VisualScripting;
using UnityEngine;

namespace ArcaneOnyx.BehaviorTree.Tests
{
    /// <summary>
    /// The recorder's job is to make "why did the AI do that" a thing you read rather than a thing you
    /// reconstruct, so these tests are mostly about whether a recording still answers the question — that an
    /// abort names the guard responsible, that a branch that never started is distinguishable from one that
    /// was killed, and that two uses of the same shared branch do not collapse into each other.
    ///
    /// <para>
    /// Nodes are driven without a machine, the way the rest of the edit-mode suite drives them. That is why
    /// the recorder is injected through <see cref="BehaviorTreeNode.SetFlightRecorder"/> instead of being
    /// fetched from the machine: a recorder only reachable through a live machine could not be covered here,
    /// and these are exactly the cases worth covering.
    /// </para>
    /// </summary>
    [TestFixture]
    public class BehaviorTreeFlightRecorderTests
    {
        private BehaviorTreeFlightRecorder recorder;

        [SetUp]
        public void SetUp()
        {
            BehaviorTreeFlightRecorders.Reset();
            BehaviorTreeFlightRecorders.GloballyEnabled = true;

            recorder = new BehaviorTreeFlightRecorder("TestAgent", "TestTree");
        }

        [TearDown]
        public void TearDown()
        {
            BehaviorTreeFlightRecorders.Reset();
            BehaviorTreeFlightRecorders.GloballyEnabled = true;
        }

        #region Fixtures

        private static T AddNode<T>(BehaviorTreeGraph graph, float x = 0.0f) where T : BehaviorTreeNode, new()
        {
            var node = new T { Position = new Rect(x, 100.0f, 150.0f, 100.0f) };
            graph.Nodes.Add(node);

            return node;
        }

        private static void Connect(BehaviorTreeGraph graph, BehaviorTreeNode parent, BehaviorTreeNode child)
        {
            var transition = new BehaviorTreeTransition();
            transition.SetupTransition(parent, child, 0);
            graph.Transitions.Add(transition);
        }

        /// <summary>Hands every node in the graph the recorder, the way the machine does at Awake.</summary>
        private void Bind(BehaviorTreeGraph graph)
        {
            foreach (var node in graph.Nodes)
            {
                node.SetFlightRecorder(recorder);
            }
        }

        /// <summary>Entry -> Sequence -> a child that never finishes, with a guard on the Sequence.</summary>
        private Sequence GuardedSequence(
            BehaviorTreeGraph graph,
            bool guardStartsTrue,
            out BooleanConditionalExecution guard,
            out ScriptedNode child)
        {
            var sequence = AddNode<Sequence>(graph);

            child = new ScriptedNode(ExecutionStatus.Running) { Position = new Rect(0.0f, 300.0f, 150.0f, 100.0f) };
            graph.Nodes.Add(child);

            Connect(graph, graph.EntryNode, sequence);
            Connect(graph, sequence, child);

            guard = AddNode<BooleanConditionalExecution>(graph);
            guard.UpdateOwner(sequence);
            guard.Value.SetDefaultValue(guardStartsTrue);

            graph.OnAwake();
            Bind(graph);

            return sequence;
        }

        private BehaviorTreeEvent[] EventsOfKind(BehaviorTreeEventKind kind)
        {
            return recorder.Events.Where(recorded => recorded.Kind == kind).ToArray();
        }

        #endregion

        #region Ring buffer

        [Test]
        public void TheRingKeepsTheNewestEventsAndSaysHowManyItLost()
        {
            var ring = new BehaviorTreeEventRing(3);

            for (int i = 0; i < 5; i++)
            {
                ring.Add(BehaviorTreeEvent.Create(
                    BehaviorTreeEventKind.NodeEnter, tick: i, sequence: 0, frame: 0, time: 0.0f,
                    scopeId: 0, nodeGuid: Guid.Empty));
            }

            Assert.AreEqual(3, ring.Count, "A ring holds its capacity and no more.");
            Assert.AreEqual(2, ring.Dropped,
                "A clipped recording has to admit it is clipped, or it gets read as the whole story.");

            CollectionAssert.AreEqual(new[] { 2, 3, 4 }, ring.Select(recorded => recorded.Tick).ToArray(),
                "Oldest first, and the oldest survivors are the newest arrivals.");
        }

        [Test]
        public void ClearingTheRingResetsWhatItReportsHavingLost()
        {
            var ring = new BehaviorTreeEventRing(1);

            ring.Add(BehaviorTreeEvent.Create(BehaviorTreeEventKind.NodeEnter, 0, 0, 0, 0.0f, 0, Guid.Empty));
            ring.Add(BehaviorTreeEvent.Create(BehaviorTreeEventKind.NodeEnter, 1, 0, 0, 0.0f, 0, Guid.Empty));

            Assert.AreEqual(1, ring.Dropped);

            ring.Clear();

            Assert.AreEqual(0, ring.Count);
            Assert.AreEqual(0, ring.Dropped);
        }

        #endregion

        #region Guards

        [Test]
        public void AGuardIsRecordedOnlyWhenItsAnswerChanges()
        {
            var graph = new BehaviorTreeGraph();
            var sequence = GuardedSequence(graph, guardStartsTrue: true, out var guard, out _);

            sequence.OnNodeEnter();
            sequence.OnUpdateInternal();
            sequence.OnUpdateInternal();

            Assert.AreEqual(1, EventsOfKind(BehaviorTreeEventKind.GuardEval).Length,
                "A guard evaluates every tick while its owner runs. Recording every evaluation would bury the "
                + "one that mattered under thousands of repeats of the same answer.");

            guard.Value.SetDefaultValue(false);
            sequence.OnUpdateInternal();

            var evaluations = EventsOfKind(BehaviorTreeEventKind.GuardEval);

            Assert.AreEqual(2, evaluations.Length, "The transition is the event.");
            Assert.IsTrue(evaluations[0].Flag);
            Assert.IsFalse(evaluations[1].Flag);
        }

        [Test]
        public void AGuardEvalNamesBothTheGuardAndWhatItProtects()
        {
            var graph = new BehaviorTreeGraph();
            var sequence = GuardedSequence(graph, guardStartsTrue: true, out var guard, out _);

            sequence.OnNodeEnter();

            var evaluation = EventsOfKind(BehaviorTreeEventKind.GuardEval).Single();

            Assert.AreEqual(guard.guid, evaluation.NodeGuid, "The event is about the guard.");
            Assert.AreEqual(sequence.guid, evaluation.RelatedGuid,
                "And a guard is only meaningful next to the branch it gates.");
        }

        [Test]
        public void AnAbortNamesTheGuardThatCausedIt()
        {
            var graph = new BehaviorTreeGraph();
            var sequence = GuardedSequence(graph, guardStartsTrue: true, out var guard, out _);

            sequence.OnNodeEnter();
            sequence.OnUpdateInternal();

            guard.Value.SetDefaultValue(false);

            Assert.AreEqual(ExecutionStatus.Failure, sequence.OnUpdateInternal());

            var abort = EventsOfKind(BehaviorTreeEventKind.NodeAborted).Single();

            Assert.AreEqual(sequence.guid, abort.NodeGuid);
            Assert.AreEqual(guard.guid, abort.RelatedGuid,
                "By the time the parent composite sees a Failure, which guard caused it is gone. This is the "
                + "only place that fact exists, and losing it is what makes selector thrash so hard to debug.");
        }

        [Test]
        public void ABranchThatNeverStartedIsRecordedDifferentlyFromOneThatWasKilled()
        {
            var graph = new BehaviorTreeGraph();
            var sequence = GuardedSequence(graph, guardStartsTrue: false, out var guard, out var child);

            sequence.OnNodeEnter();

            Assert.AreEqual(0, child.EnterCalls, "Precondition: a false guard stops the branch starting at all.");

            var skipped = EventsOfKind(BehaviorTreeEventKind.NodeSkipped).Single();

            Assert.AreEqual(sequence.guid, skipped.NodeGuid);
            Assert.AreEqual(guard.guid, skipped.RelatedGuid);

            Assert.IsEmpty(EventsOfKind(BehaviorTreeEventKind.NodeEnter),
                "Nothing entered, so nothing may be recorded as having entered.");
            Assert.IsEmpty(EventsOfKind(BehaviorTreeEventKind.NodeAborted),
                "'Never ran' and 'ran and was killed' are different answers to why a branch did not happen.");
        }

        #endregion

        #region Enter and exit

        [Test]
        public void AnExitCarriesTheStatusTheNodeEndedOn()
        {
            var graph = new BehaviorTreeGraph();

            var sequence = AddNode<Sequence>(graph);
            var child = new ScriptedNode(ExecutionStatus.Failure) { Position = new Rect(0.0f, 300.0f, 150.0f, 100.0f) };
            graph.Nodes.Add(child);

            Connect(graph, graph.EntryNode, sequence);
            Connect(graph, sequence, child);

            graph.OnAwake();
            Bind(graph);

            sequence.RunToCompletion();

            var exit = EventsOfKind(BehaviorTreeEventKind.NodeExit)
                .Single(recorded => recorded.NodeGuid == sequence.guid);

            Assert.AreEqual(ExecutionStatus.Failure, exit.Status,
                "Without the status an exit says a node stopped but not whether it worked.");
        }

        [Test]
        public void ExitsAreOnlyRecordedForNodesThatActuallyRan()
        {
            var graph = new BehaviorTreeGraph();
            var sequence = GuardedSequence(graph, guardStartsTrue: false, out _, out _);

            sequence.OnNodeEnter();
            sequence.OnNodeExit();

            Assert.IsEmpty(EventsOfKind(BehaviorTreeEventKind.NodeExit),
                "OnNodeExit is called on nodes that never started and does nothing for them. Recording those "
                + "would fill a recording with exits for branches a selector never reached.");
        }

        #endregion

        #region Call sites

        [Test]
        public void TwoUsesOfTheSameBranchAreToldApart()
        {
            // The case this whole addressing scheme exists for. A sub-tree asset is instantiated per call
            // site and guid is serialized, so the clone keeps the original guids: address a node by guid
            // alone and two copies of a shared Attack become one node that appears to be in two states.
            var shared = new ScriptedNode(ExecutionStatus.Running);

            var first = new BehaviorTreeVariableScope(new VariableDeclarations());
            var second = new BehaviorTreeVariableScope(new VariableDeclarations());

            int firstId = recorder.RegisterCallSite(first, null, Guid.NewGuid(), "Attack");
            int secondId = recorder.RegisterCallSite(second, null, Guid.NewGuid(), "Attack");

            Assert.AreNotEqual(firstId, secondId, "Same asset, two call sites, two identities.");

            shared.SetFlightRecorder(recorder);

            shared.SetVariableScope(first);
            shared.OnNodeEnter();

            shared.SetVariableScope(second);
            shared.OnNodeEnter();

            var entries = EventsOfKind(BehaviorTreeEventKind.NodeEnter);

            Assert.AreEqual(2, entries.Length);
            Assert.AreEqual(shared.guid, entries[0].NodeGuid);
            Assert.AreEqual(shared.guid, entries[1].NodeGuid, "The guid genuinely is the same.");
            Assert.AreEqual(firstId, entries[0].ScopeId);
            Assert.AreEqual(secondId, entries[1].ScopeId, "Which is why the call site has to carry the difference.");
        }

        [Test]
        public void RegisteringTheSameScopeTwiceKeepsOneIdentity()
        {
            var scope = new BehaviorTreeVariableScope(new VariableDeclarations());

            int first = recorder.RegisterCallSite(scope, null, Guid.NewGuid(), "Attack");
            int second = recorder.RegisterCallSite(scope, null, Guid.NewGuid(), "Attack");

            Assert.AreEqual(first, second);
            Assert.AreEqual(2, recorder.CallSites.Count, "The root plus the one that was registered.");
        }

        [Test]
        public void ACallSiteRemembersWhichOneRunsIt()
        {
            var outer = new BehaviorTreeVariableScope(new VariableDeclarations());
            var inner = new BehaviorTreeVariableScope(new VariableDeclarations(), outer);

            int outerId = recorder.RegisterCallSite(outer, null, Guid.NewGuid(), "Combat");
            int innerId = recorder.RegisterCallSite(inner, outer, Guid.NewGuid(), "Attack");

            var recorded = recorder.CallSites.Single(callSite => callSite.Id == innerId);

            Assert.AreEqual(outerId, recorded.ParentId,
                "A flat list of call sites cannot say 'Attack, under Combat'. The parent link is what turns "
                + "it back into the tree a reader is looking at.");
        }

        [Test]
        public void ANodeWithNoRegisteredScopeIsAttributedToTheRoot()
        {
            var node = new ScriptedNode(ExecutionStatus.Running);
            node.SetFlightRecorder(recorder);

            node.OnNodeEnter();

            Assert.AreEqual(BehaviorTreeCallSite.RootId,
                EventsOfKind(BehaviorTreeEventKind.NodeEnter).Single().ScopeId,
                "A node the machine never scoped still has to land somewhere readable rather than throw.");
        }

        #endregion

        #region Switches

        [Test]
        public void NothingIsRecordedWhileTheGlobalSwitchIsOff()
        {
            BehaviorTreeFlightRecorders.GloballyEnabled = false;

            var graph = new BehaviorTreeGraph();
            var sequence = GuardedSequence(graph, guardStartsTrue: true, out _, out _);

            sequence.OnNodeEnter();
            sequence.OnUpdateInternal();

            Assert.AreEqual(0, recorder.Events.Count,
                "The kill switch is the answer to two hundred agents and one interesting one.");
        }

        [Test]
        public void TurningARecorderOffKeepsWhatItAlreadyHas()
        {
            var graph = new BehaviorTreeGraph();
            var sequence = GuardedSequence(graph, guardStartsTrue: true, out _, out _);

            sequence.OnNodeEnter();
            int recorded = recorder.Events.Count;

            Assert.Greater(recorded, 0, "Precondition: something was recorded.");

            recorder.Enabled = false;
            sequence.OnUpdateInternal();

            Assert.AreEqual(recorded, recorder.Events.Count,
                "Stopping a recorder after reproducing a bug must freeze the evidence, not discard it.");
        }

        #endregion

        #region Ticks

        [Test]
        public void EventsWithinATickAreOrdered()
        {
            // Ordering inside a tick is the whole causal claim — "the guard flipped, then the node aborted".
            // A tick number alone cannot express it, and it is what will make the rule that services tick
            // before guards checkable when services land.
            var graph = new BehaviorTreeGraph();
            var sequence = GuardedSequence(graph, guardStartsTrue: true, out var guard, out _);

            recorder.BeginTick();
            sequence.OnNodeEnter();
            sequence.OnUpdateInternal();

            recorder.BeginTick();
            guard.Value.SetDefaultValue(false);
            sequence.OnUpdateInternal();

            var second = recorder.Events.Where(recorded => recorded.Tick == 2).ToArray();

            Assert.AreEqual(BehaviorTreeEventKind.GuardEval, second[0].Kind);
            Assert.AreEqual(BehaviorTreeEventKind.NodeAborted, second[1].Kind);
            Assert.Less(second[0].Sequence, second[1].Sequence,
                "The guard flipping is the cause and the abort is the effect, and a recording has to keep "
                + "them in that order to be readable as one.");
        }

        [Test]
        public void EachTickRestartsTheSequenceNumbering()
        {
            var graph = new BehaviorTreeGraph();
            var sequence = GuardedSequence(graph, guardStartsTrue: true, out _, out _);

            recorder.BeginTick();
            sequence.OnNodeEnter();

            recorder.BeginTick();
            sequence.OnNodeExit();

            Assert.AreEqual(0, recorder.Events.First(recorded => recorded.Tick == 2).Sequence);
        }

        #endregion

        #region Variable writes

        [Test]
        public void AWriteFromOutsideTheTreeIsRecordedWithItsWriterNamed()
        {
            recorder.ExternalVariableWrite("TargetingSensor", "hasTarget", false, true);

            var write = EventsOfKind(BehaviorTreeEventKind.VariableWrite).Single();

            Assert.AreEqual("hasTarget", write.Key);
            Assert.AreEqual("False", write.OldValue);
            Assert.AreEqual("True", write.NewValue);
            Assert.AreEqual("TargetingSensor", write.Writer,
                "Facts are produced by always-on sensors rather than by branches, so without a name for an "
                + "out-of-graph writer the commonest 'who changed this?' has no answer at all.");
        }

        [Test]
        public void ALongValueIsTruncatedRatherThanKeptWhole()
        {
            var long_ = new string('x', BehaviorTreeEvent.MaxValueLength * 2);

            recorder.ExternalVariableWrite("Sensor", "blob", null, long_);

            var write = EventsOfKind(BehaviorTreeEventKind.VariableWrite).Single();

            Assert.AreEqual("null", write.OldValue, "A value that did not exist reads as null, not as an error.");
            Assert.LessOrEqual(write.NewValue.Length, BehaviorTreeEvent.MaxValueLength + 1,
                "A ring's memory cost has to stay bounded by its capacity, not by what someone stored.");
        }

        #endregion

        #region Export

        [Test]
        public void ARecordingExportsWithItsCallSitesAndEvents()
        {
            var graph = new BehaviorTreeGraph();
            var sequence = GuardedSequence(graph, guardStartsTrue: true, out var guard, out _);

            recorder.BeginTick();
            sequence.OnNodeEnter();
            guard.Value.SetDefaultValue(false);
            sequence.OnUpdateInternal();

            string json = BehaviorTreeRecordingDump.ToJson(recorder);

            StringAssert.Contains("\"agent\": \"TestAgent\"", json);
            StringAssert.Contains("\"tree\": \"TestTree\"", json);
            StringAssert.Contains("\"callSites\"", json);
            StringAssert.Contains("\"kind\": \"NodeAborted\"", json);

            StringAssert.Contains(guard.guid.ToString(), json,
                "Guids in a recording are the same guids bt_describe_tree emits — that pairing is what makes "
                + "a recording plus a tree dump a bug report someone else can read.");
        }

        [Test]
        public void ExportingNothingIsStillValidOutput()
        {
            Assert.DoesNotThrow(() => BehaviorTreeRecordingDump.ToJson(null));
            StringAssert.Contains("\"count\": 0", BehaviorTreeRecordingDump.ToJson(recorder));
        }

        #endregion
    }
}
