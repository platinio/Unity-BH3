using System;
using System.Collections.Generic;
using System.Linq;
using ArcaneOnyx.BehaviorTree.Debugging;
using ArcaneOnyx.GraphCore;
using NUnit.Framework;
using Unity.VisualScripting;

namespace ArcaneOnyx.BehaviorTree.Tests
{
    /// <summary>
    /// The variable watch (spec 01, Component 4): what every variable held at one tick, and who wrote it.
    ///
    /// <para>
    /// Two things are worth knowing before reading these. First, the model answers for a <b>tick</b>, never
    /// for the present — most of what could go wrong here is a value leaking backwards or forwards in time,
    /// so several tests park a vantage point mid-recording and assert the future is invisible. Second, scope
    /// is <see cref="VariableKind"/> first and call site only within
    /// <see cref="VariableKind.Graph"/>; the call site alone is where the write came <i>from</i>, which is a
    /// different question and the reason the kind had to be recorded at all.
    /// </para>
    /// </summary>
    [TestFixture]
    public class BehaviorTreeVariableWatchTests
    {
        private static readonly Guid Sensor = new("11111111-1111-1111-1111-111111111111");
        private static readonly Guid Writer = new("22222222-2222-2222-2222-222222222222");

        #region Fixtures

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

            /// <summary>A write by a node in the tree.</summary>
            public RecordingBuilder Write(
                string key, string from, string to, VariableKind kind, int callSite = 0, Guid? writer = null)
            {
                events.Add(BehaviorTreeEvent.Create(
                    BehaviorTreeEventKind.VariableWrite, tick, sequence++, tick, tick * 0.02f, callSite,
                    Guid.Empty, writer ?? Writer, ExecutionStatus.None, false, key, from, to, kind));

                return this;
            }

            /// <summary>A write by a sensor outside the tree, which has a name rather than a guid.</summary>
            public RecordingBuilder ExternalWrite(string key, string from, string to, string writer)
            {
                events.Add(BehaviorTreeEvent.Create(
                    BehaviorTreeEventKind.VariableWrite, tick, sequence++, tick, tick * 0.02f,
                    BehaviorTreeCallSite.RootId, Guid.Empty, Guid.Empty, ExecutionStatus.None, false,
                    key, from, to, VariableKind.Object, writer));

                return this;
            }

            /// <summary>Anything that is not a write, to prove the watch ignores it.</summary>
            public RecordingBuilder Enter(Guid node, int callSite = 0)
            {
                events.Add(BehaviorTreeEvent.Create(
                    BehaviorTreeEventKind.NodeEnter, tick, sequence++, tick, tick * 0.02f, callSite, node));

                return this;
            }

            public BehaviorTreeRecordingSnapshot Build() =>
                new("Zombie", "ZombieTree", tick, events, callSites, 0, new List<GuardTrace>());
        }

        private sealed class StubTopology : IBehaviorTreeTopology
        {
            private readonly Dictionary<Guid, BehaviorTreeNodeInfo> nodes = new();

            public StubTopology Node(Guid guid, string name)
            {
                nodes[guid] = new BehaviorTreeNodeInfo(guid, name, "ScriptedNode", Guid.Empty, Array.Empty<Guid>());
                return this;
            }

            public bool TryGetNode(Guid guid, out BehaviorTreeNodeInfo node) => nodes.TryGetValue(guid, out node);

            public bool TryGetGuardReads(Guid guard, out IReadOnlyList<string> keys)
            {
                keys = Array.Empty<string>();
                return false;
            }
        }

        private static BehaviorTreeVariableWatchScope ScopeOf(
            BehaviorTreeVariableWatch watch, VariableKind kind, int callSite = -1)
        {
            return watch.Scopes.FirstOrDefault(s => s.Kind == kind && (kind != VariableKind.Graph || s.CallSiteId == callSite));
        }

        private static BehaviorTreeVariableWatchRow RowOf(BehaviorTreeVariableWatchScope scope, string key)
        {
            return scope?.Rows.FirstOrDefault(r => r.Key == key);
        }

        #endregion

        #region Values at a tick

        [Test]
        public void AValueIsTheOneItHeldAtTheVantageTickNotTheLatest()
        {
            var recording = new RecordingBuilder()
                .At(10).Write("alertLevel", "0", "1", VariableKind.Object)
                .At(20).Write("alertLevel", "1", "2", VariableKind.Object)
                .At(30).Write("alertLevel", "2", "3", VariableKind.Object)
                .Build();

            var watch = BehaviorTreeVariableWatch.At(recording, 20);

            Assert.AreEqual("2", RowOf(ScopeOf(watch, VariableKind.Object), "alertLevel").Value,
                "Parked at tick 20, the table must show what tick 20 knew — the tick-30 write has not happened yet.");
        }

        [Test]
        public void AVariableFirstWrittenAfterTheVantageTickIsAbsent()
        {
            var recording = new RecordingBuilder()
                .At(10).Write("hasTarget", "null", "True", VariableKind.Object)
                .At(40).Write("lastKnownPos", "null", "(1.0, 0.0, 2.0)", VariableKind.Object)
                .Build();

            var watch = BehaviorTreeVariableWatch.At(recording, 20);

            Assert.IsNull(RowOf(ScopeOf(watch, VariableKind.Object), "lastKnownPos"),
                "A row for a variable whose first write is in the future would leak the future into the past.");
            Assert.IsNotNull(RowOf(ScopeOf(watch, VariableKind.Object), "hasTarget"));
        }

        [Test]
        public void ANegativeTickMeansTheEndOfTheRecording()
        {
            var recording = new RecordingBuilder()
                .At(10).Write("alertLevel", "0", "1", VariableKind.Object)
                .At(30).Write("alertLevel", "1", "9", VariableKind.Object)
                .Build();

            var watch = BehaviorTreeVariableWatch.At(recording);

            Assert.AreEqual(30, watch.Tick, "The default vantage point matches Explain's: the end of the recording.");
            Assert.AreEqual("9", RowOf(ScopeOf(watch, VariableKind.Object), "alertLevel").Value);
        }

        [Test]
        public void EventsThatAreNotWritesAreIgnored()
        {
            var recording = new RecordingBuilder()
                .At(5).Enter(Writer)
                .At(6).Write("hasTarget", "null", "True", VariableKind.Object)
                .Build();

            var watch = BehaviorTreeVariableWatch.At(recording);

            Assert.AreEqual(1, watch.Scopes.Sum(s => s.Rows.Count),
                "Only VariableWrite events describe a variable; a NodeEnter carries no key and must not make a row.");
        }

        #endregion

        #region Scopes

        [Test]
        public void AnObjectWriteFromInsideABranchBelongsToTheAgentNotTheBranch()
        {
            // The reason VariableKind is recorded at all. The write was made from call site 3, but it landed
            // on the agent's Variables component, and filing it under Combat would show agent-wide state as
            // that branch's private scratch.
            var recording = new RecordingBuilder()
                .CallSite(3, 0, "Combat")
                .At(10).Write("alertLevel", "0", "1", VariableKind.Object, callSite: 3)
                .Build();

            var watch = BehaviorTreeVariableWatch.At(recording);

            Assert.IsNotNull(RowOf(ScopeOf(watch, VariableKind.Object), "alertLevel"),
                "An Object write belongs to the agent scope whatever branch it was made from.");
            Assert.IsNull(ScopeOf(watch, VariableKind.Graph, 3),
                "No Graph scope should exist — nothing wrote branch-local state.");
        }

        [Test]
        public void GraphWritesFromTwoCallSitesOfOneAssetStaySeparate()
        {
            var recording = new RecordingBuilder()
                .CallSite(1, 0, "Attack")
                .CallSite(2, 0, "Attack")
                .At(10).Write("attempts", "0", "1", VariableKind.Graph, callSite: 1)
                .At(11).Write("attempts", "0", "7", VariableKind.Graph, callSite: 2)
                .Build();

            var watch = BehaviorTreeVariableWatch.At(recording);

            Assert.AreEqual("1", RowOf(ScopeOf(watch, VariableKind.Graph, 1), "attempts").Value);
            Assert.AreEqual("7", RowOf(ScopeOf(watch, VariableKind.Graph, 2), "attempts").Value,
                "Two instances of one shared branch have separate scratch; merging them would invent a contradiction.");
        }

        [Test]
        public void CollidingScopeNamesAreDisambiguatedByCallSite()
        {
            var recording = new RecordingBuilder()
                .CallSite(1, 0, "Attack")
                .CallSite(2, 0, "Attack")
                .CallSite(3, 0, "Idle")
                .At(10).Write("attempts", "0", "1", VariableKind.Graph, callSite: 1)
                .At(11).Write("attempts", "0", "7", VariableKind.Graph, callSite: 2)
                .At(12).Write("waited", "0", "3", VariableKind.Graph, callSite: 3)
                .Build();

            var watch = BehaviorTreeVariableWatch.At(recording);

            Assert.AreEqual("Attack #1", ScopeOf(watch, VariableKind.Graph, 1).Label);
            Assert.AreEqual("Attack #2", ScopeOf(watch, VariableKind.Graph, 2).Label);
            Assert.AreEqual("Idle", ScopeOf(watch, VariableKind.Graph, 3).Label,
                "A name that does not collide stays clean — '#3' everywhere would be noise.");
        }

        [Test]
        public void TheAgentScopeIsListedFirst()
        {
            var recording = new RecordingBuilder()
                .CallSite(1, 0, "Combat")
                .At(10).Write("attempts", "0", "1", VariableKind.Graph, callSite: 1)
                .At(11).Write("hasTarget", "null", "True", VariableKind.Object)
                .At(12).Write("difficulty", "0", "2", VariableKind.Application)
                .Build();

            var watch = BehaviorTreeVariableWatch.At(recording);

            Assert.AreEqual(VariableKind.Object, watch.Scopes[0].Kind, "Agent state is what a designer looks for first.");
            Assert.AreEqual(VariableKind.Graph, watch.Scopes[1].Kind);
            Assert.AreEqual(VariableKind.Application, watch.Scopes[2].Kind, "The process-wide stores come last.");
        }

        [Test]
        public void TheRootGraphScopeIsNamedAfterTheTree()
        {
            var recording = new RecordingBuilder()
                .At(10).Write("patrolIndex", "0", "1", VariableKind.Graph)
                .Build();

            var watch = BehaviorTreeVariableWatch.At(recording);

            Assert.AreEqual("ZombieTree", ScopeOf(watch, VariableKind.Graph, BehaviorTreeCallSite.RootId).Label);
        }

        [Test]
        public void RowsAreSortedByKeySoTheyDoNotMoveAsThePlayheadDoes()
        {
            var recording = new RecordingBuilder()
                .At(10).Write("zeta", "0", "1", VariableKind.Object)
                .At(11).Write("alpha", "0", "1", VariableKind.Object)
                .At(12).Write("mid", "0", "1", VariableKind.Object)
                .Build();

            var keys = ScopeOf(BehaviorTreeVariableWatch.At(recording), VariableKind.Object)
                .Rows.Select(r => r.Key).ToArray();

            CollectionAssert.AreEqual(new[] { "alpha", "mid", "zeta" }, keys);
        }

        #endregion

        #region History and writers

        [Test]
        public void HistoryIsMostRecentFirstAndCarriesBothValues()
        {
            var recording = new RecordingBuilder()
                .At(10).Write("hasTarget", "null", "True", VariableKind.Object)
                .At(20).Write("hasTarget", "True", "False", VariableKind.Object)
                .Build();

            var row = RowOf(ScopeOf(BehaviorTreeVariableWatch.At(recording), VariableKind.Object), "hasTarget");

            Assert.AreEqual(20, row.History[0].Tick, "The latest write leads, so Value and History[0] cannot disagree.");
            Assert.AreEqual("True", row.History[0].OldValue);
            Assert.AreEqual("False", row.History[0].NewValue);
            Assert.AreEqual(10, row.History[1].Tick);
        }

        [Test]
        public void HistoryIsCappedButTheTotalIsStillReported()
        {
            var builder = new RecordingBuilder();

            for (int i = 1; i <= 20; i++)
            {
                builder.At(i).Write("counter", (i - 1).ToString(), i.ToString(), VariableKind.Object);
            }

            var row = RowOf(
                ScopeOf(BehaviorTreeVariableWatch.At(builder.Build(), -1, null, historyLimit: 5), VariableKind.Object),
                "counter");

            Assert.AreEqual(5, row.History.Count);
            Assert.AreEqual(20, row.WriteCount, "A row showing 5 of 20 is a variable churning, which is worth seeing.");
            Assert.IsTrue(row.HistoryClipped);
            Assert.AreEqual(20, row.History[0].Tick, "The cap must drop the oldest writes, not the newest.");
        }

        [Test]
        public void WriteCountOnlyCountsWritesAtOrBeforeTheVantageTick()
        {
            var builder = new RecordingBuilder();

            for (int i = 1; i <= 10; i++)
            {
                builder.At(i).Write("counter", (i - 1).ToString(), i.ToString(), VariableKind.Object);
            }

            var row = RowOf(ScopeOf(BehaviorTreeVariableWatch.At(builder.Build(), 4), VariableKind.Object), "counter");

            Assert.AreEqual(4, row.WriteCount, "Counting writes the vantage point cannot see would leak the future.");
        }

        [Test]
        public void ANodeWriterIsNamedByTheTopologyAndStaysLocatable()
        {
            var recording = new RecordingBuilder()
                .At(10).Write("alertLevel", "0", "1", VariableKind.Object, writer: Writer)
                .Build();

            var topology = new StubTopology().Node(Writer, "Raise Alert");
            var row = RowOf(ScopeOf(BehaviorTreeVariableWatch.At(recording, -1, topology), VariableKind.Object), "alertLevel");

            Assert.AreEqual("Raise Alert", row.Latest.WriterName);
            Assert.AreEqual(Writer, row.Latest.WriterGuid);
            Assert.IsTrue(row.Latest.HasLocatableWriter, "A node writer is something the canvas can select.");
        }

        [Test]
        public void WithoutATopologyANodeWriterFallsBackToItsGuid()
        {
            var recording = new RecordingBuilder()
                .At(10).Write("alertLevel", "0", "1", VariableKind.Object, writer: Writer)
                .Build();

            var row = RowOf(ScopeOf(BehaviorTreeVariableWatch.At(recording), VariableKind.Object), "alertLevel");

            Assert.AreEqual(Writer.ToString(), row.Latest.WriterName,
                "A guid is honest where a name is unavailable; inventing one would be worse.");
        }

        [Test]
        public void AnExternalWriterKeepsItsNameAndIsNotLocatable()
        {
            var recording = new RecordingBuilder()
                .At(10).ExternalWrite("hasTarget", "null", "True", "VisionSensor")
                .Build();

            var row = RowOf(ScopeOf(BehaviorTreeVariableWatch.At(recording), VariableKind.Object), "hasTarget");

            Assert.AreEqual("VisionSensor", row.Latest.WriterName);
            Assert.IsFalse(row.Latest.HasLocatableWriter,
                "A sensor is not on the canvas, so offering to select it would be a button that does nothing.");
        }

        #endregion

        #region Degenerate input

        [Test]
        public void ANullRecordingIsAnEmptyWatchRatherThanAThrow()
        {
            var watch = BehaviorTreeVariableWatch.At(null);

            Assert.IsTrue(watch.IsEmpty);
            Assert.IsEmpty(watch.Scopes, "A panel drawing before an agent exists must not be the thing that breaks.");
        }

        [Test]
        public void ARecordingWithNoWritesIsEmpty()
        {
            var recording = new RecordingBuilder().At(5).Enter(Writer).Build();

            Assert.IsTrue(BehaviorTreeVariableWatch.At(recording).IsEmpty);
        }

        [Test]
        public void AWriteFromAnUnregisteredCallSiteStillGetsAScope()
        {
            // Possible on a clipped recording: the sub-tree push scrolled out of the ring while its writes
            // did not. Losing the row would hide a real value; naming it by id is the honest fallback.
            var recording = new RecordingBuilder()
                .At(10).Write("attempts", "0", "1", VariableKind.Graph, callSite: 9)
                .Build();

            Assert.AreEqual("call site 9", ScopeOf(BehaviorTreeVariableWatch.At(recording), VariableKind.Graph, 9).Label);
        }

        #endregion

        #region Against the real recorder

        /// <summary>
        /// Drives a real node through the real recorder, so the model and Component 1 cannot drift apart. The
        /// hand-built recordings above would keep passing if <c>SaveVariable</c> stopped reporting the kind.
        /// </summary>
        [Test]
        public void ARealNodeWritingBothKindsProducesBothScopes()
        {
            var recorder = RecordRealWrites(out var agent);

            try
            {
                var watch = BehaviorTreeVariableWatch.At(recorder);

                Assert.AreEqual("1", RowOf(ScopeOf(watch, VariableKind.Graph, BehaviorTreeCallSite.RootId), "attempts")?.Value,
                    "A Graph write must land in the branch scope the write was made from.");
                Assert.AreEqual("2", RowOf(ScopeOf(watch, VariableKind.Object), "alertLevel")?.Value,
                    "An Object write must land in the agent scope.");
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(agent);
                BehaviorTreeFlightRecorders.Reset();
            }
        }

        /// <summary>
        /// Drives a real <see cref="GameplayNode"/> through the real recorder, one write of each kind.
        ///
        /// <para>
        /// A real machine is needed rather than a bare node: <c>SaveVariable</c> resolves
        /// <see cref="VariableKind.Object"/> against the agent's Variables component, so an Object write
        /// without one throws — which is also why this is the write path worth covering rather than assuming.
        /// </para>
        /// </summary>
        private static BehaviorTreeFlightRecorder RecordRealWrites(out UnityEngine.GameObject agent)
        {
            var recorder = new BehaviorTreeFlightRecorder("Zombie", "ZombieTree");

            agent = new UnityEngine.GameObject("Zombie");

            // RequireComponent(typeof(Variables)) adds the store; BindVariables does what Awake would.
            var machine = agent.AddComponent<BindableMachine>();
            machine.BindVariables();
            machine.SetFlightRecorder(recorder);

            var node = new WritingNode();
            node.SetMachine(machine);
            node.SetFlightRecorder(recorder);

            node.Write("attempts", VariableKind.Graph, 1);
            node.Write("alertLevel", VariableKind.Object, 2);

            return recorder;
        }

        /// <summary>
        /// A machine whose <see cref="BehaviorTreeMachine.Variables"/> can be bound without entering play mode.
        /// <c>Awake</c> does that binding and does not run in edit mode, so an Object write through a real
        /// <see cref="GameplayNode"/> would otherwise throw before reaching the store.
        /// </summary>
        private sealed class BindableMachine : BehaviorTreeMachine
        {
            public void BindVariables() => Variables = GetComponent<Variables>();
        }

        /// <summary>A <see cref="GameplayNode"/> exposing its protected write so a test can make one.</summary>
        private sealed class WritingNode : GameplayNode
        {
            public override string NodeName => "Writing Test Node";

            public void Write(string key, VariableKind kind, object value) => SaveVariable(key, kind, value);
        }

        #endregion

        #region Round trip

        [Test]
        public void TheScopeSurvivesAJsonRoundTrip()
        {
            // Through the real dump, which only takes a real recorder — so this covers the write path, the
            // JSON property and the import together. A hand-built snapshot would skip the first of those.
            var recorder = RecordRealWrites(out var agent);

            try
            {
                var json = BehaviorTreeRecordingDump.ToJson(recorder);

                Assert.IsTrue(BehaviorTreeRecordingImport.TryFromJson(json, out var imported), "The dump must re-import.");

                var watch = BehaviorTreeVariableWatch.At(imported);

                Assert.AreEqual("1", RowOf(ScopeOf(watch, VariableKind.Graph, BehaviorTreeCallSite.RootId), "attempts")?.Value,
                    "A Graph scope that imported as anything else would regroup the whole table.");
                Assert.AreEqual("2", RowOf(ScopeOf(watch, VariableKind.Object), "alertLevel")?.Value);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(agent);
                BehaviorTreeFlightRecorders.Reset();
            }
        }

        [Test]
        public void ARecordingExportedBeforeScopeWasRecordedImportsAsAgentState()
        {
            // A file written by the previous build: a VariableWrite with no "scope" property. Object is the
            // right assumption — Flow, the enum's default, means "not a variable write" here and would put
            // every old write in a scope of its own.
            const string legacy =
                "{\"agent\":\"Zombie\",\"tree\":\"ZombieTree\",\"tick\":12,\"dropped\":0," +
                "\"callSites\":[{\"id\":0,\"parent\":0,\"asset\":\"Zombie\"}]," +
                "\"events\":[{\"tick\":10,\"seq\":0,\"frame\":10,\"time\":0.2,\"kind\":\"VariableWrite\"," +
                "\"callSite\":0,\"key\":\"hasTarget\",\"from\":\"null\",\"to\":\"True\",\"writer\":\"VisionSensor\"}]}";

            Assert.IsTrue(BehaviorTreeRecordingImport.TryFromJson(legacy, out var imported));

            var watch = BehaviorTreeVariableWatch.At(imported);

            Assert.AreEqual("True", RowOf(ScopeOf(watch, VariableKind.Object), "hasTarget")?.Value);
            Assert.AreEqual("VisionSensor", RowOf(ScopeOf(watch, VariableKind.Object), "hasTarget").Latest.WriterName);
        }

        #endregion
    }
}
