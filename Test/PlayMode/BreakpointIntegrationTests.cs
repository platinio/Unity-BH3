using System;
using System.Collections;
using System.Collections.Generic;
using ArcaneOnyx.BehaviorTree.Debugging;
using ArcaneOnyx.GraphCore;
using NUnit.Framework;
using Unity.VisualScripting;
using UnityEngine;
using UnityEngine.TestTools;

namespace ArcaneOnyx.BehaviorTree.Tests.PlayMode
{
    /// <summary>
    /// Breakpoints against a real machine ticking in a real player loop.
    ///
    /// <para>
    /// The edit-mode suite pins the matching <em>semantics</em> — masks, operators, cultures, break-on-hit —
    /// by driving a recorder by hand. What it cannot reach is the seam: that a breakpoint armed on an
    /// <em>authored</em> node guid catches the <em>clone</em> the machine actually runs (<c>Machine.Awake</c>
    /// does <c>Instantiate(nest.macro)</c>), that a live typed value survives the trip from
    /// <see cref="AgentVariableWriter"/> through the recorder to the matcher, and that a hit reaches the
    /// editor's responder, which pauses the editor. Nothing here is a new matching rule; every rule involved
    /// is already covered. What was not covered is that the pieces are connected.
    /// </para>
    ///
    /// <para>
    /// <b>The pause watchdog is load-bearing, not hygiene.</b> A hit makes the editor-side responder call
    /// <c>Debug.Break()</c>, which halts the player loop at the end of the frame — and a play-mode test
    /// coroutine lives in the player loop, so a test that let the pause stand would freeze itself and time the
    /// whole run out. <c>EditorApplication.update</c> keeps running while play mode is paused, so the fixture
    /// watches from there, counts the pause, and lifts it. The count is also the assertion surface: this
    /// assembly cannot reference <c>ArcaneOnyx.BehaviorTree.Editor</c>, so the responder is only observable
    /// here by what it does to the editor, which is the more honest test of it anyway.
    /// </para>
    /// </summary>
    public class BreakpointIntegrationTests : PlayModeAgentFixture
    {
        private readonly List<BehaviorTreeBreakpointHit> hits = new();
        private readonly List<GameObject> extraAgents = new();

#if UNITY_EDITOR
        private int editorPauses;
#endif

        [SetUp]
        public void ArmObservers()
        {
            // The domain reload on entering play mode ran the store's InitializeOnLoad, so whatever the
            // developer really has armed is in the runtime store right now. Cleared here and not restored by
            // this fixture: leaving play mode reloads the domain again, and the store loads the file back.
            BehaviorTreeBreakpoints.Clear();
            BehaviorTreeBreakpoints.GloballyEnabled = true;
            BehaviorTreeBreakpoints.AgentFilter = null;

            hits.Clear();
            BehaviorTreeBreakpoints.Hit += Record;

#if UNITY_EDITOR
            editorPauses = 0;
            UnityEditor.EditorApplication.update += LiftPause;
#endif
        }

        [TearDown]
        public void DisarmObservers()
        {
            BehaviorTreeBreakpoints.Hit -= Record;
            BehaviorTreeBreakpoints.Clear();
            BehaviorTreeBreakpoints.GloballyEnabled = true;
            BehaviorTreeBreakpoints.AgentFilter = null;

            foreach (var extra in extraAgents)
            {
                if (extra != null) UnityEngine.Object.DestroyImmediate(extra);
            }

            extraAgents.Clear();

#if UNITY_EDITOR
            UnityEditor.EditorApplication.update -= LiftPause;
            UnityEditor.EditorApplication.isPaused = false;
#endif
        }

        private void Record(BehaviorTreeBreakpointHit hit) => hits.Add(hit);

#if UNITY_EDITOR
        private void LiftPause()
        {
            if (!UnityEditor.EditorApplication.isPaused) return;

            editorPauses++;
            UnityEditor.EditorApplication.isPaused = false;
        }
#endif

        [UnityTest]
        public IEnumerator ABreakpointOnTheAuthoredNodeCatchesTheCloneTheMachineRuns()
        {
            // The seam Finding 1 makes worth testing: the machine runs a clone of the authored graph, and a
            // breakpoint armed before Spawn — on the authored node's guid — only works because guids survive
            // the Instantiate. A test arming after spawn against the running graph would pass even if they
            // did not.
            var tree = NewTree();
            var hold = Add<WaitTime>(tree.graph, 0.0f, 250.0f);

            Connect(tree.graph, tree.graph.EntryNode, hold);
            FeedFloat(tree.graph, hold, hold.Time, 999.0f);

            BehaviorTreeBreakpoints.SetNode(hold.guid, BehaviorTreeNodeBreakEvents.Enter);

            var machine = Spawn(tree);

            for (int frame = 0; frame < 5 && hits.Count == 0; frame++)
            {
                yield return null;
            }

            Assert.AreEqual(1, hits.Count, "The node entered once and holds for 999 seconds.");
            Assert.AreEqual(hold.guid, hits[0].SubjectGuid);
            Assert.AreEqual(BehaviorTreeEventKind.NodeEnter, hits[0].Cause.Kind);
            Assert.IsTrue(ReferenceEquals(hits[0].Recording, machine.FlightRecorder),
                "The hit must carry the recording of the machine that tripped it — the scrubber and the "
                + "why-inspector read everything from it.");
        }

#if UNITY_EDITOR
        [UnityTest]
        public IEnumerator AHitActuallyPausesTheEditorAndPlayCarriesOnOnceLifted()
        {
            // The responder end to end, observed from the only side this assembly can see: the editor really
            // pauses, and lifting the pause really hands the run back. If the responder's isPlaying gate, its
            // Debug.Break, or its Resume wiring broke, this is the test that notices.
            var tree = NewTree();
            var hold = Add<WaitTime>(tree.graph, 0.0f, 250.0f);

            Connect(tree.graph, tree.graph.EntryNode, hold);
            FeedFloat(tree.graph, hold, hold.Time, 999.0f);

            BehaviorTreeBreakpoints.SetNode(hold.guid, BehaviorTreeNodeBreakEvents.Enter);
            Spawn(tree);

            // The pause lands at the end of the frame the hit happened in, and the watchdog lifts it from the
            // editor loop — so from inside the player loop this reads as one slow yield, not a deadlock.
            for (int frame = 0; frame < 10 && editorPauses == 0; frame++)
            {
                yield return null;
            }

            Assert.AreEqual(1, hits.Count);
            Assert.GreaterOrEqual(editorPauses, 1, "A hit in play mode must stop the editor, or a breakpoint is a log line.");

            int frameWhenLifted = Time.frameCount;

            yield return null;
            yield return null;

            Assert.Greater(Time.frameCount, frameWhenLifted,
                "Unpausing must hand the run back rather than leaving the player loop stopped.");
        }
#endif

        [UnityTest]
        public IEnumerator ASensorWriteIsMatchedAgainstTheLiveTypedValue()
        {
            // The ordering operators only work because the live object rides along from the write to the
            // matcher. The edit-mode suite hands the recorder that object itself; this proves the real path —
            // AgentVariableWriter.SetOn through the recorder — actually threads it, on the kind of write the
            // docs call the commonest one worth breaking on: a sensor's, from outside the tree.
            var tree = NewTree();
            var hold = Add<WaitTime>(tree.graph, 0.0f, 250.0f);

            Connect(tree.graph, tree.graph.EntryNode, hold);
            FeedFloat(tree.graph, hold, hold.Time, 999.0f);

            var machine = Spawn(tree);
            yield return null;

            BehaviorTreeBreakpoints.SetVariable("ammo", "5", BehaviorTreeVariableCompare.LessThan);

            var sensor = AgentVariableWriter.On(machine.gameObject);

            sensor.Write("TestSensor", "ammo", 7);
            Assert.IsEmpty(hits, "7 is not below 5.");

            sensor.Write("TestSensor", "ammo", 3);

            Assert.AreEqual(1, hits.Count, "3 is, and the comparison is numeric on the live int — not on rendered text.");
            Assert.AreEqual(BehaviorTreeEventKind.VariableWrite, hits[0].Cause.Kind);
            Assert.AreEqual("3", hits[0].Cause.NewValue);
            Assert.AreEqual("TestSensor", hits[0].Cause.Writer, "The hit names the sensor, which is the answer to \"who wrote this\".");
        }

        [UnityTest]
        public IEnumerator AFactPublishedThroughSetOnIsRecordedAndFiresTheBreakpoint()
        {
            // The pin for a design decision: SetOn is the convenient static everything outside a graph
            // reaches for, and it used to set and version without recording — guards woke, and variable
            // breakpoints, the watch and the timeline were all silently blind to the write. Now the public
            // publish surface records, attributed "(external)" unless the caller names itself. If this test
            // starts failing because SetOn stopped recording, that blindness is back.
            var tree = NewTree();
            var hold = Add<WaitTime>(tree.graph, 0.0f, 250.0f);

            Connect(tree.graph, tree.graph.EntryNode, hold);
            FeedFloat(tree.graph, hold, hold.Time, 999.0f);

            var machine = Spawn(tree);
            yield return null;

            BehaviorTreeBreakpoints.SetVariable("alertLevel");

            AgentVariableWriter.SetOn(machine.gameObject, "alertLevel", 1);

            Assert.AreEqual(1, hits.Count, "A SetOn write is a recorded write, so an any-write breakpoint sees it.");
            Assert.AreEqual("(external)", hits[0].Cause.Writer, "An unnamed publisher is attributed as external rather than invisibly.");

            AgentVariableWriter.SetOn(machine.gameObject, "alertLevel", 2, "DemoDriver");

            Assert.AreEqual(2, hits.Count);
            Assert.AreEqual("DemoDriver", hits[1].Cause.Writer, "A caller that names itself is attributed by that name.");

            Assert.AreEqual(2, AgentVariableWriter.On(machine.gameObject).VersionOf("alertLevel"),
                "The version half is unchanged — both writes were changes, so guards watching the key still wake.");
        }

        [UnityTest]
        public IEnumerator ATreeNodeWritingAVariableFiresTheBreakpointAndNamesTheWriter()
        {
            // The in-tree half of the same seam: SetVariable.OnUpdate → GameplayNode.SaveVariable → the
            // recorder, with the boxed bool in hand. "true" matching a value that renders as "True" is the
            // edit-mode rule; that a designer's tree write reaches that rule at all is what only this run
            // shows. The subject must be the writing node — a VariableWrite leaves NodeGuid empty.
            var tree = NewTree();
            var repeater = Add<Repeater>(tree.graph, 0.0f, 100.0f);
            var write = Add<SetVariable>(tree.graph, 0.0f, 250.0f);

            SetPrivateField(write, "VariableKind", BehaviorTreeVariableKind.Object);
            FeedString(tree.graph, write, write.Key, "hasTarget");
            FeedBool(tree.graph, write, write.Value, true);

            Connect(tree.graph, tree.graph.EntryNode, repeater);
            Connect(tree.graph, repeater, write);

            BehaviorTreeBreakpoints.SetVariable("hasTarget", "true", BehaviorTreeVariableCompare.Equals);

            Spawn(tree, (_, variables) => variables.declarations.Set("hasTarget", false));

            for (int frame = 0; frame < 5 && hits.Count == 0; frame++)
            {
                yield return null;
            }

            Assert.GreaterOrEqual(hits.Count, 1);
            Assert.AreEqual(write.guid, hits[0].SubjectGuid,
                "The hit must point at the node that wrote — the question a variable breakpoint answers is \"who wrote this\".");
        }

        [UnityTest]
        public IEnumerator ARealGuardFlippingFiresOnlyTheArmedDirection()
        {
            // A guard breakpoint through the whole reactive machinery: scheduled on the key, woken by the
            // writer's version bump, recorded only on the transition. The guard's first answer is false, and
            // BecameTrue must sit through it.
            var tree = NewTree();
            var graph = tree.graph;

            var repeater = Add<Repeater>(graph, 0.0f, 100.0f);
            var selector = Add<Selector>(graph, 0.0f, 250.0f);
            var attack = Add<WaitTime>(graph, -400.0f, 400.0f);
            var idle = Add<WaitTime>(graph, 400.0f, 400.0f);

            Connect(graph, graph.EntryNode, repeater);
            Connect(graph, repeater, selector);
            Connect(graph, selector, attack);
            Connect(graph, selector, idle);

            FeedFloat(graph, attack, attack.Time, 999.0f);
            FeedFloat(graph, idle, idle.Time, 999.0f);

            var read = ReadAgentVariable(graph, "hasTarget", -600.0f, 0.0f);
            var guard = Add<BooleanReactiveGuard>(graph, -400.0f, 250.0f);
            guard.UpdateOwner(attack);
            read.Value.ValidlyConnectTo(guard.Value);
            guard.AddTrigger(GuardTrigger.KeyChanged("hasTarget"));

            BehaviorTreeBreakpoints.SetGuard(guard.guid, BehaviorTreeGuardBreakOn.BecameTrue);

            var machine = Spawn(tree, (_, variables) => variables.declarations.Set("hasTarget", false));

            for (int frame = 0; frame < 3; frame++)
            {
                yield return null;
            }

            Assert.IsEmpty(hits, "The guard answered false at entry, and only the rise to true is armed.");

            PublishFact(machine, "hasTarget", true);

            for (int frame = 0; frame < 5 && hits.Count == 0; frame++)
            {
                yield return null;
            }

            Assert.AreEqual(1, hits.Count);
            Assert.AreEqual(BehaviorTreeEventKind.GuardEval, hits[0].Cause.Kind);
            Assert.IsTrue(hits[0].Cause.Flag);
            Assert.AreEqual(guard.guid, hits[0].Cause.NodeGuid, "The subject of a guard hit is the guard, not the node it protects.");
        }

        [UnityTest]
        public IEnumerator BreakOnHitCountsPerAgentWithTwoAgentsTickingTogether()
        {
            // Two machines on one tree asset, both reaching the same node every frame — the shape "break on
            // hit #30" is actually used in: a crowd running one behaviour. With a single shared counter the
            // editor stops once the agents' *combined* count passes the number, which is neither agent's
            // third and is whichever one happened to tick first.
            var tree = NewTree();
            var repeater = Add<Repeater>(tree.graph, 0.0f, 100.0f);
            var write = Add<SetVariable>(tree.graph, 0.0f, 250.0f);

            SetPrivateField(write, "VariableKind", BehaviorTreeVariableKind.Object);
            FeedString(tree.graph, write, write.Key, "tick");
            FeedBool(tree.graph, write, write.Value, true);

            Connect(tree.graph, tree.graph.EntryNode, repeater);
            Connect(tree.graph, repeater, write);

            // Repeater restarts its child as soon as it completes and Set Variable completes on the tick it
            // runs, so the node is entered once per frame on each agent.
            var breakpoint = BehaviorTreeBreakpoints.SetNode(write.guid, BehaviorTreeNodeBreakEvents.Enter);
            BehaviorTreeBreakpoints.SetBreakOnHit(breakpoint, 3);

            var first = Spawn(tree, (_, variables) => variables.declarations.Set("tick", false));

            var secondAgent = new GameObject("SecondAgent");
            secondAgent.SetActive(false);
            extraAgents.Add(secondAgent);

            var second = secondAgent.AddComponent<BehaviorTreeMachine>();
            secondAgent.GetComponent<Variables>().declarations.Set("tick", false);
            second.nest.macro = tree;
            secondAgent.SetActive(true);

            for (int frame = 0; frame < 12; frame++)
            {
                yield return null;
            }

            var a = first.FlightRecorder;
            var b = second.FlightRecorder;

            Assert.Greater(breakpoint.MatchesFor(a), 0, "Both agents have to have reached it for this to prove anything.");
            Assert.Greater(breakpoint.MatchesFor(b), 0);
            Assert.AreEqual(2, breakpoint.AgentsMatched, "Two agents, two tallies.");

            AssertFiredOnItsOwnCount(breakpoint, a, "The first agent");
            AssertFiredOnItsOwnCount(breakpoint, b, "The second agent");
        }

        /// <summary>
        /// Fires from its own hit #N onwards and not before — which is exactly what a shared counter cannot
        /// satisfy for both agents at once, since one of them would be firing on matches the other made.
        /// </summary>
        private static void AssertFiredOnItsOwnCount(
            BehaviorTreeBreakpoint breakpoint, IBehaviorTreeRecording agent, string who)
        {
            int matches = breakpoint.MatchesFor(agent);
            int expected = matches >= breakpoint.BreakOnHit ? matches - breakpoint.BreakOnHit + 1 : 0;

            Assert.AreEqual(expected, breakpoint.HitsFor(agent),
                $"{who} matched {matches}× and must have fired {expected}× — from its own hit "
                + $"#{breakpoint.BreakOnHit} onwards, not from the moment the agents' combined count passed it.");
        }

        [UnityTest]
        public IEnumerator TheAgentFilterNarrowsRealAgentsToTheOneBeingDebugged()
        {
            // Two live machines on one tree asset — the forty-zombies case the filter exists for, with real
            // per-machine recorders rather than two hand-made ones. Both writes happen in this same frame,
            // between two yields: the editor-side debug-target tracker re-resolves the filter about every
            // quarter second from its own loop, so an assertion that let a frame boundary in here would be
            // racing it rather than testing the filter.
            var tree = NewTree();
            var hold = Add<WaitTime>(tree.graph, 0.0f, 250.0f);

            Connect(tree.graph, tree.graph.EntryNode, hold);
            FeedFloat(tree.graph, hold, hold.Time, 999.0f);

            var debugged = Spawn(tree);

            var second = new GameObject("Bystander");
            second.SetActive(false);
            extraAgents.Add(second);
            var bystander = second.AddComponent<BehaviorTreeMachine>();
            bystander.nest.macro = tree;
            second.SetActive(true);

            yield return null;

            BehaviorTreeBreakpoints.SetVariable("ping");
            BehaviorTreeBreakpoints.AgentFilter = debugged.FlightRecorder;

            AgentVariableWriter.On(bystander.gameObject).Write("TestSensor", "ping", 1);
            AgentVariableWriter.On(debugged.gameObject).Write("TestSensor", "ping", 1);

            Assert.AreEqual(1, hits.Count, "Only the debugged agent may stop the editor, however many run the tree.");
            Assert.IsTrue(ReferenceEquals(hits[0].Recording, debugged.FlightRecorder));
        }
    }
}
