using System;
using System.Collections;
using System.Linq;
using ArcaneOnyx.BehaviorTree.Debugging;
using ArcaneOnyx.GraphCore;
using NUnit.Framework;
using Unity.VisualScripting;
using UnityEngine;
using UnityEngine.TestTools;

namespace ArcaneOnyx.BehaviorTree.Tests.PlayMode
{
    /// <summary>
    /// The why-inspector's answers, read off recordings a real machine produced.
    ///
    /// <para>
    /// The edit-mode suite pins the sentences thoroughly, but almost every recording it explains was built by
    /// hand — and the one play-mode test whose name promises otherwise
    /// (<c>ARecordingExplainsWhyTheAgentSwitchedBranches</c>) asserts on the raw event stream and never calls
    /// <see cref="BehaviorTreeExplainer.Explain"/> at all. So nothing anywhere checked that the recordings
    /// the recorder actually writes — skips emitted by real guards, aborts under real scheduling, takeovers
    /// from a real Selector scan, guard traces captured from live graph pulls — produce the explanations the
    /// panel promises. That is this suite.
    /// </para>
    ///
    /// <para>
    /// The topology is <see cref="BehaviorTreeGraphTopology.From(BehaviorTreeMachine)"/>, the same resolution
    /// the panel uses, so names in the assertions are the names a designer would read.
    /// </para>
    /// </summary>
    public class ExplainerIntegrationTests : PlayModeAgentFixture
    {
        [UnityTest]
        public IEnumerator ARealPreemptionExplainsAsTakenOverRatherThanAsAPlainExit()
        {
            // The read side of the E2E recording test's promise: it asserts the stream distinguishes a
            // takeover from an abort "which reads differently to whoever is asking why" — this is whoever
            // asking why. Idle gives way when Attack's guard opens, and the explanation must say who took the
            // slot and which guard let them, not merely that Idle exited.
            var tree = BuildZombieTree(out var attack, out var idle, out var attackGuard);
            var machine = Spawn(tree, (_, variables) => variables.declarations.Set("hasTarget", false));

            for (int frame = 0; frame < 3; frame++)
            {
                yield return null;
            }

            Assert.IsTrue(Entered(machine.FlightRecorder, idle), "With no target, Idle is the branch that holds.");

            AgentVariableWriter.On(machine.gameObject).Write("TargetingSensor", "hasTarget", true);

            for (int frame = 0; frame < 4; frame++)
            {
                yield return null;
            }

            var recorder = machine.FlightRecorder;

            Assert.IsTrue(recorder.Events.Any(e => e.Kind == BehaviorTreeEventKind.NodeTakenOver),
                "The Selector must have recorded the takeover, or this explains nothing.");

            var topology = BehaviorTreeGraphTopology.From(machine);
            var explanation = BehaviorTreeExplainer.Explain(recorder, BehaviorTreeCallSite.RootId, idle, topology);

            Assert.AreEqual(BehaviorTreeOutcome.TakenOver, explanation.Outcome,
                "Idle was outbid, not aborted and not a plain exit — the recording keeps those apart so the answer can.");
            StringAssert.Contains("Taken over", explanation.Headline);

            // By guid rather than by name: both branches here are WaitTime nodes, so their display names are
            // identical and the honest check is that the cause clause points at the winner on the canvas.
            var cause = explanation.Clauses.Single(c => c.Role == BehaviorTreeClauseRole.Cause && c.Text.Contains("outranks"));

            Assert.AreEqual(attack, cause.Link.NodeGuid,
                "The cause clause links to the preemptor, because its guard is where to look next.");
            Assert.IsTrue(explanation.Clauses.Any(c => c.Text.Contains("TargetingSensor")),
                "and the chain reaches the write that woke the preemptor's guard, writer named — that is where the fix lives.");
        }

        [UnityTest]
        public IEnumerator ARealAbortExplainsWithTheGuardTheTraceAndTheWriterNamed()
        {
            // The full abort chain on a live agent: a sensor writes, the guard falls, the branch dies — and
            // the explanation walks it backwards, guard named in the headline, the guard chain captured from
            // a real graph pull, and the sensor named as the writer. The trace is the part the docs flag as
            // fragile (a short ring, live pulls), which is exactly why it needs a run under real scheduling.
            var tree = BuildGuardedHold(out var hold, out var guard, watchKey: "safe");
            var machine = Spawn(tree, (_, variables) => variables.declarations.Set("safe", true));

            for (int frame = 0; frame < 3; frame++)
            {
                yield return null;
            }

            Assert.IsTrue(Entered(machine.FlightRecorder, hold), "The guarded branch has to be running before it can be aborted.");

            AgentVariableWriter.On(machine.gameObject).Write("TestSensor", "safe", false);

            for (int frame = 0; frame < 4; frame++)
            {
                yield return null;
            }

            var recorder = machine.FlightRecorder;
            var topology = BehaviorTreeGraphTopology.From(machine);
            var explanation = BehaviorTreeExplainer.Explain(recorder, BehaviorTreeCallSite.RootId, hold, topology);

            Assert.AreEqual(BehaviorTreeOutcome.Aborted, explanation.Outcome);
            StringAssert.Contains("Aborted", explanation.Headline);

            Assert.IsTrue(explanation.Clauses.Any(c =>
                    c.Role == BehaviorTreeClauseRole.Evidence && c.Text.Contains("turning false")),
                "The guard's fall is the evidence the abort rests on.");
            Assert.IsTrue(explanation.Clauses.Any(c => c.Text.Contains("TestSensor")),
                "The writer that flipped the guard is named — 'who wrote this' is the question the panel exists for.");
            Assert.IsNotNull(explanation.Trace,
                "A real flip captures a guard chain. If this is null, capture broke under real scheduling and "
                + "every 'graph' button in the panel is a dead end.");
        }

        [UnityTest]
        public IEnumerator ARealSkipExplainsAsNeverEnteredRatherThanAborted()
        {
            // The wording rule with the recording produced by a real guard declining entry: never-entered and
            // ran-and-was-killed are different afternoons, and the real skip event has to land on the right one.
            var tree = BuildGuardedHold(out var hold, out _, watchKey: "safe");
            var machine = Spawn(tree, (_, variables) => variables.declarations.Set("safe", false));

            for (int frame = 0; frame < 3; frame++)
            {
                yield return null;
            }

            var recorder = machine.FlightRecorder;

            Assert.IsFalse(Entered(recorder, hold), "The guard held it out, or there is no skip to explain.");

            var explanation = BehaviorTreeExplainer.Explain(
                recorder, BehaviorTreeCallSite.RootId, hold, BehaviorTreeGraphTopology.From(machine));

            Assert.AreEqual(BehaviorTreeOutcome.Skipped, explanation.Outcome);
            StringAssert.Contains("Never entered", explanation.Headline);
            StringAssert.DoesNotContain("Aborted", explanation.Headline);
        }

        [UnityTest]
        public IEnumerator ExplainingFromBeforeTheAbortStillSaysRunning()
        {
            // The scrubber's contract on a real recording: parked before the abort, the panel must describe
            // that moment, not leak the future it already knows about.
            var tree = BuildGuardedHold(out var hold, out _, watchKey: "safe");
            var machine = Spawn(tree, (_, variables) => variables.declarations.Set("safe", true));

            for (int frame = 0; frame < 3; frame++)
            {
                yield return null;
            }

            int tickWhileRunning = machine.FlightRecorder.Tick;

            AgentVariableWriter.On(machine.gameObject).Write("TestSensor", "safe", false);

            for (int frame = 0; frame < 4; frame++)
            {
                yield return null;
            }

            var recorder = machine.FlightRecorder;
            var topology = BehaviorTreeGraphTopology.From(machine);

            Assert.AreEqual(BehaviorTreeOutcome.Aborted,
                BehaviorTreeExplainer.Explain(recorder, BehaviorTreeCallSite.RootId, hold, topology).Outcome,
                "Seen from the end, the abort is the story,");

            Assert.AreEqual(BehaviorTreeOutcome.Running,
                BehaviorTreeExplainer.Explain(recorder, BehaviorTreeCallSite.RootId, hold, topology, tickWhileRunning).Outcome,
                "but parked before it, the node was simply running — the panel must not know the future.");
        }

        [UnityTest]
        public IEnumerator ASharedBranchAtTwoRealCallSitesIsTwoDifferentStories()
        {
            // The call-site machinery through the real path: the machine instantiating one branch asset at
            // two call sites, rather than a test registering ids by hand. The same guid must offer two call
            // sites, and the path must read through the sub-tree rather than pretending the node ran at root.
            var branch = BuildHoldingBranch(out var hold);

            var tree = NewTree();
            var graph = tree.graph;

            var repeater = Add<Repeater>(graph, 0.0f, 100.0f);
            var parallel = Add<ParallelSequence>(graph, 0.0f, 250.0f);
            var first = CallBranch(graph, branch, -400.0f, 400.0f);
            var second = CallBranch(graph, branch, 400.0f, 400.0f);

            Connect(graph, graph.EntryNode, repeater);
            Connect(graph, repeater, parallel);
            Connect(graph, parallel, first);
            Connect(graph, parallel, second);

            var machine = Spawn(tree);

            for (int frame = 0; frame < 4; frame++)
            {
                yield return null;
            }

            var recorder = machine.FlightRecorder;
            var callSites = BehaviorTreeExplainer.CallSitesFor(recorder, hold);

            Assert.AreEqual(2, callSites.Count,
                "One authored node, two running copies — the panel's call-site dropdown exists for exactly this.");

            foreach (var callSite in callSites)
            {
                Assert.AreNotEqual(BehaviorTreeCallSite.RootId, callSite, "Both copies ran inside a sub-tree.");

                var explanation = BehaviorTreeExplainer.Explain(recorder, callSite, hold);

                Assert.AreEqual(BehaviorTreeOutcome.Running, explanation.Outcome);
                Assert.IsNotEmpty(explanation.CallSitePath, "and each answer says which copy it is about, root down.");
            }
        }

        #region Building

        /// <summary>
        /// Entry -> Repeater -> Selector -> [ Attack (guarded on hasTarget), Idle ]. Both branches hold
        /// forever, so the only thing that moves execution is Attack's guard opening — which is what makes
        /// the Selector record a takeover rather than anything ending on its own.
        /// </summary>
        private static BehaviorTreeGraphAsset BuildZombieTree(out Guid attack, out Guid idle, out Guid attackGuard)
        {
            var asset = NewTree();
            var graph = asset.graph;

            var repeater = Add<Repeater>(graph, 0.0f, 100.0f);
            var selector = Add<Selector>(graph, 0.0f, 250.0f);
            var attackNode = Add<WaitTime>(graph, -900.0f, 400.0f);
            var idleNode = Add<WaitTime>(graph, 900.0f, 400.0f);

            Connect(graph, graph.EntryNode, repeater);
            Connect(graph, repeater, selector);
            Connect(graph, selector, attackNode);
            Connect(graph, selector, idleNode);

            FeedFloat(graph, attackNode, attackNode.Time, 999.0f);
            FeedFloat(graph, idleNode, idleNode.Time, 999.0f);

            var read = ReadAgentVariable(graph, "hasTarget", -600.0f, 0.0f);
            var guard = Add<BooleanReactiveGuard>(graph, -900.0f, 250.0f);

            guard.UpdateOwner(attackNode);
            read.Value.ValidlyConnectTo(guard.Value);
            guard.AddTrigger(GuardTrigger.KeyChanged("hasTarget"));

            attack = attackNode.guid;
            idle = idleNode.guid;
            attackGuard = guard.guid;

            return asset;
        }

        /// <summary>
        /// One guarded branch that never finishes on its own, so its guard is the only thing that can end it.
        ///
        /// <para>
        /// Deliberately no Repeater. With one, an aborted branch is retried the very next tick, the guard
        /// declines re-entry, and a skip becomes the node's <em>most recent</em> episode — so the explainer,
        /// correctly, answers "never entered" about the latest attempt instead of "aborted" about the one the
        /// test meant. Letting the tree end keeps the abort the end of the story.
        /// </para>
        /// </summary>
        private static BehaviorTreeGraphAsset BuildGuardedHold(out Guid hold, out Guid guardGuid, string watchKey)
        {
            var asset = NewTree();
            var graph = asset.graph;

            var wait = Add<WaitTime>(graph, 0.0f, 400.0f);

            Connect(graph, graph.EntryNode, wait);
            FeedFloat(graph, wait, wait.Time, 999.0f);

            var read = ReadAgentVariable(graph, watchKey, -600.0f, 0.0f);
            var guard = Add<BooleanReactiveGuard>(graph, 0.0f, 250.0f);

            guard.UpdateOwner(wait);
            read.Value.ValidlyConnectTo(guard.Value);
            guard.AddTrigger(GuardTrigger.KeyChanged(watchKey));

            hold = wait.guid;
            guardGuid = guard.guid;

            return asset;
        }

        private static BehaviorTreeGraphAsset BuildHoldingBranch(out Guid hold)
        {
            var asset = NewTree();
            var graph = asset.graph;

            var wait = Add<WaitTime>(graph, 0.0f, 100.0f);

            FeedFloat(graph, wait, wait.Time, 999.0f);
            Connect(graph, graph.EntryNode, wait);

            hold = wait.guid;

            return asset;
        }

        private static RunBehaviorTreeGraphNode CallBranch(
            BehaviorTreeGraph graph, BehaviorTreeGraphAsset branch, float x, float y)
        {
            var call = Add<RunBehaviorTreeGraphNode>(graph, x, y);

            call.SetBehaviorTreeGraphAsset(branch);
            call.RefreshParameters();

            return call;
        }

        #endregion
    }
}
