using System;
using System.Collections.Generic;
using ArcaneOnyx.GraphCore;
using ArcaneOnyx.VisualScriptingExtension;
using Unity.VisualScripting.FullSerializer;

namespace ArcaneOnyx.BehaviorTree.Debugging
{
    /// <summary>
    /// Unknown event kinds and missing optional fields are skipped rather than fatal, so a recording
    /// written by a newer build still opens in an older one. A malformed document throws.
    /// </summary>
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

            var agent = root.Get("agent").AsStringOr(BehaviorTreeRecordingNames.UnnamedAgent);
            var tree = root.Get("tree").AsStringOr(BehaviorTreeRecordingNames.UnnamedTree);
            var tick = root.Get("tick").AsIntOr();
            var dropped = root.Get("dropped").AsIntOr();

            return new BehaviorTreeRecordingSnapshot(
                agent,
                tree,
                tick,
                ReadEvents(root.Get("events")),
                ReadCallSites(root.Get("callSites"), tree),
                dropped,
                ReadTraces(root.Get("traces")));
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
            var id = callSiteJson.Get("id").AsIntOr();
            var isRoot = id == BehaviorTreeCallSite.RootId;

            var parentId = callSiteJson.Has("parent")
                ? callSiteJson.Get("parent").AsIntOr()
                : BehaviorTreeCallSite.RootId;

            var assetName = callSiteJson.Get("asset")
                .AsStringOr(isRoot ? treeName : BehaviorTreeRecordingNames.Unnamed);

            return new BehaviorTreeCallSite(id, parentId, ReadGuid(callSiteJson.Get("runNode")), assetName);
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
            if (!Enum.TryParse<BehaviorTreeEventKind>(eventJson.Get("kind").AsStringOr(), out var kind))
            {
                return null;
            }

            var stamp = ReadStamp(eventJson);
            var node = ReadGuid(eventJson.Get("node"));

            return kind switch
            {
                BehaviorTreeEventKind.NodeEnter => BehaviorTreeEvent.NodeEntered(stamp, node),
                BehaviorTreeEventKind.NodeExit => ReadNodeExited(eventJson, stamp, node),
                BehaviorTreeEventKind.NodeSkipped =>
                    BehaviorTreeEvent.NodeSkippedByGuard(stamp, node, ReadGuardGuid(eventJson)),
                BehaviorTreeEventKind.NodeAborted =>
                    BehaviorTreeEvent.NodeAbortedByGuard(stamp, node, ReadGuardGuid(eventJson)),
                BehaviorTreeEventKind.NodeTakenOver => ReadNodeTakenOver(eventJson, stamp, node),
                BehaviorTreeEventKind.GuardEval => ReadGuardChanged(eventJson, stamp, node),
                BehaviorTreeEventKind.VariableWrite => ReadVariableWrite(eventJson, stamp),
                BehaviorTreeEventKind.TreePushed =>
                    BehaviorTreeEvent.SubTreePushed(stamp, node, ReadAssetName(eventJson)),
                BehaviorTreeEventKind.TreePopped =>
                    BehaviorTreeEvent.SubTreePopped(stamp, node, ReadAssetName(eventJson)),
                _ => null,
            };
        }

        private static BehaviorTreeEvent ReadNodeExited(
            fsData eventJson, in BehaviorTreeEventStamp stamp, Guid node)
        {
            Enum.TryParse<ExecutionStatus>(eventJson.Get("status").AsStringOr(), out var status);

            return BehaviorTreeEvent.NodeExited(stamp, node, status);
        }

        private static BehaviorTreeEvent ReadGuardChanged(
            fsData eventJson, in BehaviorTreeEventStamp stamp, Guid guard)
        {
            return BehaviorTreeEvent.GuardChanged(
                stamp, guard, ReadGuid(eventJson.Get("owner")), eventJson.Get("result").AsBoolOr());
        }

        private static BehaviorTreeEvent ReadNodeTakenOver(
            fsData eventJson, in BehaviorTreeEventStamp stamp, Guid victim)
        {
            return BehaviorTreeEvent.NodeTakenOver(
                stamp,
                victim,
                ReadGuardGuid(eventJson),
                eventJson.Get("preemptor").AsStringOr(null),
                ReadGuid(eventJson.Get("preemptorNode")));
        }

        private static BehaviorTreeEvent ReadVariableWrite(fsData eventJson, in BehaviorTreeEventStamp stamp)
        {
            var variableName = eventJson.Get("key").AsStringOr();
            var oldValue = eventJson.Get("from").AsStringOr("null");
            var newValue = eventJson.Get("to").AsStringOr("null");
            var variableKind = ReadVariableKind(eventJson);
            var writer = eventJson.Get("writer").AsStringOr();

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
            var stored = eventJson.Get("variableKind").AsStringOr();
            if (string.IsNullOrEmpty(stored)) stored = eventJson.Get("scope").AsStringOr();

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
                traceJson.Get("tick").AsIntOr(),
                traceJson.Get("seq").AsIntOr(),
                traceJson.Get("callSite").AsIntOr(),
                ReadGuid(traceJson.Get("guard")),
                ReadGuid(traceJson.Get("owner")),
                traceJson.Get("result").AsBoolOr(),
                ReadChain(traceJson.Get("chain")),
                ReadSnapshots(traceJson.Get("snapshots")));
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
                ReadGuid(nodeJson.Get("node")),
                nodeJson.Get("name").AsStringOr(BehaviorTreeRecordingNames.Unnamed),
                nodeJson.Get("type").AsStringOr(BehaviorTreeRecordingNames.UnknownNodeType),
                nodeJson.Get("value").AsStringOr("null"),
                nodeJson.Get("depth").AsIntOr(),
                ReadOptionalIndex(nodeJson, "parent"),
                ReadOptionalIndex(nodeJson, "snapshot"));
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
                ReadGuid(snapshotJson.Get("owner")),
                snapshotJson.Get("graph").AsStringOr(BehaviorTreeRecordingNames.Unnamed),
                ReadWires(snapshotJson.Get("wires")));
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
            var wasEvaluated = wireJson.Get("evaluated").AsBoolOr();

            return new GuardWireValue(
                ReadGuid(wireJson.Get("connection")),
                ReadGuid(wireJson.Get("sourceUnit")),
                wireJson.Get("sourceKey").AsStringOr(""),
                ReadGuid(wireJson.Get("destUnit")),
                wireJson.Get("destKey").AsStringOr(""),
                wasEvaluated ? wireJson.Get("value").AsStringOr("null") : "(not evaluated)",
                wasEvaluated);
        }

        private static BehaviorTreeEventStamp ReadStamp(fsData eventJson)
        {
            return new BehaviorTreeEventStamp(
                eventJson.Get("tick").AsIntOr(),
                eventJson.Get("seq").AsIntOr(),
                eventJson.Get("frame").AsIntOr(),
                eventJson.Get("time").AsFloatOr(),
                eventJson.Get("callSite").AsIntOr());
        }

        private static Guid ReadGuardGuid(fsData eventJson) => ReadGuid(eventJson.Get("guard"));

        private static string ReadAssetName(fsData eventJson) => eventJson.Get("asset").AsStringOr();

        private static int ReadOptionalIndex(fsData json, string property)
        {
            return json.Has(property) ? json.Get(property).AsIntOr(-1) : -1;
        }

        private static Guid ReadGuid(fsData value)
        {
            var text = value.AsStringOr();

            return !string.IsNullOrEmpty(text) && Guid.TryParse(text, out var guid) ? guid : Guid.Empty;
        }
    }
}
