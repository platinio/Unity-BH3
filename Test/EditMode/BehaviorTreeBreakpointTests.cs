using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using ArcaneOnyx.BehaviorTree.Debugging;
using ArcaneOnyx.GraphCore;
using NUnit.Framework;
using Unity.VisualScripting;
using UnityEngine;
using UnityEngine.TestTools;

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

            // The editor's own updater sets this from whatever agent is on screen, so a test run inside the
            // editor would otherwise inherit a filter pointing at nothing in this fixture.
            BehaviorTreeBreakpoints.AgentFilter = null;

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
            BehaviorTreeBreakpoints.AgentFilter = null;

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

            var guard = AddNode<BooleanReactiveGuard>(graph);
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
            var guard = new BooleanReactiveGuard();

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
            var guard = new BooleanReactiveGuard();

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
            var guard = new BooleanReactiveGuard();

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
            var guard = new BooleanReactiveGuard();

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

            recorder.VariableWrite(writer, "hasTarget", BehaviorTreeVariableKind.Object, false, true);
            recorder.VariableWrite(writer, "hasTarget", BehaviorTreeVariableKind.Object, true, false);

            Assert.AreEqual(2, hits.Count);
        }

        [Test]
        public void AValueMatchOnlyFiresOnThatValue()
        {
            var writer = BoundNode();
            BehaviorTreeBreakpoints.SetVariable("ammo", "0");

            recorder.VariableWrite(writer, "ammo", BehaviorTreeVariableKind.Object, 3, 2);
            recorder.VariableWrite(writer, "ammo", BehaviorTreeVariableKind.Object, 2, 1);

            Assert.IsEmpty(hits, "Neither write landed on the value asked for.");

            recorder.VariableWrite(writer, "ammo", BehaviorTreeVariableKind.Object, 1, 0);

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

            recorder.VariableWrite(writer, "hasTarget", BehaviorTreeVariableKind.Object, false, true);

            Assert.AreEqual(1, hits.Count);
            Assert.AreEqual(writer.guid, hits[0].SubjectGuid);
            Assert.AreNotEqual(Guid.Empty, hits[0].SubjectGuid);
        }

        [Test]
        public void EqualsComparesNumbersAsNumbersRatherThanAsRenderedText()
        {
            // The reason the typed value is threaded through to the matcher at all. Comparing renderings works
            // for "0" and breaks the moment a float is involved.
            var writer = BoundNode();
            BehaviorTreeBreakpoints.SetVariable("speed", "3.5", BehaviorTreeVariableCompare.Equals);

            recorder.VariableWrite(writer, "speed", BehaviorTreeVariableKind.Object, 0.0f, 3.5f);

            Assert.AreEqual(1, hits.Count);
        }

        [Test]
        public void ANumberIsMatchedWhicheverCultureTheDesignerTypedItIn()
        {
            // The watch panel renders values in the editor's culture, which is what someone copies from. Both
            // that rendering and the invariant form have to work, or the breakpoint silently never fires on
            // any machine whose decimal separator is a comma.
            var previous = CultureInfo.CurrentCulture;

            try
            {
                CultureInfo.CurrentCulture = new CultureInfo("de-DE");

                var writer = BoundNode();
                BehaviorTreeBreakpoints.SetVariable("speed", "3,5", BehaviorTreeVariableCompare.Equals);

                recorder.VariableWrite(writer, "speed", BehaviorTreeVariableKind.Object, 0.0f, 3.5f);

                Assert.AreEqual(1, hits.Count, "A comma-decimal machine renders 3.5 as \"3,5\".");
            }
            finally
            {
                CultureInfo.CurrentCulture = previous;
            }
        }

        [Test]
        public void ABooleanMatchesWhateverCaseTheDesignerTyped()
        {
            // bool.ToString() is "True", so an ordinal compare against what anyone actually types never fires.
            var writer = BoundNode();
            BehaviorTreeBreakpoints.SetVariable("hasTarget", "true", BehaviorTreeVariableCompare.Equals);

            recorder.VariableWrite(writer, "hasTarget", BehaviorTreeVariableKind.Object, false, true);

            Assert.AreEqual(1, hits.Count);
        }

        [Test]
        public void NotEqualsFiresOnEverythingElse()
        {
            var writer = BoundNode();
            BehaviorTreeBreakpoints.SetVariable("ammo", "0", BehaviorTreeVariableCompare.NotEquals);

            recorder.VariableWrite(writer, "ammo", BehaviorTreeVariableKind.Object, 3, 2);
            Assert.AreEqual(1, hits.Count);

            recorder.VariableWrite(writer, "ammo", BehaviorTreeVariableKind.Object, 2, 0);
            Assert.AreEqual(1, hits.Count, "Landing on the excluded value must not fire.");
        }

        [TestCase(BehaviorTreeVariableCompare.LessThan, 4, true)]
        [TestCase(BehaviorTreeVariableCompare.LessThan, 5, false)]
        [TestCase(BehaviorTreeVariableCompare.LessOrEqual, 5, true)]
        [TestCase(BehaviorTreeVariableCompare.GreaterThan, 6, true)]
        [TestCase(BehaviorTreeVariableCompare.GreaterThan, 5, false)]
        [TestCase(BehaviorTreeVariableCompare.GreaterOrEqual, 5, true)]
        public void OrderingOperatorsCompareNumerically(
            BehaviorTreeVariableCompare compare, int written, bool shouldFire)
        {
            var writer = BoundNode();
            BehaviorTreeBreakpoints.SetVariable("hp", "5", compare);

            recorder.VariableWrite(writer, "hp", BehaviorTreeVariableKind.Object, 100, written);

            Assert.AreEqual(shouldFire ? 1 : 0, hits.Count);
        }

        [Test]
        public void AnOrderingOperatorOnANonNumberSaysSoRatherThanFiringOrGoingQuiet()
        {
            // The failure this component exists to remove, applied to itself: a breakpoint that cannot match
            // must not be indistinguishable from a breakpoint the program never reached.
            var writer = BoundNode();
            var breakpoint = BehaviorTreeBreakpoints.SetVariable("target", "5", BehaviorTreeVariableCompare.LessThan);

            recorder.VariableWrite(writer, "target", BehaviorTreeVariableKind.Object, null, "Zombie");

            Assert.IsEmpty(hits, "'Zombie' is not a number, so nothing should fire.");
            Assert.IsNotNull(breakpoint.Diagnostic, "…but it has to say why.");
            StringAssert.Contains("not a number", breakpoint.Diagnostic);
        }

        [Test]
        public void ContainsMatchesPartOfARenderedValue()
        {
            var writer = BoundNode();
            BehaviorTreeBreakpoints.SetVariable("state", "Attack", BehaviorTreeVariableCompare.Contains);

            recorder.VariableWrite(writer, "state", BehaviorTreeVariableKind.Object, "Idle", "AttackMelee");

            Assert.AreEqual(1, hits.Count);

            recorder.VariableWrite(writer, "state", BehaviorTreeVariableKind.Object, "AttackMelee", "Flee");

            Assert.AreEqual(1, hits.Count);
        }

        [Test]
        public void ContainsOnANumberStillMatchesButWarnsThatItIsMatchingText()
        {
            // The mirror of the ordering case, and the more dangerous one. Ordering on a string cannot match,
            // which is at least quiet; contains on a number matches *more* than it looks like it should, and a
            // breakpoint that fires too often is harder to spot than one that never fires.
            var writer = BoundNode();
            var breakpoint = BehaviorTreeBreakpoints.SetVariable("alertLevel", "1", BehaviorTreeVariableCompare.Contains);

            recorder.VariableWrite(writer, "alertLevel", BehaviorTreeVariableKind.Object, 0, 1);
            Assert.AreEqual(1, hits.Count);

            recorder.VariableWrite(writer, "alertLevel", BehaviorTreeVariableKind.Object, 9, 10);
            Assert.AreEqual(2, hits.Count, "10 contains the text \"1\", which is the trap.");

            Assert.IsNotNull(breakpoint.Diagnostic);
            StringAssert.Contains("matches its text", breakpoint.Diagnostic);
        }

        [Test]
        public void AnEmptyExpectedValueIsAnyWriteWhateverOperatorWasAskedFor()
        {
            // An operator with nothing to compare against cannot mean anything, and silently keeping it would
            // produce a breakpoint whose description reads "ammo < " .
            var writer = BoundNode();
            var breakpoint = BehaviorTreeBreakpoints.SetVariable("ammo", string.Empty, BehaviorTreeVariableCompare.LessThan);

            Assert.AreEqual(BehaviorTreeVariableCompare.Changed, breakpoint.Compare);

            recorder.VariableWrite(writer, "ammo", BehaviorTreeVariableKind.Object, 3, 2);

            Assert.AreEqual(1, hits.Count);
        }

        #endregion

        #region Break on the Nth hit

        [Test]
        public void BreakOnHitSkipsTheEarlierMatchesButStillCountsThem()
        {
            var node = BoundNode();
            var breakpoint = BehaviorTreeBreakpoints.SetNode(node.guid, BehaviorTreeNodeBreakEvents.Enter);

            BehaviorTreeBreakpoints.SetBreakOnHit(breakpoint, 3);

            node.OnNodeEnter();
            node.OnNodeEnter();

            Assert.IsEmpty(hits, "The first two matches were asked to be skipped.");
            Assert.AreEqual(2, breakpoint.MatchCount, "…but they are counted, or the row looks broken.");
            Assert.AreEqual(0, breakpoint.HitCount);

            node.OnNodeEnter();

            Assert.AreEqual(1, hits.Count);
            Assert.AreEqual(3, breakpoint.MatchCount);
            Assert.AreEqual(1, breakpoint.HitCount);

            node.OnNodeEnter();

            Assert.AreEqual(2, hits.Count, "Nth and after, not only the Nth.");
        }

        [Test]
        public void BreakOnHitAppliesToAGuardToo()
        {
            // The oscillating guard is the motivating case: it is the one that produces dozens of identical
            // hits, and it is a guard rather than a variable.
            var owner = BoundNode();
            var guard = new BooleanReactiveGuard();

            var breakpoint = BehaviorTreeBreakpoints.SetGuard(guard.guid, BehaviorTreeGuardBreakOn.EitherWay);
            BehaviorTreeBreakpoints.SetBreakOnHit(breakpoint, 4);

            for (int i = 0; i < 6; i++)
            {
                recorder.BeginTick();
                recorder.GuardEval(owner, guard, i % 2 == 0);
            }

            Assert.AreEqual(3, hits.Count, "Six flips, the first three skipped.");
        }

        [Test]
        public void AZeroOrNegativeBreakOnHitReadsAsTheFirst()
        {
            var node = BoundNode();
            var breakpoint = BehaviorTreeBreakpoints.SetNode(node.guid, BehaviorTreeNodeBreakEvents.Enter);

            BehaviorTreeBreakpoints.SetBreakOnHit(breakpoint, 0);
            Assert.AreEqual(1, breakpoint.BreakOnHit);

            node.OnNodeEnter();

            Assert.AreEqual(1, hits.Count);
        }

        [Test]
        public void BreakOnHitCountsPerAgentRatherThanAcrossAllOfThem()
        {
            // A breakpoint matches every agent running the tree, deliberately. With one shared counter that
            // makes "break on hit #3" a race: forty zombies reach the combined third almost immediately, and
            // the editor stops on whichever got there first rather than on the third time the one you are
            // watching did.
            var other = new BehaviorTreeFlightRecorder("OtherAgent", "TestTree");
            var node = BoundNode();

            var breakpoint = BehaviorTreeBreakpoints.SetNode(node.guid, BehaviorTreeNodeBreakEvents.Enter);
            BehaviorTreeBreakpoints.SetBreakOnHit(breakpoint, 3);

            // One node, two agents, alternating — the same guid reported by two recordings, which is what a
            // shared branch running on two agents actually looks like to the matcher.
            for (int pass = 0; pass < 2; pass++)
            {
                node.SetFlightRecorder(recorder);
                node.OnNodeEnter();

                node.SetFlightRecorder(other);
                node.OnNodeEnter();
            }

            Assert.IsEmpty(hits, "Four entries, but only two per agent — neither has reached its own third.");
            Assert.AreEqual(4, breakpoint.MatchCount, "The total still counts every agent's matches.");
            Assert.AreEqual(2, breakpoint.AgentsMatched);

            node.SetFlightRecorder(recorder);
            node.OnNodeEnter();

            Assert.AreEqual(1, hits.Count, "This agent's third is what fires, not the combined fifth.");
            Assert.AreEqual(1, breakpoint.HitsFor(recorder));
            Assert.AreEqual(0, breakpoint.HitsFor(other), "The other agent is still one short of its own third.");
        }

        [Test]
        public void OneAgentsMatchesAreNotAHeadStartForTheNext()
        {
            // The case the agent filter alone does not cover. With the filter set only one agent's events
            // reach the counter, so a shared count looks per-agent — until you select a different agent, and
            // its "hit #3" is already two thirds counted by the zombie you were watching before.
            var other = new BehaviorTreeFlightRecorder("OtherAgent", "TestTree");
            var node = BoundNode();

            var breakpoint = BehaviorTreeBreakpoints.SetNode(node.guid, BehaviorTreeNodeBreakEvents.Enter);
            BehaviorTreeBreakpoints.SetBreakOnHit(breakpoint, 3);

            BehaviorTreeBreakpoints.AgentFilter = recorder;

            node.OnNodeEnter();
            node.OnNodeEnter();

            Assert.IsEmpty(hits, "Two of the three the watched agent was asked for.");

            // Attention moves to the other agent, the way selecting one in the hierarchy does.
            node.SetFlightRecorder(other);
            BehaviorTreeBreakpoints.AgentFilter = other;

            node.OnNodeEnter();

            Assert.IsEmpty(hits, "The newly watched agent has reached this once, not three times.");
            Assert.AreEqual(1, breakpoint.MatchesFor(other));
            Assert.AreEqual(2, breakpoint.MatchesFor(recorder), "and the first agent keeps its own count.");
        }

        [Test]
        public void ADestroyedAgentTakesItsTallyWithIt()
        {
            // Two reasons, and the second is why this is not merely tidy: a count that outlived its agent
            // would still be sitting in the panel's total, and the tally is keyed by the recording — so
            // holding it would keep that agent's whole event ring alive for the rest of the session.
            var node = BoundNode();
            var breakpoint = BehaviorTreeBreakpoints.SetNode(node.guid, BehaviorTreeNodeBreakEvents.Enter);

            node.OnNodeEnter();

            Assert.AreEqual(1, breakpoint.HitsFor(recorder));
            Assert.AreEqual(1, breakpoint.AgentsMatched);

            BehaviorTreeFlightRecorders.Unregister(recorder);

            Assert.AreEqual(0, breakpoint.HitsFor(recorder), "A destroyed agent's count describes a run that is over.");
            Assert.AreEqual(0, breakpoint.AgentsMatched, "and its recording must not be held as a dictionary key.");
            Assert.AreEqual(0, breakpoint.HitCount, "so the total stops counting it too.");
        }

        [Test]
        public void ResettingTheRecordersDropsEveryTally()
        {
            // Reset drops every recorder at once — a domain reload, or the next fixture starting clean — and
            // the tallies keyed by those recordings have to go with them for the same reason Unregister's do.
            // Registering by hand because that is what BehaviorTreeRecorder.Attach does for a real agent; an
            // edit-mode recorder is built directly and would otherwise never be in the list Reset walks.
            BehaviorTreeFlightRecorders.Register(recorder);

            var node = BoundNode();
            var breakpoint = BehaviorTreeBreakpoints.SetNode(node.guid, BehaviorTreeNodeBreakEvents.Enter);

            node.OnNodeEnter();

            Assert.AreEqual(1, breakpoint.AgentsMatched, "The tally has to exist for dropping it to prove anything.");

            BehaviorTreeFlightRecorders.Reset();

            Assert.AreEqual(0, breakpoint.AgentsMatched, "A recorder dropped wholesale takes its tally with it.");
            Assert.AreEqual(0, breakpoint.HitCount);
        }

        [Test]
        public void PeakMatchesIsTheClosestAnySingleAgentHasCome()
        {
            // What "how near am I to hit #N" means when several agents are counting separately and no one of
            // them speaks for the row. The total would answer a question nobody asked.
            var other = new BehaviorTreeFlightRecorder("OtherAgent", "TestTree");
            var node = BoundNode();

            var breakpoint = BehaviorTreeBreakpoints.SetNode(node.guid, BehaviorTreeNodeBreakEvents.Enter);
            BehaviorTreeBreakpoints.SetBreakOnHit(breakpoint, 10);

            node.OnNodeEnter();
            node.OnNodeEnter();
            node.OnNodeEnter();

            node.SetFlightRecorder(other);
            node.OnNodeEnter();

            Assert.AreEqual(4, breakpoint.MatchCount);
            Assert.AreEqual(3, breakpoint.PeakMatches, "Three is how close anyone has come, not four.");
        }

        [Test]
        public void ResetClearsTheMatchCountAndTheDiagnostic()
        {
            var writer = BoundNode();
            var breakpoint = BehaviorTreeBreakpoints.SetVariable("target", "5", BehaviorTreeVariableCompare.LessThan);

            recorder.VariableWrite(writer, "target", BehaviorTreeVariableKind.Object, null, "Zombie");
            Assert.IsNotNull(breakpoint.Diagnostic);

            BehaviorTreeBreakpoints.ResetHitCounts();

            Assert.IsNull(breakpoint.Diagnostic, "It describes a run that is over.");
            Assert.AreEqual(0, breakpoint.MatchCount);
        }

        #endregion

        #region Variable breakpoints, continued

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
        public void TheAgentFilterNarrowsBreakpointsToOneAgent()
        {
            // A breakpoint is armed on a node, and a node belongs to a tree that forty agents may be running.
            // Without the filter, arming one stops the editor for whichever agent reaches it first.
            var other = new BehaviorTreeFlightRecorder("OtherAgent", "TestTree");

            var mine = BoundNode();
            var theirs = new ScriptedNode(ExecutionStatus.Success);
            theirs.SetFlightRecorder(other);

            // Same guid on both, which is the case that matters: one tree, two agents.
            BehaviorTreeBreakpoints.SetNode(mine.guid, BehaviorTreeNodeBreakEvents.Enter);
            BehaviorTreeBreakpoints.SetNode(theirs.guid, BehaviorTreeNodeBreakEvents.Enter);

            BehaviorTreeBreakpoints.AgentFilter = recorder;

            theirs.OnNodeEnter();
            Assert.IsEmpty(hits, "The other agent is not the one being debugged.");

            mine.OnNodeEnter();
            Assert.AreEqual(1, hits.Count);
            Assert.AreEqual("TestAgent", hits[0].AgentName);
        }

        [Test]
        public void NoAgentFilterMeansEveryAgentFires()
        {
            // Null is "the debugger could not say which agent is meant", and then filtering to nothing would
            // make breakpoints silently stop working whenever no tree happened to be open.
            var other = new BehaviorTreeFlightRecorder("OtherAgent", "TestTree");

            var theirs = new ScriptedNode(ExecutionStatus.Success);
            theirs.SetFlightRecorder(other);

            BehaviorTreeBreakpoints.SetNode(theirs.guid, BehaviorTreeNodeBreakEvents.Enter);
            BehaviorTreeBreakpoints.AgentFilter = null;

            theirs.OnNodeEnter();

            Assert.AreEqual(1, hits.Count);
        }

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
        public void ReadingARecordingBackNeverFiresABreakpoint()
        {
            // Scrubbing the timeline while the editor is paused must not trip anything. In practice it cannot:
            // breakpoints are evaluated inside the recorder, the recorder is only written to while the tree
            // ticks, and the tree only ticks from Update — which Unity does not run while paused. But that is
            // three separate facts holding hands, and the one this test can actually pin is the load-bearing
            // one: replaying a recording is a pure read. A future scrubbing path that re-pulled a port or
            // re-entered the recorder would break it silently, and the symptom would be an editor that pauses
            // while you are studying why it paused.
            var node = BoundNode(ExecutionStatus.Running);
            var guard = new BooleanReactiveGuard();
            var writer = BoundNode();

            // Produce a recording worth replaying, with breakpoints armed the whole time.
            BehaviorTreeBreakpoints.SetNode(node.guid, BehaviorTreeNodeBreakEvents.All);
            BehaviorTreeBreakpoints.SetGuard(guard.guid, BehaviorTreeGuardBreakOn.EitherWay);
            BehaviorTreeBreakpoints.SetVariable("ammo");

            for (int tick = 0; tick < 5; tick++)
            {
                recorder.BeginTick();
                node.OnNodeEnter();
                recorder.GuardEval(node, guard, tick % 2 == 0);
                recorder.VariableWrite(writer, "ammo", BehaviorTreeVariableKind.Object, tick, tick + 1);
                node.OnNodeExit();
            }

            var duringTheRun = hits.Count;
            Assert.Greater(duringTheRun, 0, "The recording has to be worth replaying for this to prove anything.");

            hits.Clear();

            // Everything the scrubber and the panels do to a recording, with nothing running.
            var timeline = BehaviorTreeTimeline.Build(recorder);

            foreach (var changeTick in timeline.ChangeTicks)
            {
                BehaviorTreeTreeState.At(recorder, timeline, changeTick);
            }

            BehaviorTreeVariableWatch.At(recorder, -1);
            BehaviorTreeExplainer.Explain(recorder, BehaviorTreeCallSite.RootId, node.guid, null, -1);

            Assert.IsEmpty(hits, "Replaying a recording is a read. Nothing about it may fire a breakpoint.");
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
        public void TheOperatorAndTheHitCountSurviveTheRoundTrip()
        {
            var guid = Guid.NewGuid();

            BehaviorTreeBreakpoints.SetVariable("hp", "5", BehaviorTreeVariableCompare.LessThan);
            BehaviorTreeBreakpoints.SetBreakOnHit(BehaviorTreeBreakpoints.SetNode(guid, BehaviorTreeNodeBreakEvents.Enter), 7);

            BehaviorTreeBreakpointStore.Save();
            BehaviorTreeBreakpointStore.Load();

            var variable = BehaviorTreeBreakpoints.ForVariable("hp");
            Assert.AreEqual(BehaviorTreeVariableCompare.LessThan, variable.Compare);
            Assert.AreEqual("5", variable.ExpectedValue);

            Assert.AreEqual(7, BehaviorTreeBreakpoints.ForNode(guid).BreakOnHit);
        }

        [Test]
        public void AVersionOneFileKeepsMeaningWhatItMeant()
        {
            // Written before `compare` existed. Reading a missing operator as the enum's default — Changed —
            // would turn "break when ammo is 0" into "break on every ammo write", which is a breakpoint that
            // fires constantly rather than one that does not fire: the loud failure, not the quiet one, but a
            // wrong answer either way.
            File.WriteAllText(breakpointFile, @"{
    ""version"": 1,
    ""breakpoints"": [
        { ""kind"": ""Variable"", ""variableKey"": ""ammo"", ""expectedValue"": ""0"", ""enabled"": true },
        { ""kind"": ""Variable"", ""variableKey"": ""alertLevel"", ""expectedValue"": """", ""enabled"": true }
    ]
}");

            BehaviorTreeBreakpointStore.Load();

            var withValue = BehaviorTreeBreakpoints.ForVariable("ammo");
            Assert.AreEqual(BehaviorTreeVariableCompare.Equals, withValue.Compare);
            Assert.AreEqual("0", withValue.ExpectedValue);
            Assert.AreEqual(1, withValue.BreakOnHit, "No breakOnHit in the file reads as the first hit.");

            Assert.AreEqual(
                BehaviorTreeVariableCompare.Changed,
                BehaviorTreeBreakpoints.ForVariable("alertLevel").Compare,
                "No value means any write, which is what it meant before the operator existed.");
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

        #region Toggling moments

        // The runtime store's SetNode replaces the whole mask, so "flip one moment" exists only inside
        // BehaviorTreeBreakpointStore.ToggleNodeEvent. These pin its three cases — no breakpoint yet, other
        // moments armed, last moment removed — through the editor store, where the toggle actually lives.

        [Test]
        public void TogglingAMomentOnAnUnarmedNodeArmsJustThatMomentAndSavesIt()
        {
            var node = new ScriptedNode(ExecutionStatus.Success);

            BehaviorTreeBreakpointStore.ToggleNodeEvent(node, BehaviorTreeNodeBreakEvents.Exit);

            Assert.AreEqual(BehaviorTreeNodeBreakEvents.Exit, BehaviorTreeBreakpoints.ForNode(node.guid).Events);

            // Arming through the store rather than the runtime is the promise that persisting cannot be
            // forgotten, so a toggle that does not reach the file is a broken toggle even if the mask is right.
            BehaviorTreeBreakpointStore.Load();

            Assert.AreEqual(BehaviorTreeNodeBreakEvents.Exit, BehaviorTreeBreakpoints.ForNode(node.guid).Events);
        }

        [Test]
        public void TogglingAnArmedMomentOffLeavesTheOthersArmed()
        {
            var node = new ScriptedNode(ExecutionStatus.Success);

            BehaviorTreeBreakpointStore.SetNode(
                node, BehaviorTreeNodeBreakEvents.Enter | BehaviorTreeNodeBreakEvents.Aborted);

            BehaviorTreeBreakpointStore.ToggleNodeEvent(node, BehaviorTreeNodeBreakEvents.Aborted);

            Assert.AreEqual(BehaviorTreeNodeBreakEvents.Enter, BehaviorTreeBreakpoints.ForNode(node.guid).Events);
        }

        [Test]
        public void TogglingTheOnlyArmedMomentOffRemovesTheBreakpoint()
        {
            var node = new ScriptedNode(ExecutionStatus.Success);

            BehaviorTreeBreakpointStore.ToggleNodeEvent(node, BehaviorTreeNodeBreakEvents.Enter);
            Assert.IsNotNull(BehaviorTreeBreakpoints.ForNode(node.guid));

            BehaviorTreeBreakpointStore.ToggleNodeEvent(node, BehaviorTreeNodeBreakEvents.Enter);

            Assert.IsNull(BehaviorTreeBreakpoints.ForNode(node.guid));
            Assert.IsEmpty(BehaviorTreeBreakpoints.All, "Toggling the last moment off is disarming, not an empty mask.");
        }

        [Test]
        public void TogglingAMomentTwiceLandsWhereItStarted()
        {
            var node = new ScriptedNode(ExecutionStatus.Success);
            BehaviorTreeBreakpointStore.SetNode(node, BehaviorTreeNodeBreakEvents.Enter);

            BehaviorTreeBreakpointStore.ToggleNodeEvent(node, BehaviorTreeNodeBreakEvents.Exit);
            BehaviorTreeBreakpointStore.ToggleNodeEvent(node, BehaviorTreeNodeBreakEvents.Exit);

            Assert.AreEqual(BehaviorTreeNodeBreakEvents.Enter, BehaviorTreeBreakpoints.ForNode(node.guid).Events);
        }

        [Test]
        public void TogglingANullNodeDoesNothing()
        {
            Assert.DoesNotThrow(() => BehaviorTreeBreakpointStore.ToggleNodeEvent(null, BehaviorTreeNodeBreakEvents.Enter));
            Assert.IsEmpty(BehaviorTreeBreakpoints.All);
        }

        #endregion

        #region Play transition

        /// <summary>
        /// Entering play mode zeroes every counter. Today the domain reload does most of the work and the
        /// store's EnteredPlayMode hook is belt and braces; the observable contract is what this pins, so
        /// turning domain reload off later cannot quietly bring last session's counts back.
        ///
        /// <para>
        /// After the reload the store has re-run its InitializeOnLoad and loaded whatever the developer's real
        /// file holds, so the assertion sweeps <see cref="BehaviorTreeBreakpoints.All"/> rather than looking
        /// for the one armed above — which no longer exists, and whose absence is not what is under test.
        /// </para>
        /// </summary>
        [UnityTest]
        public IEnumerator EnteringPlayModeZeroesEveryHitCount()
        {
            var node = BoundNode();
            BehaviorTreeBreakpoints.SetNode(node.guid, BehaviorTreeNodeBreakEvents.Enter);

            node.OnNodeEnter();
            Assert.AreEqual(1, BehaviorTreeBreakpoints.ForNode(node.guid).HitCount, "The count has to be nonzero for the reset to prove anything.");

            yield return new EnterPlayMode();
            yield return null;

            foreach (var breakpoint in BehaviorTreeBreakpoints.All)
            {
                Assert.AreEqual(0, breakpoint.HitCount,
                    $"{breakpoint.Describe()} carries a hit count from before play began — a count about a run that does not exist yet.");
                Assert.AreEqual(0, breakpoint.MatchCount, $"{breakpoint.Describe()} carries a stale match count.");
            }

            yield return new ExitPlayMode();
        }

        #endregion
    }
}
