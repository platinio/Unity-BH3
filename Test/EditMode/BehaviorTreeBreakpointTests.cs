using System;
using System.Collections.Generic;
using System.IO;
using ArcaneOnyx.BehaviorTree.Debugging;
using ArcaneOnyx.GraphCore;
using NUnit.Framework;
using Unity.VisualScripting;
using UnityEngine;

namespace ArcaneOnyx.BehaviorTree.Tests
{
    /// <summary>
    /// Breakpoints are matched inside the flight recorder, so almost everything here is driven through a real
    /// <see cref="BehaviorTreeFlightRecorder"/> with real nodes rather than through hand-built events. That is
    /// deliberate: the component's whole risk is drifting apart from Component 1's event schema — which field
    /// holds the guard on a <c>GuardEval</c>, which holds the writer on a <c>VariableWrite</c> — and a test
    /// that constructs its own events would keep passing after the schema moved underneath it.
    /// </summary>
    [TestFixture]
    public class BehaviorTreeBreakpointTests
    {
        private BehaviorTreeFlightRecorder recorder;
        private readonly List<BehaviorTreeBreakpointHit> hits = new();
        private string breakpointFile;

        [SetUp]
        public void SetUp()
        {
            BehaviorTreeFlightRecorders.Reset();
            BehaviorTreeFlightRecorders.GloballyEnabled = true;

            BehaviorTreeBreakpoints.Clear();
            BehaviorTreeBreakpoints.GloballyEnabled = true;

            // Never the developer's real file. A suite that wipes the breakpoints someone had armed is a suite
            // that damages the thing it is checking.
            breakpointFile = Path.Combine(Path.GetTempPath(), $"bh3-breakpoints-{Guid.NewGuid():N}.json");
            BehaviorTreeBreakpointStore.OverridePath = breakpointFile;

            hits.Clear();
            BehaviorTreeBreakpoints.Hit += Record;

            recorder = new BehaviorTreeFlightRecorder("TestAgent", "TestTree");
        }

        [TearDown]
        public void TearDown()
        {
            BehaviorTreeBreakpoints.Hit -= Record;
            BehaviorTreeBreakpoints.Clear();
            BehaviorTreeBreakpoints.GloballyEnabled = true;

            if (File.Exists(breakpointFile)) File.Delete(breakpointFile);

            // Back to the real file, and back to whatever the developer had armed before the run.
            BehaviorTreeBreakpointStore.OverridePath = null;
            BehaviorTreeBreakpointStore.Load();

            BehaviorTreeFlightRecorders.Reset();
            BehaviorTreeFlightRecorders.GloballyEnabled = true;
        }

        private void Record(BehaviorTreeBreakpointHit hit) => hits.Add(hit);

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

        private ScriptedNode BoundNode(ExecutionStatus result = ExecutionStatus.Success)
        {
            var node = new ScriptedNode(result);
            node.SetFlightRecorder(recorder);

            return node;
        }

        #endregion

        #region Node breakpoints

        [Test]
        public void ANodeBreakpointArmedForEnterFiresWhenTheNodeStarts()
        {
            var node = BoundNode();
            BehaviorTreeBreakpoints.SetNode(node.guid, BehaviorTreeNodeBreakEvents.Enter);

            node.OnNodeEnter();

            Assert.AreEqual(1, hits.Count);
            Assert.AreEqual(node.guid, hits[0].SubjectGuid);
            Assert.AreEqual(BehaviorTreeEventKind.NodeEnter, hits[0].Cause.Kind);
        }

        [Test]
        public void ANodeBreakpointArmedForEnterIgnoresTheExit()
        {
            var node = BoundNode();
            BehaviorTreeBreakpoints.SetNode(node.guid, BehaviorTreeNodeBreakEvents.Enter);

            node.OnNodeEnter();
            hits.Clear();

            node.OnUpdateInternal();
            node.OnNodeExit();

            Assert.IsEmpty(hits, "Exit was not armed, so nothing should have fired.");
        }

        [Test]
        public void AMaskFiresOnEveryMomentItNames()
        {
            var node = BoundNode();

            BehaviorTreeBreakpoints.SetNode(
                node.guid, BehaviorTreeNodeBreakEvents.Enter | BehaviorTreeNodeBreakEvents.Exit);

            node.OnNodeEnter();
            node.OnUpdateInternal();
            node.OnNodeExit();

            Assert.AreEqual(2, hits.Count);
            Assert.AreEqual(BehaviorTreeEventKind.NodeEnter, hits[0].Cause.Kind);
            Assert.AreEqual(BehaviorTreeEventKind.NodeExit, hits[1].Cause.Kind);
        }

        [Test]
        public void ClearingTheLastMomentRemovesTheBreakpointRatherThanLeavingADeadOne()
        {
            var node = BoundNode();

            BehaviorTreeBreakpoints.SetNode(node.guid, BehaviorTreeNodeBreakEvents.Enter);
            Assert.IsNotNull(BehaviorTreeBreakpoints.ForNode(node.guid));

            BehaviorTreeBreakpoints.SetNode(node.guid, BehaviorTreeNodeBreakEvents.None);

            Assert.IsNull(BehaviorTreeBreakpoints.ForNode(node.guid));
            Assert.IsEmpty(BehaviorTreeBreakpoints.All, "An empty mask is a removal, not a breakpoint that cannot fire.");
        }

        [Test]
        public void ADisabledBreakpointDoesNotFireButStaysArmed()
        {
            var node = BoundNode();
            var breakpoint = BehaviorTreeBreakpoints.SetNode(node.guid, BehaviorTreeNodeBreakEvents.Enter);

            BehaviorTreeBreakpoints.SetEnabled(breakpoint, false);
            node.OnNodeEnter();

            Assert.IsEmpty(hits);
            Assert.AreEqual(1, BehaviorTreeBreakpoints.All.Count, "Disabling is not deleting.");
        }

        [Test]
        public void TheGlobalSwitchStopsEverythingWithoutDisarmingAnything()
        {
            var node = BoundNode();
            BehaviorTreeBreakpoints.SetNode(node.guid, BehaviorTreeNodeBreakEvents.Enter);

            BehaviorTreeBreakpoints.GloballyEnabled = false;
            node.OnNodeEnter();

            Assert.IsEmpty(hits);
            Assert.AreEqual(1, BehaviorTreeBreakpoints.All.Count);
        }

        [Test]
        public void ARealGuardedAbortFiresAnAbortBreakpoint()
        {
            // End to end through the machinery that actually produces an abort, rather than by asserting on a
            // NodeAborted event someone constructed. Finding 8 — the abort is followed by its own exit in the
            // same tick — means a component reading these events can very easily attribute the wrong one.
            var graph = new BehaviorTreeGraph();
            var sequence = AddNode<Sequence>(graph);
            var child = new ScriptedNode(ExecutionStatus.Running) { Position = new Rect(0.0f, 300.0f, 150.0f, 100.0f) };
            graph.Nodes.Add(child);

            Connect(graph, graph.EntryNode, sequence);
            Connect(graph, sequence, child);

            var guard = AddNode<BooleanConditionalExecution>(graph);
            guard.UpdateOwner(sequence);
            guard.Value.SetDefaultValue(true);

            graph.OnAwake();

            foreach (var node in graph.Nodes)
            {
                node.SetFlightRecorder(recorder);
            }

            BehaviorTreeBreakpoints.SetNode(sequence.guid, BehaviorTreeNodeBreakEvents.Aborted);

            sequence.OnNodeEnter();
            sequence.OnUpdateInternal();

            Assert.IsEmpty(hits, "The guard still holds, so nothing has been aborted yet.");

            guard.Value.SetDefaultValue(false);
            sequence.OnUpdateInternal();

            Assert.AreEqual(1, hits.Count);
            Assert.AreEqual(BehaviorTreeEventKind.NodeAborted, hits[0].Cause.Kind);
            Assert.AreEqual(guard.guid, hits[0].Cause.RelatedGuid, "The abort should still name the guard that caused it.");
        }

        #endregion

        #region Guard breakpoints

        [Test]
        public void AGuardBreakpointFiresOnEitherTransition()
        {
            var owner = BoundNode();
            var guard = new BooleanConditionalExecution();

            BehaviorTreeBreakpoints.SetGuard(guard.guid, BehaviorTreeGuardBreakOn.EitherWay);

            recorder.GuardEval(owner, guard, true);
            recorder.GuardEval(owner, guard, false);

            Assert.AreEqual(2, hits.Count);
            Assert.IsTrue(hits[0].Cause.Flag);
            Assert.IsFalse(hits[1].Cause.Flag);
        }

        [Test]
        public void BecameFalseIgnoresTheTransitionToTrue()
        {
            var owner = BoundNode();
            var guard = new BooleanConditionalExecution();

            BehaviorTreeBreakpoints.SetGuard(guard.guid, BehaviorTreeGuardBreakOn.BecameFalse);

            recorder.GuardEval(owner, guard, true);
            Assert.IsEmpty(hits, "Only the fall to false was armed.");

            recorder.GuardEval(owner, guard, false);
            Assert.AreEqual(1, hits.Count);
        }

        [Test]
        public void AGuardThatKeepsSayingTheSameThingFiresOnce()
        {
            // The property that makes a guard breakpoint usable at all. Guards evaluate every tick, so a
            // breakpoint on "is false" without the recorder's transition filtering would pause the editor on
            // the frame it was armed and on every frame after it.
            var owner = BoundNode();
            var guard = new BooleanConditionalExecution();

            BehaviorTreeBreakpoints.SetGuard(guard.guid, BehaviorTreeGuardBreakOn.EitherWay);

            for (int tick = 0; tick < 20; tick++)
            {
                recorder.BeginTick();
                recorder.GuardEval(owner, guard, false);
            }

            Assert.AreEqual(1, hits.Count, "Twenty ticks of the same answer is one change of mind.");
        }

        [Test]
        public void AGuardBreakpointReadsTheGuardAndNotTheNodeItProtects()
        {
            // GuardEval is the one event kind whose subject is not a behaviour node: NodeGuid is the guard and
            // RelatedGuid is its owner. Reading them the usual way round would arm every guard breakpoint
            // against the wrong half of the tree, and would still look like it worked on a tree where the
            // guard happened to be the thing selected.
            var owner = BoundNode();
            var guard = new BooleanConditionalExecution();

            BehaviorTreeBreakpoints.SetGuard(owner.guid, BehaviorTreeGuardBreakOn.EitherWay);

            recorder.GuardEval(owner, guard, false);

            Assert.IsEmpty(hits, "Arming the owner's guid must not catch its guard's transitions.");
        }

        #endregion

        #region Variable breakpoints

        [Test]
        public void AVariableBreakpointFiresOnAnyWrite()
        {
            var writer = BoundNode();
            BehaviorTreeBreakpoints.SetVariable("hasTarget");

            recorder.VariableWrite(writer, "hasTarget", VariableKind.Object, false, true);
            recorder.VariableWrite(writer, "hasTarget", VariableKind.Object, true, false);

            Assert.AreEqual(2, hits.Count);
        }

        [Test]
        public void AValueMatchOnlyFiresOnThatValue()
        {
            var writer = BoundNode();
            BehaviorTreeBreakpoints.SetVariable("ammo", "0");

            recorder.VariableWrite(writer, "ammo", VariableKind.Object, 3, 2);
            recorder.VariableWrite(writer, "ammo", VariableKind.Object, 2, 1);

            Assert.IsEmpty(hits, "Neither write landed on the value asked for.");

            recorder.VariableWrite(writer, "ammo", VariableKind.Object, 1, 0);

            Assert.AreEqual(1, hits.Count);
            Assert.AreEqual("0", hits[0].Cause.NewValue);
        }

        [Test]
        public void AVariableHitPointsAtTheWritingNodeRatherThanAnEmptyGuid()
        {
            // A VariableWrite leaves NodeGuid empty and puts the writer in RelatedGuid, so a hit that reported
            // its subject naively would select nothing on exactly the breakpoint whose point is "who wrote
            // this".
            var writer = BoundNode();
            BehaviorTreeBreakpoints.SetVariable("hasTarget");

            recorder.VariableWrite(writer, "hasTarget", VariableKind.Object, false, true);

            Assert.AreEqual(1, hits.Count);
            Assert.AreEqual(writer.guid, hits[0].SubjectGuid);
            Assert.AreNotEqual(Guid.Empty, hits[0].SubjectGuid);
        }

        [Test]
        public void AWriteFromOutsideTheTreeStillFires()
        {
            // Facts come from sensors rather than from branches (Finding 5), so the commonest variable worth
            // breaking on is written by something with no node guid at all.
            BehaviorTreeBreakpoints.SetVariable("lastKnownTargetPos");

            recorder.ExternalVariableWrite("VisionSensor", "lastKnownTargetPos", null, Vector3.one);

            Assert.AreEqual(1, hits.Count);
            Assert.AreEqual("VisionSensor", hits[0].Cause.Writer);
        }

        #endregion

        #region Addressing

        [Test]
        public void ABreakpointFiresAtEveryCallSiteOfASharedBranch()
        {
            // The deliberate reading of Finding 1 for this component. Everything else in the debugger is
            // addressed as (CallSiteId, NodeGuid) because a guid is not unique within an agent; a breakpoint
            // is the one thing that should not be, because the spec puts it on the asset-side config and
            // because right-clicking a node in Attack is a statement about that node rather than about the
            // copy of Attack that Combat happens to be running.
            var shared = BoundNode(ExecutionStatus.Running);

            var first = new BehaviorTreeVariableScope(new VariableDeclarations());
            var second = new BehaviorTreeVariableScope(new VariableDeclarations());

            int firstId = recorder.RegisterCallSite(first, null, Guid.NewGuid(), "Attack");
            int secondId = recorder.RegisterCallSite(second, null, Guid.NewGuid(), "Attack");

            BehaviorTreeBreakpoints.SetNode(shared.guid, BehaviorTreeNodeBreakEvents.Enter);

            shared.SetVariableScope(first);
            shared.OnNodeEnter();

            shared.SetVariableScope(second);
            shared.OnNodeEnter();

            Assert.AreEqual(2, hits.Count, "One breakpoint, both running copies of the branch.");
            Assert.AreEqual(firstId, hits[0].CallSiteId);
            Assert.AreEqual(secondId, hits[1].CallSiteId, "The hit says which copy, even though the breakpoint does not.");
        }

        #endregion

        #region Coupling to the recorder

        [Test]
        public void NothingFiresWhileTheAgentIsNotRecording()
        {
            // Documented rather than incidental: breakpoints are matched inside the recorder, so turning
            // recording off turns them off. The panel says so on screen, and this pins the behaviour so the
            // sentence in the doc cannot quietly stop being true.
            var node = BoundNode();
            BehaviorTreeBreakpoints.SetNode(node.guid, BehaviorTreeNodeBreakEvents.Enter);

            recorder.Enabled = false;
            node.OnNodeEnter();

            Assert.IsEmpty(hits);

            recorder.Enabled = true;
            node.OnNodeEnter();

            Assert.AreEqual(1, hits.Count);
        }

        [Test]
        public void ASubTreeBoundaryDoesNotFireANodeBreakpointTwice()
        {
            // A RunBehaviorTreeGraphNode entering emits both NodeEnter and TreePushed. Matching the second one
            // as well would pause the editor twice for one thing happening.
            // Deliberately not SetFlightRecorder: RunBehaviorTreeGraphNode overrides it to reach into
            // BehaviorTreeGraphAssetInstance, which Instantiates on first read and throws on a node with no
            // asset assigned. The recorder does not need the node to hold a reference to it — the events go
            // the other way — and NodeEnter reads the plain serialized asset rather than the instance.
            var runNode = new RunBehaviorTreeGraphNode();

            BehaviorTreeBreakpoints.SetNode(runNode.guid, BehaviorTreeNodeBreakEvents.All);

            recorder.NodeEnter(runNode);

            Assert.AreEqual(1, hits.Count, "TreePushed is the recorder's bookkeeping, not a second entry.");
            Assert.AreEqual(BehaviorTreeEventKind.NodeEnter, hits[0].Cause.Kind);
        }

        #endregion

        #region Hit counts

        [Test]
        public void HitCountsRiseAndCanBeReset()
        {
            var node = BoundNode();
            var breakpoint = BehaviorTreeBreakpoints.SetNode(node.guid, BehaviorTreeNodeBreakEvents.Enter);

            node.OnNodeEnter();
            node.OnNodeEnter();

            Assert.AreEqual(2, breakpoint.HitCount);

            BehaviorTreeBreakpoints.ResetHitCounts();

            Assert.AreEqual(0, breakpoint.HitCount, "A count from a previous run describes a run that no longer exists.");
        }

        #endregion

        #region Persistence

        [Test]
        public void ARoundTripThroughTheFileKeepsEveryKind()
        {
            var nodeGuid = Guid.NewGuid();
            var guardGuid = Guid.NewGuid();

            BehaviorTreeBreakpoints.SetNode(nodeGuid, BehaviorTreeNodeBreakEvents.Enter | BehaviorTreeNodeBreakEvents.Aborted);
            BehaviorTreeBreakpoints.SetGuard(guardGuid, BehaviorTreeGuardBreakOn.BecameFalse);
            BehaviorTreeBreakpoints.SetVariable("ammo", "0");

            BehaviorTreeBreakpointStore.Save();
            BehaviorTreeBreakpointStore.Load();

            Assert.AreEqual(3, BehaviorTreeBreakpoints.All.Count);

            var node = BehaviorTreeBreakpoints.ForNode(nodeGuid);
            Assert.IsNotNull(node, "A breakpoint keyed by guid has to survive a reload — that is the point of the file.");
            Assert.AreEqual(
                BehaviorTreeNodeBreakEvents.Enter | BehaviorTreeNodeBreakEvents.Aborted, node.Events);

            Assert.AreEqual(BehaviorTreeGuardBreakOn.BecameFalse, BehaviorTreeBreakpoints.ForGuard(guardGuid).GuardBreakOn);
            Assert.AreEqual("0", BehaviorTreeBreakpoints.ForVariable("ammo").ExpectedValue);
        }

        [Test]
        public void DisabledSurvivesTheRoundTrip()
        {
            var guid = Guid.NewGuid();
            var breakpoint = BehaviorTreeBreakpoints.SetNode(guid, BehaviorTreeNodeBreakEvents.Enter);

            BehaviorTreeBreakpoints.SetEnabled(breakpoint, false);
            BehaviorTreeBreakpointStore.Save();
            BehaviorTreeBreakpointStore.Load();

            Assert.IsFalse(BehaviorTreeBreakpoints.ForNode(guid).Enabled);
        }

        [Test]
        public void HitCountsAreNotPersisted()
        {
            var node = BoundNode();
            BehaviorTreeBreakpoints.SetNode(node.guid, BehaviorTreeNodeBreakEvents.Enter);

            node.OnNodeEnter();
            Assert.AreEqual(1, BehaviorTreeBreakpoints.ForNode(node.guid).HitCount);

            BehaviorTreeBreakpointStore.Save();
            BehaviorTreeBreakpointStore.Load();

            Assert.AreEqual(0, BehaviorTreeBreakpoints.ForNode(node.guid).HitCount);
        }

        [Test]
        public void AnUnreadableEntryIsSkippedRatherThanFailingTheWholeLoad()
        {
            // The file is per-user and hand-editable by design, so a stale enum name from an older build is a
            // thing that will happen. Dropping one row beats refusing to load the other two.
            var good = Guid.NewGuid();

            File.WriteAllText(breakpointFile, @"{
    ""version"": 1,
    ""breakpoints"": [
        { ""kind"": ""Node"", ""target"": """ + good + @""", ""events"": ""Enter"", ""enabled"": true },
        { ""kind"": ""Nonsense"", ""target"": ""not-a-guid"", ""events"": ""Sideways"", ""enabled"": true },
        { ""kind"": ""Variable"", ""variableKey"": ""ammo"", ""enabled"": true }
    ]
}");

            BehaviorTreeBreakpointStore.Load();

            Assert.AreEqual(2, BehaviorTreeBreakpoints.All.Count);
            Assert.IsNotNull(BehaviorTreeBreakpoints.ForNode(good));
            Assert.IsNotNull(BehaviorTreeBreakpoints.ForVariable("ammo"));
        }

        [Test]
        public void LoadingWithNoFileLeavesNothingArmedAndDoesNotThrow()
        {
            BehaviorTreeBreakpoints.SetVariable("ammo");

            if (File.Exists(breakpointFile)) File.Delete(breakpointFile);

            Assert.DoesNotThrow(BehaviorTreeBreakpointStore.Load);
            Assert.IsEmpty(BehaviorTreeBreakpoints.All);
        }

        #endregion

        #region Arming rules

        [Test]
        public void ArmingTheSameTargetTwiceEditsRatherThanDuplicates()
        {
            var guid = Guid.NewGuid();

            BehaviorTreeBreakpoints.SetNode(guid, BehaviorTreeNodeBreakEvents.Enter);
            BehaviorTreeBreakpoints.SetNode(guid, BehaviorTreeNodeBreakEvents.Exit);

            Assert.AreEqual(1, BehaviorTreeBreakpoints.All.Count);
            Assert.AreEqual(BehaviorTreeNodeBreakEvents.Exit, BehaviorTreeBreakpoints.ForNode(guid).Events);
        }

        [Test]
        public void ANodeAndAGuardCanShareAGuidWithoutSharingABreakpoint()
        {
            // A ConditionalExecution is itself a node, so one guid can legitimately carry both kinds. Keying
            // them in one map would make arming a guard silently overwrite a node breakpoint.
            var guid = Guid.NewGuid();

            BehaviorTreeBreakpoints.SetNode(guid, BehaviorTreeNodeBreakEvents.Enter);
            BehaviorTreeBreakpoints.SetGuard(guid, BehaviorTreeGuardBreakOn.BecameFalse);

            Assert.AreEqual(2, BehaviorTreeBreakpoints.All.Count);
            Assert.IsNotNull(BehaviorTreeBreakpoints.ForNode(guid));
            Assert.IsNotNull(BehaviorTreeBreakpoints.ForGuard(guid));
        }

        #endregion
    }
}
