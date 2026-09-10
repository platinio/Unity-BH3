using System;
using System.Collections.Generic;
using System.Linq;
using ArcaneOnyx.BehaviorTree.Debugging;
using ArcaneOnyx.GraphCore;
using NUnit.Framework;
using Unity.VisualScripting;
using UnityEngine;

namespace ArcaneOnyx.BehaviorTree.Tests
{
    /// <summary>
    /// The why-inspector's promise is that a designer reads the answer instead of inferring it, so these tests
    /// are about the sentences: that an abort and a skip do not get the same words, that a claimed cause is
    /// backed by a recorded write rather than by whatever happened to be nearby, and that an explanation says
    /// so when it can only offer a correlation.
    ///
    /// <para>
    /// Most fixtures build a recording by hand rather than by running a tree. The engine's contract is with
    /// the buffer, not with the runtime, and a hand-built buffer can pose the awkward cases — a clipped
    /// recording, a guard that flipped forty ticks before the abort, one branch asset running at two call
    /// sites — that a live tree would take a scene to reproduce. The cases that are really about the
    /// recorder's own output drive real nodes instead, and are marked as such.
    /// </para>
    /// </summary>
    [TestFixture]
    public class BehaviorTreeWhyInspectorTests
    {
        private static readonly Guid Branch = new("11111111-1111-1111-1111-111111111111");
        private static readonly Guid Guard = new("22222222-2222-2222-2222-222222222222");
        private static readonly Guid Sibling = new("33333333-3333-3333-3333-333333333333");
        private static readonly Guid Parent = new("44444444-4444-4444-4444-444444444444");
        private static readonly Guid Child = new("55555555-5555-5555-5555-555555555555");
        private static readonly Guid Sensor = new("66666666-6666-6666-6666-666666666666");

        #region Fixtures

        /// <summary>A topology answering from a table, so a test states the structure it means to test.</summary>
        private sealed class StubTopology : IBehaviorTreeTopology
        {
            private readonly Dictionary<Guid, BehaviorTreeNodeInfo> nodes = new();
            private readonly Dictionary<Guid, IReadOnlyList<string>> reads = new();

            public StubTopology Node(Guid guid, string name, string type = "ScriptedNode", Guid parent = default, params Guid[] children)
            {
                nodes[guid] = new BehaviorTreeNodeInfo(guid, name, type, parent, children);
                return this;
            }

            public StubTopology GuardReads(Guid guard, params string[] keys)
            {
                reads[guard] = keys;
                return this;
            }

            public bool TryGetNode(Guid guid, out BehaviorTreeNodeInfo node) => nodes.TryGetValue(guid, out node);

            public bool TryGetGuardReads(Guid guard, out IReadOnlyList<string> keys) =>
                reads.TryGetValue(guard, out keys) && keys.Count > 0;
        }

        private sealed class RecordingBuilder
        {
            private readonly List<BehaviorTreeEvent> events = new();
            private readonly List<BehaviorTreeCallSite> callSites = new()
            {
                new BehaviorTreeCallSite(BehaviorTreeCallSite.RootId, BehaviorTreeCallSite.RootId, Guid.Empty, "Zombie"),
            };

            private int tick;
            private int sequence;

            public RecordingBuilder At(int value)
            {
                tick = value;
                sequence = 0;
                return this;
            }

            public RecordingBuilder CallSite(int id, int parent, string asset)
            {
                callSites.Add(new BehaviorTreeCallSite(id, parent, Guid.NewGuid(), asset));
                return this;
            }

            public RecordingBuilder Enter(Guid node, int scope = 0) => Add(BehaviorTreeEventKind.NodeEnter, scope, node);

            public RecordingBuilder Exit(Guid node, ExecutionStatus status, int scope = 0) =>
                Add(BehaviorTreeEventKind.NodeExit, scope, node, status: status);

            public RecordingBuilder Aborted(Guid node, Guid guard, int scope = 0) =>
                Add(BehaviorTreeEventKind.NodeAborted, scope, node, guard);

            public RecordingBuilder Skipped(Guid node, Guid guard, int scope = 0) =>
                Add(BehaviorTreeEventKind.NodeSkipped, scope, node, guard);

            public RecordingBuilder TakenOver(Guid victim, Guid guard, string preemptorName, Guid preemptor, int scope = 0) =>
                Add(BehaviorTreeEventKind.NodeTakenOver, scope, victim, guard,
                    key: preemptorName, newValue: preemptor.ToString());

            public RecordingBuilder GuardEval(Guid guard, Guid owner, bool result, int scope = 0) =>
                Add(BehaviorTreeEventKind.GuardEval, scope, guard, owner, flag: result);

            public RecordingBuilder Write(
                string key, string from, string to, Guid writer, int scope = 0,
                BehaviorTreeVariableKind variableKind = BehaviorTreeVariableKind.Object) =>
                Add(BehaviorTreeEventKind.VariableWrite, scope, Guid.Empty, writer,
                    key: key, oldValue: from, newValue: to, variableKind: variableKind);

            public RecordingBuilder ExternalWrite(
                string key, string from, string to, string writer, int scope = 0,
                BehaviorTreeVariableKind variableKind = BehaviorTreeVariableKind.Object) =>
                Add(BehaviorTreeEventKind.VariableWrite, scope, Guid.Empty,
                    key: key, oldValue: from, newValue: to, variableKind: variableKind, writer: writer);

            private RecordingBuilder Add(
                BehaviorTreeEventKind kind, int scope, Guid node, Guid related = default,
                ExecutionStatus status = ExecutionStatus.None, bool flag = false,
                string key = null, string oldValue = null, string newValue = null,
                BehaviorTreeVariableKind variableKind = BehaviorTreeVariableKind.None, string writer = null)
            {
                events.Add(BehaviorTreeEvent.Create(
                    kind, tick, sequence++, tick, tick * 0.02f, scope, node, related, status, flag,
                    key, oldValue, newValue, variableKind, writer));

                return this;
            }

            private readonly List<GuardTrace> traces = new();

            /// <summary>Attaches a trace to the most recently added event, the way a real capture does.</summary>
            public RecordingBuilder Trace(params (string name, string value)[] chain)
            {
                var last = events[events.Count - 1];
                var nodes = new List<GuardTraceNode>
                {
                    new(last.NodeGuid, "guard", "BooleanReactiveGuard", last.Flag ? "true" : "false", 0, -1, -1),
                };

                for (int i = 0; i < chain.Length; i++)
                {
                    nodes.Add(new GuardTraceNode(
                        Guid.NewGuid(), chain[i].name, "Node", chain[i].value, i + 1, i, -1));
                }

                traces.Add(new GuardTrace(
                    last.Tick, last.Sequence, last.CallSiteId, last.NodeGuid, last.RelatedGuid, last.Flag, nodes, null));

                return this;
            }

            public BehaviorTreeRecordingSnapshot Build(int dropped = 0) =>
                new("Zombie", "ZombieTree", tick, events, callSites, dropped, traces);
        }

        private static string TextOf(BehaviorTreeExplanation explanation) => explanation.ToString();

        private static bool HasClause(BehaviorTreeExplanation explanation, BehaviorTreeClauseRole role, string contains)
        {
            return explanation.Clauses.Any(clause =>
                clause.Role == role && clause.Text.IndexOf(contains, StringComparison.OrdinalIgnoreCase) >= 0);
        }

        #endregion

        #region Takeovers

        [Test]
        public void ATakeoverIsWordedAsGivingWayRatherThanAborted()
        {
            // The recorder keeps NodeTakenOver and NodeAborted apart on purpose: an aborted branch had a
            // guard turn false under it, a taken-over branch had a sibling outbid it, and the two send
            // whoever is asking to different places. This is the read side of that distinction.
            var recording = new RecordingBuilder()
                .At(400).Enter(Branch)
                .At(450).GuardEval(Guard, Sibling, true)
                        .TakenOver(Branch, Guard, "Attack", Sibling)
                        .Exit(Branch, ExecutionStatus.Failure)
                .Build();

            var topology = new StubTopology()
                .Node(Branch, "Idle")
                .Node(Sibling, "Attack")
                .Node(Guard, "target in range", "BooleanReactiveGuard");

            var explanation = BehaviorTreeExplainer.Explain(recording, 0, Branch, topology);

            Assert.AreEqual(BehaviorTreeOutcome.TakenOver, explanation.Outcome);
            StringAssert.Contains("Taken over at tick 450", explanation.Headline);
            StringAssert.Contains("Attack", explanation.Headline,
                "The preemptor is the answer — the place to look next is its guard, not this branch.");
            StringAssert.DoesNotContain("Aborted", explanation.Headline,
                "Nothing under this node turned false, and calling it an abort sends the reader to the wrong branch.");

            Assert.IsTrue(HasClause(explanation, BehaviorTreeClauseRole.Cause, "outranks"),
                "The cause is the priority, said as priority.");
            Assert.IsTrue(HasClause(explanation, BehaviorTreeClauseRole.Context, "entered at tick 400"));
            Assert.IsTrue(HasClause(explanation, BehaviorTreeClauseRole.Context, "returned Failure"),
                "The exit it caused is context, not hidden and not the headline.");
        }

        [Test]
        public void ATakeoverBlamesTheWriteThatWokeThePreemptorsGuard()
        {
            // Everything an abort hangs off the fall to false hangs here off the rise to true: the guard that
            // opened, and the write that opened it. The chain ends at the sensor, which is where the fix
            // usually lives.
            var recording = new RecordingBuilder()
                .At(400).Enter(Branch)
                .At(449).ExternalWrite("hasTarget", "False", "True", "VisionSensor")
                .At(450).GuardEval(Guard, Sibling, true)
                        .TakenOver(Branch, Guard, "Attack", Sibling)
                        .Exit(Branch, ExecutionStatus.Failure)
                .Build();

            var topology = new StubTopology()
                .Node(Branch, "Idle")
                .Node(Sibling, "Attack")
                .Node(Guard, "target in range", "BooleanReactiveGuard")
                .GuardReads(Guard, "hasTarget");

            var explanation = BehaviorTreeExplainer.Explain(recording, 0, Branch, topology);

            Assert.IsTrue(HasClause(explanation, BehaviorTreeClauseRole.Evidence, "turning true"),
                "The guard opening is the evidence; an abort's wording of 'turning false' would be the wrong direction.");
            StringAssert.Contains("VisionSensor", TextOf(explanation),
                "The write that woke the preemptor's guard is part of the answer, writer named.");
            StringAssert.Contains("hasTarget", TextOf(explanation));
        }

        [Test]
        public void ThePreemptorIsNamedFromTheRecordingWhenThereIsNoTopology()
        {
            // An imported recording has no tree to ask, so the name recorded at the moment of the takeover is
            // what survives — and when even that is missing, the sentence stays honest rather than quoting a
            // guid at a designer.
            var recorded = new RecordingBuilder()
                .At(400).Enter(Branch)
                .At(450).TakenOver(Branch, Guard, "Attack", Sibling).Exit(Branch, ExecutionStatus.Failure)
                .Build();

            StringAssert.Contains("Attack", BehaviorTreeExplainer.Explain(recorded, 0, Branch).Headline);

            var nameless = new RecordingBuilder()
                .At(400).Enter(Branch)
                .At(450).TakenOver(Branch, Guard, null, Guid.Empty).Exit(Branch, ExecutionStatus.Failure)
                .Build();

            StringAssert.Contains("a higher-priority branch", BehaviorTreeExplainer.Explain(nameless, 0, Branch).Headline);
        }

        #endregion

        #region Aborts

        [Test]
        public void AnAbortNamesTheGuardThatKilledTheBranch()
        {
            var recording = new RecordingBuilder()
                .At(400).Enter(Branch)
                .At(412).GuardEval(Guard, Branch, false).Aborted(Branch, Guard)
                .Build();

            var topology = new StubTopology()
                .Node(Branch, "Idle")
                .Node(Guard, "no enemy in range", "BooleanReactiveGuard");

            var explanation = BehaviorTreeExplainer.Explain(recording, 0, Branch, topology);

            Assert.AreEqual(BehaviorTreeOutcome.Aborted, explanation.Outcome);
            StringAssert.Contains("Aborted at tick 412", explanation.Headline);
            StringAssert.Contains("no enemy in range", explanation.Headline,
                "The guard is the answer; a designer should not have to click through to find out which one.");

            Assert.IsTrue(HasClause(explanation, BehaviorTreeClauseRole.Context, "entered at tick 400"),
                "How long it ran before dying is the difference between a bug and a design.");
        }

        [Test]
        public void AnAbortBlamesTheVariableTheGuardActuallyReads()
        {
            var recording = new RecordingBuilder()
                .At(400).Enter(Branch)
                .At(412)
                    .Write("hasTarget", "False", "True", Sensor)
                    .GuardEval(Guard, Branch, false)
                    .Aborted(Branch, Guard)
                .Build();

            var topology = new StubTopology()
                .Node(Branch, "Idle")
                .Node(Guard, "no enemy in range", "BooleanReactiveGuard")
                .Node(Sensor, "VisionService")
                .GuardReads(Guard, "hasTarget");

            var explanation = BehaviorTreeExplainer.Explain(recording, 0, Branch, topology);

            Assert.IsTrue(HasClause(explanation, BehaviorTreeClauseRole.Cause, "'hasTarget' changed False -> True"),
                "The write the guard reads is a cause, not a coincidence, and is stated as one.");
            Assert.IsTrue(HasClause(explanation, BehaviorTreeClauseRole.Cause, "VisionService"),
                "Naming the writer is the whole point — that is the node a designer has to go and look at.");

            Assert.IsFalse(explanation.Clauses.Any(clause => clause.Role == BehaviorTreeClauseRole.Caveat),
                "Nothing is being guessed here, so nothing should be hedged.");
        }

        [Test]
        public void AWriteIsOfferedAsCorrelationWhenTheGuardsInputsCannotBeResolved()
        {
            var recording = new RecordingBuilder()
                .At(400).Enter(Branch)
                .At(412)
                    .Write("unrelatedTimer", "1", "2", Sensor)
                    .GuardEval(Guard, Branch, false)
                    .Aborted(Branch, Guard)
                .Build();

            // No GuardReads: the guard is fed by a script graph the topology cannot see through.
            var topology = new StubTopology().Node(Branch, "Idle").Node(Guard, "some guard");

            var explanation = BehaviorTreeExplainer.Explain(recording, 0, Branch, topology);

            Assert.IsTrue(HasClause(explanation, BehaviorTreeClauseRole.Evidence, "'unrelatedTimer' changed"),
                "The last write is still the most useful thing to show.");
            Assert.IsTrue(HasClause(explanation, BehaviorTreeClauseRole.Caveat, "not a proven cause"),
                "A tool built to replace guessing must not quietly guess. Overclaiming here costs a designer an afternoon.");
        }

        [Test]
        public void AnExternalWriterIsNamedEvenThoughItHasNoNodeGuid()
        {
            var recording = new RecordingBuilder()
                .At(400).Enter(Branch)
                .At(412)
                    .ExternalWrite("hasTarget", "False", "True", "TargetingSensor")
                    .GuardEval(Guard, Branch, false)
                    .Aborted(Branch, Guard)
                .Build();

            var topology = new StubTopology()
                .Node(Branch, "Idle")
                .Node(Guard, "no enemy in range")
                .GuardReads(Guard, "hasTarget");

            var explanation = BehaviorTreeExplainer.Explain(recording, 0, Branch, topology);

            Assert.IsTrue(HasClause(explanation, BehaviorTreeClauseRole.Cause, "TargetingSensor"),
                "Facts come from sensors, so the commonest answer to 'who wrote this' is a name rather than a guid.");
        }

        #endregion

        #region Skips and never-ran

        [Test]
        public void ASkipIsWordedAsNeverEnteredRatherThanAborted()
        {
            var recording = new RecordingBuilder()
                .At(300).GuardEval(Guard, Branch, false)
                .At(301).Skipped(Branch, Guard)
                .Build();

            var topology = new StubTopology().Node(Branch, "Idle").Node(Guard, "no enemy in range");

            var explanation = BehaviorTreeExplainer.Explain(recording, 0, Branch, topology);

            Assert.AreEqual(BehaviorTreeOutcome.Skipped, explanation.Outcome);
            StringAssert.Contains("Never entered", explanation.Headline);
            StringAssert.DoesNotContain("Aborted", explanation.Headline,
                "Conflating these two was the bug Finding 3 records: 'never started' and 'was killed' send a designer to different places.");

            Assert.IsTrue(HasClause(explanation, BehaviorTreeClauseRole.Evidence, "false since tick 300"),
                "How long it has been false separates a momentary flicker from a branch that has been dead all session.");
        }

        [Test]
        public void ABranchThatNeverRanIsExplainedByThePriorityThatBeatIt()
        {
            var recording = new RecordingBuilder()
                .At(300).Enter(Sibling)
                .Build();

            var topology = new StubTopology()
                .Node(Parent, "Root Selector", "Selector", default, Sibling, Branch)
                .Node(Sibling, "Combat", "Sequence", Parent)
                .Node(Branch, "Idle", "Sequence", Parent);

            var explanation = BehaviorTreeExplainer.Explain(recording, 0, Branch, topology);

            Assert.AreEqual(BehaviorTreeOutcome.NoRecord, explanation.Outcome);
            Assert.IsTrue(HasClause(explanation, BehaviorTreeClauseRole.Cause, "ran 'Combat' (priority 1)"),
                "Nothing about this node is in the buffer, so the only available answer is what outranked it.");
            Assert.IsTrue(HasClause(explanation, BehaviorTreeClauseRole.Cause, "this node is priority 2"));
        }

        [Test]
        public void ALowerPriorityBranchRunningIsNotOfferedAsAReason()
        {
            var recording = new RecordingBuilder()
                .At(300).Enter(Sibling)
                .Build();

            // Branch outranks Sibling here, so Sibling running is a consequence of Branch not running, not a
            // cause of it. Reversing that reads plausibly and sends the designer the wrong way.
            var topology = new StubTopology()
                .Node(Parent, "Root Selector", "Selector", default, Branch, Sibling)
                .Node(Branch, "Combat", "Sequence", Parent)
                .Node(Sibling, "Idle", "Sequence", Parent);

            var explanation = BehaviorTreeExplainer.Explain(recording, 0, Branch, topology);

            Assert.IsFalse(explanation.Clauses.Any(clause => clause.Text.Contains("priority")),
                "A lower-priority sibling running explains nothing about a higher-priority one.");
        }

        [Test]
        public void AnEmptyRecordingSaysSoRatherThanBlamingTheNode()
        {
            var recording = new RecordingBuilder().At(0).Build();

            var explanation = BehaviorTreeExplainer.Explain(recording, 0, Branch);

            Assert.IsTrue(HasClause(explanation, BehaviorTreeClauseRole.Caveat, "recording is empty"),
                "'It never ran' and 'nothing was recorded' look identical from here, and only one is the tree's fault.");
        }

        [Test]
        public void ANodeThatHasNotStartedYetIsNotSaidToHaveNeverRun()
        {
            // Explaining at a scrubbed tick, "nothing about this node is in the recording" is a claim about the
            // whole recording that the recording contradicts two ticks later. The distinction only became
            // reachable when the why panel started following the scrubber.
            var recording = new RecordingBuilder()
                .At(0).Enter(Parent)
                .At(2).Enter(Branch)
                .At(40).Exit(Branch, ExecutionStatus.Success)
                .Build();

            var explanation = BehaviorTreeExplainer.Explain(recording, 0, Branch, null, 1);

            StringAssert.Contains("Has not run yet as of tick 1", explanation.Headline);
            StringAssert.Contains("tick 2", explanation.Headline,
                "Where to scrub to is the most useful thing this answer can carry.");

            Assert.IsFalse(explanation.Headline.Contains("Never ran"),
                "It ran at tick 2. Saying 'never' from tick 1 is the panel contradicting its own recording.");
        }

        [Test]
        public void ANodeWithNothingAnywhereInTheRecordingStillSaysNeverRan()
        {
            var recording = new RecordingBuilder()
                .At(0).Enter(Parent)
                .At(40).Exit(Parent, ExecutionStatus.Success)
                .Build();

            var explanation = BehaviorTreeExplainer.Explain(recording, 0, Branch, null, 1);

            StringAssert.Contains("Never ran", explanation.Headline,
                "Looking forward must not soften the answer when there is genuinely nothing to find.");
        }

        [Test]
        public void AClippedRecordingRefusesToClaimTheNodeNeverRan()
        {
            var recording = new RecordingBuilder()
                .At(300).Enter(Sibling)
                .Build(dropped: 40);

            var explanation = BehaviorTreeExplainer.Explain(recording, 0, Branch);

            Assert.IsTrue(HasClause(explanation, BehaviorTreeClauseRole.Caveat, "clipped"),
                "The buffer scrolled; anything before its start is unaccounted for and the sentence must admit it.");
        }

        #endregion

        #region Exits and running

        [Test]
        public void AFailureNamesTheChildThatCausedIt()
        {
            var recording = new RecordingBuilder()
                .At(500).Enter(Branch).Enter(Child)
                .At(520).Exit(Child, ExecutionStatus.Failure).Exit(Branch, ExecutionStatus.Failure)
                .Build();

            var topology = new StubTopology()
                .Node(Branch, "Chase", "Sequence", default, Child)
                .Node(Child, "WaitUntilReachNavTargetPosition", "WaitUntilReachNavTargetPosition", Branch);

            var explanation = BehaviorTreeExplainer.Explain(recording, 0, Branch, topology);

            Assert.AreEqual(BehaviorTreeOutcome.Failed, explanation.Outcome);
            StringAssert.Contains("Exited Failure at tick 520", explanation.Headline);
            Assert.IsTrue(HasClause(explanation, BehaviorTreeClauseRole.Cause, "'WaitUntilReachNavTargetPosition' returned Failure"));
        }

        [Test]
        public void AChildIsOnlyBlamedWhenTheTopologyConfirmsItIsAChild()
        {
            var recording = new RecordingBuilder()
                .At(500).Enter(Branch)
                .At(520).Exit(Sibling, ExecutionStatus.Failure).Exit(Branch, ExecutionStatus.Failure)
                .Build();

            // Sibling exits immediately before Branch, which is exactly what a positional guess would fall for.
            var topology = new StubTopology()
                .Node(Branch, "Chase", "Sequence")
                .Node(Sibling, "Something Else");

            var explanation = BehaviorTreeExplainer.Explain(recording, 0, Branch, topology);

            Assert.IsFalse(explanation.Clauses.Any(clause => clause.Text.Contains("Something Else")),
                "Adjacent is not the same as caused by, and a guess in a tool built to replace guessing is worse than silence.");
        }

        /// <summary>
        /// Caught by running the demo rather than by reading the code: a real abort produces
        /// <c>NodeAborted</c> and then <c>NodeExit Failure</c> in the same tick, so taking the last event at
        /// face value reported "Exited Failure" and dropped the guard — losing the answer in precisely the
        /// case the panel exists for.
        /// </summary>
        [Test]
        public void AnAbortIsNotHiddenByTheExitItCauses()
        {
            var recording = new RecordingBuilder()
                .At(400).Enter(Branch)
                .At(412)
                    .GuardEval(Guard, Branch, false)
                    .Aborted(Branch, Guard)
                    .Exit(Branch, ExecutionStatus.Failure)
                .Build();

            var topology = new StubTopology().Node(Branch, "Idle").Node(Guard, "no enemy in range");

            var explanation = BehaviorTreeExplainer.Explain(recording, 0, Branch, topology);

            Assert.AreEqual(BehaviorTreeOutcome.Aborted, explanation.Outcome,
                "The exit is a consequence of the abort, not a separate ending, and it carries none of the cause.");
            StringAssert.Contains("no enemy in range", explanation.Headline);
            Assert.IsTrue(HasClause(explanation, BehaviorTreeClauseRole.Context, "returned Failure to its parent"),
                "The status is still worth reporting — as an effect of the abort rather than instead of it.");
        }

        [Test]
        public void AnExitThatFollowsAReEntryIsStillAnExit()
        {
            // Abort, re-enter, exit cleanly — all in one tick. The abort belongs to the earlier episode and
            // must not be dragged forward over the exit that ended the later one.
            var recording = new RecordingBuilder()
                .At(412)
                    .Aborted(Branch, Guard)
                    .Enter(Branch)
                    .Exit(Branch, ExecutionStatus.Success)
                .Build();

            var explanation = BehaviorTreeExplainer.Explain(recording, 0, Branch);

            Assert.AreEqual(BehaviorTreeOutcome.Succeeded, explanation.Outcome);
        }

        [Test]
        public void ARunningNodeReportsHowLongItHasBeenRunning()
        {
            var recording = new RecordingBuilder()
                .At(100).Enter(Branch)
                .At(140).Build();

            var explanation = BehaviorTreeExplainer.Explain(recording, 0, Branch, atTick: 140);

            Assert.AreEqual(BehaviorTreeOutcome.Running, explanation.Outcome);
            StringAssert.Contains("Running since tick 100", explanation.Headline);
            StringAssert.Contains("41 ticks", explanation.Headline);
        }

        [Test]
        public void ExplainingFromAnEarlierTickIgnoresWhatHappenedAfterIt()
        {
            var recording = new RecordingBuilder()
                .At(100).Enter(Branch)
                .At(200).Aborted(Branch, Guard)
                .Build();

            var explanation = BehaviorTreeExplainer.Explain(recording, 0, Branch, atTick: 150);

            Assert.AreEqual(BehaviorTreeOutcome.Running, explanation.Outcome,
                "At tick 150 it was still running. An explanation that leaked the future would make the scrubber lie.");
        }

        #endregion

        #region Call sites

        [Test]
        public void TheSameBranchAtTwoCallSitesGetsTwoDifferentAnswers()
        {
            // One shared Attack asset, two call sites. The node guids are identical because the clone keeps
            // them, which is Finding 1 — addressing by guid alone would merge these two into one wrong story.
            var recording = new RecordingBuilder()
                .CallSite(1, 0, "Attack")
                .CallSite(2, 0, "Attack")
                .At(300).Enter(Branch, scope: 1)
                .At(300).Skipped(Branch, Guard, scope: 2)
                .Build();

            var running = BehaviorTreeExplainer.Explain(recording, 1, Branch);
            var skipped = BehaviorTreeExplainer.Explain(recording, 2, Branch);

            Assert.AreEqual(BehaviorTreeOutcome.Running, running.Outcome);
            Assert.AreEqual(BehaviorTreeOutcome.Skipped, skipped.Outcome,
                "Two copies of one branch are in different states, and a guid alone cannot tell them apart.");

            CollectionAssert.AreEquivalent(new[] { 1, 2 }, BehaviorTreeExplainer.CallSitesFor(recording, Branch),
                "The canvas knows which node was clicked but not which copy, so the panel has to be able to ask.");
        }

        [Test]
        public void TheCallSitePathReadsFromTheRootDown()
        {
            var recording = new RecordingBuilder()
                .CallSite(1, 0, "Combat")
                .CallSite(2, 1, "Attack")
                .At(10).Enter(Branch, scope: 2)
                .Build();

            var explanation = BehaviorTreeExplainer.Explain(recording, 2, Branch);

            Assert.AreEqual("Zombie -> Combat -> Attack", explanation.CallSitePath,
                "'Attack' alone is ambiguous the moment a branch is reused, which is the point of reusing it.");
        }

        #endregion

        #region Oscillation

        [Test]
        public void RepeatedAbortsAreCalledOutAsOscillation()
        {
            var recording = new RecordingBuilder();

            for (int tick = 100; tick < 140; tick += 8)
            {
                recording.At(tick).Enter(Branch);
                recording.At(tick + 4).GuardEval(Guard, Branch, false).Aborted(Branch, Guard);
            }

            var explanation = BehaviorTreeExplainer.Explain(recording.Build(), 0, Branch);

            Assert.IsTrue(HasClause(explanation, BehaviorTreeClauseRole.Context, "Oscillating"),
                "Selector thrash is the classic behavior tree bug and the buffer already contains the proof.");
        }

        #endregion

        #region Against the real recorder

        /// <summary>
        /// The hand-built cases above agree with themselves. This one checks the engine against what the
        /// recorder actually writes, which is the only thing that can catch the two drifting apart.
        /// </summary>
        [Test]
        public void ARealAbortRecordedByARealTreeExplainsAsAnAbort()
        {
            BehaviorTreeFlightRecorders.Reset();

            var recorder = new BehaviorTreeFlightRecorder("Zombie", "ZombieTree");
            var graph = new BehaviorTreeGraph();

            var sequence = new Sequence { Position = new Rect(0.0f, 100.0f, 150.0f, 100.0f) };
            graph.Nodes.Add(sequence);

            var child = new ScriptedNode(ExecutionStatus.Running) { Position = new Rect(0.0f, 300.0f, 150.0f, 100.0f) };
            graph.Nodes.Add(child);

            Connect(graph, graph.EntryNode, sequence);
            Connect(graph, sequence, child);

            var guard = new BooleanReactiveGuard { Position = new Rect(-150.0f, 100.0f, 150.0f, 100.0f) };
            graph.Nodes.Add(guard);
            guard.UpdateOwner(sequence);
            guard.Value.SetDefaultValue(true);

            graph.OnAwake();

            foreach (var node in graph.Nodes)
            {
                node.SetFlightRecorder(recorder);
            }

            recorder.BeginTick();
            sequence.OnNodeEnter();
            sequence.OnUpdateInternal();

            recorder.BeginTick();
            guard.Value.SetDefaultValue(false);
            sequence.OnUpdateInternal();

            var explanation = BehaviorTreeExplainer.Explain(recorder, 0, sequence.guid, BehaviorTreeGraphTopology.From(graph));

            Assert.AreEqual(BehaviorTreeOutcome.Aborted, explanation.Outcome, TextOf(explanation));
            StringAssert.Contains("Reactive Guard", explanation.Headline,
                "Names come from the graph, so a real tree explains with the names on the canvas.");
        }

        [Test]
        public void ARealSkipIsNotReportedAsAnAbort()
        {
            BehaviorTreeFlightRecorders.Reset();

            var recorder = new BehaviorTreeFlightRecorder("Zombie", "ZombieTree");
            var graph = new BehaviorTreeGraph();

            var sequence = new Sequence { Position = new Rect(0.0f, 100.0f, 150.0f, 100.0f) };
            graph.Nodes.Add(sequence);

            var child = new ScriptedNode(ExecutionStatus.Running) { Position = new Rect(0.0f, 300.0f, 150.0f, 100.0f) };
            graph.Nodes.Add(child);

            Connect(graph, graph.EntryNode, sequence);
            Connect(graph, sequence, child);

            var guard = new BooleanReactiveGuard { Position = new Rect(-150.0f, 100.0f, 150.0f, 100.0f) };
            graph.Nodes.Add(guard);
            guard.UpdateOwner(sequence);
            guard.Value.SetDefaultValue(false);

            graph.OnAwake();

            foreach (var node in graph.Nodes)
            {
                node.SetFlightRecorder(recorder);
            }

            recorder.BeginTick();
            sequence.OnNodeEnter();

            var explanation = BehaviorTreeExplainer.Explain(recorder, 0, sequence.guid, BehaviorTreeGraphTopology.From(graph));

            Assert.AreEqual(BehaviorTreeOutcome.Skipped, explanation.Outcome, TextOf(explanation));
        }

        private static void Connect(BehaviorTreeGraph graph, BehaviorTreeNode parent, BehaviorTreeNode child)
        {
            var transition = new BehaviorTreeTransition();
            transition.SetupTransition(parent, child, 0);
            graph.Transitions.Add(transition);
        }

        #endregion

        #region Guard traces

        [Test]
        public void AnAbortShowsWhatTheGuardWasReading()
        {
            var recording = new RecordingBuilder()
                .At(400).Enter(Branch)
                .At(412)
                    .GuardEval(Guard, Branch, false).Trace(("Not", "False"), ("hasTarget", "True"))
                    .Aborted(Branch, Guard)
                .Build();

            var topology = new StubTopology().Node(Branch, "Idle").Node(Guard, "not hasTarget");

            var explanation = BehaviorTreeExplainer.Explain(recording, 0, Branch, topology);

            Assert.IsNotNull(explanation.Trace,
                "The trace is attached by the guard flip's (tick, seq) — that pairing is the only link between them.");
            Assert.AreEqual(3, explanation.Trace.Chain.Count);

            Assert.IsTrue(HasClause(explanation, BehaviorTreeClauseRole.Cause, "Not -> False <- hasTarget -> True"),
                "'It returned false' restates the question; what it read is the answer.");
        }

        [Test]
        public void ATraceIsFoundOnlyForTheFlipItBelongsTo()
        {
            // A guard that flips repeatedly has one trace per flip, and an explanation about the second must
            // not pick up the first.
            var recording = new RecordingBuilder()
                .At(100).GuardEval(Guard, Branch, false).Trace(("hasTarget", "False"))
                .At(200).GuardEval(Guard, Branch, true).Trace(("hasTarget", "True"))
                .Build();

            var early = recording.TraceFor(100, 0);
            var late = recording.TraceFor(200, 0);

            Assert.AreEqual("hasTarget -> False", early.Describe());
            Assert.AreEqual("hasTarget -> True", late.Describe());
            Assert.IsNull(recording.TraceFor(150, 0), "A tick with no transition has no trace.");
        }

        [Test]
        public void TheTraceRingKeepsTheNewestAndSaysWhatItLost()
        {
            var ring = new GuardTraceRing(2);

            for (int i = 0; i < 4; i++)
            {
                ring.Add(new GuardTrace(i, 0, 0, Guard, Branch, true, null, null));
            }

            Assert.AreEqual(2, ring.Count);
            Assert.AreEqual(2, ring.Dropped);
            Assert.IsNull(ring.Find(0, 0), "The oldest traces scrolled off, and Find must not pretend otherwise.");
            Assert.IsNotNull(ring.Find(3, 0));
        }

        [Test]
        public void AGuardWithNothingConnectedProducesNoTrace()
        {
            var graph = new BehaviorTreeGraph();
            var sequence = new Sequence { Position = new Rect(0.0f, 100.0f, 150.0f, 100.0f) };
            graph.Nodes.Add(sequence);

            var guard = new BooleanReactiveGuard { Position = new Rect(-150.0f, 100.0f, 150.0f, 100.0f) };
            graph.Nodes.Add(guard);
            guard.UpdateOwner(sequence);
            guard.Value.SetDefaultValue(true);

            var trace = GuardTraceCapture.Capture(sequence, guard, true, tick: 1, sequence: 0, callSiteId: 0);

            Assert.IsNull(trace,
                "A guard reading its own inline value has no chain, and a row saying only what the sentence already said is noise.");
        }

        [Test]
        public void CaptureRePullsTheChainFromARealGraph()
        {
            var graph = new BehaviorTreeGraph();

            var sequence = new Sequence { Position = new Rect(0.0f, 100.0f, 150.0f, 100.0f) };
            graph.Nodes.Add(sequence);

            var guard = new BooleanReactiveGuard { Position = new Rect(-150.0f, 100.0f, 150.0f, 100.0f) };
            graph.Nodes.Add(guard);
            guard.UpdateOwner(sequence);

            // Not's own input is left at its default false, so Result is true. Behavior tree ports keep no
            // history, so this value exists in the trace only because capture re-pulls the port.
            var not = new Not { Position = new Rect(-300.0f, 100.0f, 150.0f, 100.0f) };
            graph.Nodes.Add(not);
            not.Result.ValidlyConnectTo(guard.Value);

            var trace = GuardTraceCapture.Capture(sequence, guard, true, tick: 7, sequence: 2, callSiteId: 0);

            Assert.IsNotNull(trace);
            Assert.AreEqual(7, trace.Tick);
            Assert.AreEqual(2, trace.Sequence);
            Assert.AreEqual(2, trace.Chain.Count, trace.Describe());
            Assert.AreEqual(not.guid, trace.Chain[1].NodeGuid);
            Assert.AreEqual("True", trace.Chain[1].Value,
                "Re-pulling is the only way to recover this — nothing on the behavior tree side records it.");
        }

        [Test]
        public void AWriteFromInsideAScriptGraphIsAttributedToTheNodeThatRanIt()
        {
            var recorder = new BehaviorTreeFlightRecorder("Zombie", "ZombieTree");
            var node = new ScriptedNode { Position = new Rect(0.0f, 100.0f, 150.0f, 100.0f) };
            node.SetFlightRecorder(recorder);

            // What VisualScriptGraphVariable does around the graph it runs.
            BehaviorTreeRecorder.PushScriptGraphOwner(node);
            BehaviorTreeRecorder.ScriptGraphVariableWrite(
                null, "Script Graph", "hasTarget", BehaviorTreeVariableKind.Object, false, true);
            BehaviorTreeRecorder.PopScriptGraphOwner();

            var written = recorder.EventAt(0);

            Assert.AreEqual(BehaviorTreeEventKind.VariableWrite, written.Kind);
            Assert.AreEqual(node.guid, written.RelatedGuid,
                "A unit inside a graph has no guid of its own, so the node that ran the graph is the locatable writer.");
            Assert.IsNull(written.Writer, "With a node to name, the weaker name field should stay empty.");
        }

        [Test]
        public void AWriteWithNoOwningNodeFallsBackToTheWriterName()
        {
            var recorder = new BehaviorTreeFlightRecorder("Zombie", "ZombieTree");
            var machine = new UnityEngine.GameObject("Zombie").AddComponent<BehaviorTreeMachine>();
            machine.SetFlightRecorder(recorder);

            try
            {
                // No push: a graph run outside a tree still has a writer worth recording, just not one the
                // canvas can point at.
                BehaviorTreeRecorder.ScriptGraphVariableWrite(
                    machine, "VisionCheck", "hasTarget", BehaviorTreeVariableKind.Object, false, true);

                var written = recorder.EventAt(0);

                Assert.AreEqual("VisionCheck", written.Writer);
                Assert.AreEqual(Guid.Empty, written.RelatedGuid);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(machine.gameObject);
            }
        }

        #endregion

        #region Export and re-import

        [Test]
        public void AnExportedRecordingExplainsTheSameWayAfterBeingReadBack()
        {
            var original = new RecordingBuilder()
                .At(400).Enter(Branch)
                .At(412)
                    .ExternalWrite("hasTarget", "False", "True", "TargetingSensor")
                    .GuardEval(Guard, Branch, false)
                    .Aborted(Branch, Guard)
                .Build();

            var recorder = new BehaviorTreeFlightRecorder("Zombie", "ZombieTree");
            for (int i = 0; i < original.EventCount; i++)
            {
                recorder.Events.Add(original.EventAt(i));
            }

            var reimported = BehaviorTreeRecordingImport.FromJson(BehaviorTreeRecordingDump.ToJson(recorder));

            var topology = new StubTopology()
                .Node(Branch, "Idle")
                .Node(Guard, "no enemy in range")
                .GuardReads(Guard, "hasTarget");

            var before = BehaviorTreeExplainer.Explain(original, 0, Branch, topology, atTick: 412);
            var after = BehaviorTreeExplainer.Explain(reimported, 0, Branch, topology, atTick: 412);

            Assert.AreEqual(before.Outcome, after.Outcome);
            Assert.AreEqual(before.Headline, after.Headline);
            CollectionAssert.AreEqual(
                before.Clauses.Select(clause => clause.Text).ToArray(),
                after.Clauses.Select(clause => clause.Text).ToArray(),
                "A recording that cannot be re-opened is a log file, not evidence.");
        }

        [Test]
        public void AnImportedRecordingKeepsWhoWroteAValue()
        {
            var recorder = new BehaviorTreeFlightRecorder("Zombie", "ZombieTree");
            recorder.ExternalVariableWrite("TargetingSensor", "hasTarget", false, true);

            var reimported = BehaviorTreeRecordingImport.FromJson(BehaviorTreeRecordingDump.ToJson(recorder));

            var written = reimported.EventAt(0);

            Assert.AreEqual(BehaviorTreeEventKind.VariableWrite, written.Kind);
            Assert.AreEqual("TargetingSensor", written.Writer,
                "The dump writes a node guid and a sensor name into one field; telling them apart on the way back is the importer's job.");
            Assert.AreEqual(Guid.Empty, written.RelatedGuid);
        }

        [Test]
        public void AnExportedTraceStillExplainsWhyTheGuardWasFalse()
        {
            var recorder = new BehaviorTreeFlightRecorder("Zombie", "ZombieTree");

            var chain = new List<GuardTraceNode>
            {
                new(Guard, "not hasTarget", "BooleanReactiveGuard", "false", 0, -1, -1),
                new(Sibling, "Not", "Not", "False", 1, 0, 0),
            };

            var wires = new List<GuardWireValue>
            {
                new(Guid.NewGuid(), Guid.NewGuid(), "value", Guid.NewGuid(), "Result", "True", true),
                new(Guid.NewGuid(), Guid.NewGuid(), "output", Guid.NewGuid(), "fallback", "(not evaluated)", false),
            };

            recorder.Traces.Add(new GuardTrace(
                412, 3, 0, Guard, Branch, false, chain,
                new List<GuardGraphSnapshot> { new(Sibling, "hasTargetRead", wires) }));

            var reimported = BehaviorTreeRecordingImport.FromJson(BehaviorTreeRecordingDump.ToJson(recorder));
            var trace = reimported.TraceFor(412, 3);

            Assert.IsNotNull(trace, "A recording whose traces do not survive export explains less after a round trip than before it.");
            Assert.AreEqual("Not -> False", trace.Describe());
            Assert.AreEqual(1, trace.Snapshots.Count);
            Assert.AreEqual(2, trace.Snapshots[0].Wires.Count);

            Assert.IsTrue(trace.Snapshots[0].Wires[0].WasEvaluated);
            Assert.AreEqual("True", trace.Snapshots[0].Wires[0].Value);

            Assert.IsFalse(trace.Snapshots[0].Wires[1].WasEvaluated,
                "A wire the flow never took is information about which way it went, not a missing field.");
            Assert.AreEqual("(not evaluated)", trace.Snapshots[0].Wires[1].Label);
        }

        [Test]
        public void MalformedJsonIsRejectedRatherThanPartiallyLoaded()
        {
            Assert.IsFalse(BehaviorTreeRecordingImport.TryFromJson("{ \"agent\": ", out _),
                "A truncated file should fail loudly, not produce a recording that quietly explains nothing.");
        }

        #endregion
    }
}
