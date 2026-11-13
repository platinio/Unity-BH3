using System;
using System.Collections.Generic;
using ArcaneOnyx.GraphCore;
using ArcaneOnyx.VisualScriptingExtension;
using Unity.VisualScripting.FullSerializer;
using Keys = ArcaneOnyx.BehaviorTree.Debugging.BehaviorTreeRecordingKeys;

namespace ArcaneOnyx.BehaviorTree.Debugging
{
    public static class BehaviorTreeRecordingImport
    {
        public static BehaviorTreeRecordingSnapshot FromJson(string json)
        {
            return ReadRecording(FsJson.Parse(json));
        }

        public static bool TryFromJson(string json, out BehaviorTreeRecordingSnapshot recording)
        {
            try
            {
                recording = FromJson(json);
                return recording != null;
            }
            catch (FormatException)
            {
                recording = null;
                return false;
            }
        }

        private static BehaviorTreeRecordingSnapshot ReadRecording(fsData root)
        {
            if (root == null || root.IsNull) return null;

            var agent = root.Get(Keys.Agent).AsStringOr(BehaviorTreeRecordingNames.UnnamedAgent);
            var tree = root.Get(Keys.Tree).AsStringOr(BehaviorTreeRecordingNames.UnnamedTree);
            var tick = root.Get(Keys.Tick).AsIntOr();
            var dropped = root.Get(Keys.Dropped).AsIntOr();

            return new BehaviorTreeRecordingSnapshot(
                agent,
                tree,
                tick,
                ReadEvents(root.Get(Keys.Events)),
                ReadCallSites(root.Get(Keys.CallSites), tree),
                dropped,
                ReadTraces(root.Get(Keys.Traces)));
        }

        private static List<BehaviorTreeCallSite> ReadCallSites(fsData callSiteArray, string treeName)
        {
            var callSites = new List<BehaviorTreeCallSite>();

            foreach (var callSiteJson in callSiteArray.Items())
            {
                callSites.Add(ReadCallSite(callSiteJson, treeName));
            }

            return callSites;
        }

        private static BehaviorTreeCallSite ReadCallSite(fsData callSiteJson, string treeName)
        {
            var id = callSiteJson.Get(Keys.Id).AsIntOr();
            var isRoot = id == BehaviorTreeCallSite.RootId;

            var parentId = callSiteJson.Has(Keys.Parent)
                ? callSiteJson.Get(Keys.Parent).AsIntOr()
                : BehaviorTreeCallSite.RootId;

            var assetName = callSiteJson.Get(Keys.Asset)
                .AsStringOr(isRoot ? treeName : BehaviorTreeRecordingNames.Unnamed);

            var runNodeGuid = ReadGuid(callSiteJson.Get(Keys.RunNode));

            return new BehaviorTreeCallSite(id, parentId, runNodeGuid, assetName);
        }

        private static List<BehaviorTreeEvent> ReadEvents(fsData eventArray)
        {
            var events = new List<BehaviorTreeEvent>();

            foreach (var eventJson in eventArray.Items())
            {
                var recorded = ReadEvent(eventJson);

                if (recorded.HasValue) events.Add(recorded.Value);
            }

            return events;
        }

        private static BehaviorTreeEvent? ReadEvent(fsData eventJson)
        {
            if (!Enum.TryParse<BehaviorTreeEventKind>(eventJson.Get(Keys.Kind).AsStringOr(), out var kind))
            {
                return null;
            }

            var stamp = ReadStamp(eventJson);
            var nodeGuid = ReadGuid(eventJson.Get(Keys.Node));

            return kind switch
            {
                BehaviorTreeEventKind.NodeEnter => BehaviorTreeEvent.NodeEntered(stamp, nodeGuid),
                BehaviorTreeEventKind.NodeExit => ReadNodeExited(eventJson, stamp, nodeGuid),
                BehaviorTreeEventKind.NodeSkipped =>
                    BehaviorTreeEvent.NodeSkippedByGuard(stamp, nodeGuid, ReadGuardGuid(eventJson)),
                BehaviorTreeEventKind.NodeAborted =>
                    BehaviorTreeEvent.NodeAbortedByGuard(stamp, nodeGuid, ReadGuardGuid(eventJson)),
                BehaviorTreeEventKind.NodeTakenOver => ReadNodeTakenOver(eventJson, stamp, nodeGuid),
                BehaviorTreeEventKind.GuardEval => ReadGuardChanged(eventJson, stamp, nodeGuid),
                BehaviorTreeEventKind.VariableWrite => ReadVariableWrite(eventJson, stamp),
                BehaviorTreeEventKind.TreePushed =>
                    BehaviorTreeEvent.SubTreePushed(stamp, nodeGuid, ReadAssetName(eventJson)),
                BehaviorTreeEventKind.TreePopped =>
                    BehaviorTreeEvent.SubTreePopped(stamp, nodeGuid, ReadAssetName(eventJson)),
                _ => null,
            };
        }

        private static BehaviorTreeEventStamp ReadStamp(fsData eventJson)
        {
            return new BehaviorTreeEventStamp(
                eventJson.Get(Keys.Tick).AsIntOr(),
                eventJson.Get(Keys.Sequence).AsIntOr(),
                eventJson.Get(Keys.Frame).AsIntOr(),
                eventJson.Get(Keys.Time).AsFloatOr(),
                eventJson.Get(Keys.CallSite).AsIntOr());
        }

        private static BehaviorTreeEvent ReadNodeExited(
            fsData eventJson, in BehaviorTreeEventStamp stamp, Guid nodeGuid)
        {
            Enum.TryParse<ExecutionStatus>(eventJson.Get(Keys.ExitStatus).AsStringOr(), out var status);

            return BehaviorTreeEvent.NodeExited(stamp, nodeGuid, status);
        }

        private static BehaviorTreeEvent ReadGuardChanged(
            fsData eventJson, in BehaviorTreeEventStamp stamp, Guid guardGuid)
        {
            return BehaviorTreeEvent.GuardChanged(
                stamp,
                guardGuid,
                ReadGuid(eventJson.Get(Keys.Owner)),
                eventJson.Get(Keys.GuardResult).AsBoolOr());
        }

        private static BehaviorTreeEvent ReadNodeTakenOver(
            fsData eventJson, in BehaviorTreeEventStamp stamp, Guid victimGuid)
        {
            return BehaviorTreeEvent.NodeTakenOver(
                stamp,
                victimGuid,
                ReadGuardGuid(eventJson),
                eventJson.Get(Keys.Preemptor).AsStringOr(null),
                ReadGuid(eventJson.Get(Keys.PreemptorNode)));
        }

        private static BehaviorTreeEvent ReadVariableWrite(fsData eventJson, in BehaviorTreeEventStamp stamp)
        {
            var variableName = eventJson.Get(Keys.VariableName).AsStringOr();
            var oldValue = eventJson.Get(Keys.From).AsStringOr(BehaviorTreeRecordingNames.NullValue);
            var newValue = eventJson.Get(Keys.To).AsStringOr(BehaviorTreeRecordingNames.NullValue);
            var variableKind = ReadVariableKind(eventJson);
            var writer = eventJson.Get(Keys.Writer).AsStringOr();

            return Guid.TryParse(writer, out var writerNodeGuid)
                ? BehaviorTreeEvent.VariableWrittenByNode(
                    stamp, writerNodeGuid, variableName, variableKind, oldValue, newValue)
                : BehaviorTreeEvent.VariableWrittenOutsideTree(
                    stamp, writer, variableName, variableKind, oldValue, newValue);
        }

        /// <summary>
        /// Falls back to Object rather than to the enum default, because None means "not a variable write"
        /// and would put every older write in a scope of its own. "scope" was this property's name briefly,
        /// so a recording exported in that window is still read rather than silently regrouped.
        /// </summary>
        private static BehaviorTreeVariableKind ReadVariableKind(fsData eventJson)
        {
            var stored = eventJson.Get(Keys.VariableKind).AsStringOr();
            if (string.IsNullOrEmpty(stored))
            {
                stored = eventJson.Get(Keys.VariableKindBeforeRename).AsStringOr();
            }

            return Enum.TryParse<BehaviorTreeVariableKind>(stored, out var parsed)
                ? parsed
                : BehaviorTreeVariableKind.Object;
        }

        private static List<GuardTrace> ReadTraces(fsData traceArray)
        {
            var traces = new List<GuardTrace>();

            foreach (var traceJson in traceArray.Items())
            {
                traces.Add(ReadTrace(traceJson));
            }

            return traces;
        }

        private static GuardTrace ReadTrace(fsData traceJson)
        {
            return new GuardTrace(
                traceJson.Get(Keys.Tick).AsIntOr(),
                traceJson.Get(Keys.Sequence).AsIntOr(),
                traceJson.Get(Keys.CallSite).AsIntOr(),
                ReadGuid(traceJson.Get(Keys.Guard)),
                ReadGuid(traceJson.Get(Keys.Owner)),
                traceJson.Get(Keys.GuardResult).AsBoolOr(),
                ReadChain(traceJson.Get(Keys.Chain)),
                ReadSnapshots(traceJson.Get(Keys.Snapshots)));
        }

        private static List<GuardTraceNode> ReadChain(fsData chainArray)
        {
            var chain = new List<GuardTraceNode>();

            foreach (var nodeJson in chainArray.Items())
            {
                chain.Add(ReadChainNode(nodeJson));
            }

            return chain;
        }

        private static GuardTraceNode ReadChainNode(fsData nodeJson)
        {
            return new GuardTraceNode(
                ReadGuid(nodeJson.Get(Keys.Node)),
                nodeJson.Get(Keys.Name).AsStringOr(BehaviorTreeRecordingNames.Unnamed),
                nodeJson.Get(Keys.Type).AsStringOr(BehaviorTreeRecordingNames.UnknownNodeType),
                nodeJson.Get(Keys.Value).AsStringOr(BehaviorTreeRecordingNames.NullValue),
                nodeJson.Get(Keys.Depth).AsIntOr(),
                ReadOptionalIndex(nodeJson, Keys.Parent),
                ReadOptionalIndex(nodeJson, Keys.Snapshot));
        }

        private static List<GuardGraphSnapshot> ReadSnapshots(fsData snapshotArray)
        {
            var snapshots = new List<GuardGraphSnapshot>();

            foreach (var snapshotJson in snapshotArray.Items())
            {
                snapshots.Add(ReadSnapshot(snapshotJson));
            }

            return snapshots;
        }

        private static GuardGraphSnapshot ReadSnapshot(fsData snapshotJson)
        {
            return new GuardGraphSnapshot(
                ReadGuid(snapshotJson.Get(Keys.Owner)),
                snapshotJson.Get(Keys.Graph).AsStringOr(BehaviorTreeRecordingNames.Unnamed),
                ReadWires(snapshotJson.Get(Keys.Wires)));
        }

        private static List<GuardWireValue> ReadWires(fsData wireArray)
        {
            var wires = new List<GuardWireValue>();

            foreach (var wireJson in wireArray.Items())
            {
                wires.Add(ReadWire(wireJson));
            }

            return wires;
        }

        private static GuardWireValue ReadWire(fsData wireJson)
        {
            var wasEvaluated = wireJson.Get(Keys.WasEvaluated).AsBoolOr();

            return new GuardWireValue(
                ReadGuid(wireJson.Get(Keys.Connection)),
                ReadGuid(wireJson.Get(Keys.SourceUnit)),
                wireJson.Get(Keys.SourceKey).AsStringOr(""),
                ReadGuid(wireJson.Get(Keys.DestinationUnit)),
                wireJson.Get(Keys.DestinationKey).AsStringOr(""),
                wasEvaluated
                    ? wireJson.Get(Keys.Value).AsStringOr(BehaviorTreeRecordingNames.NullValue)
                    : BehaviorTreeRecordingNames.NotEvaluated,
                wasEvaluated);
        }

        private static Guid ReadGuardGuid(fsData eventJson) => ReadGuid(eventJson.Get(Keys.Guard));

        private static string ReadAssetName(fsData eventJson) => eventJson.Get(Keys.Asset).AsStringOr();

        private static int ReadOptionalIndex(fsData json, string property)
        {
            if (!json.Has(property)) return GuardTraceNode.NoIndex;

            return json.Get(property).AsIntOr(GuardTraceNode.NoIndex);
        }

        private static Guid ReadGuid(fsData value)
        {
            var text = value.AsStringOr();

            return !string.IsNullOrEmpty(text) && Guid.TryParse(text, out var guid) ? guid : Guid.Empty;
        }
    }
}
