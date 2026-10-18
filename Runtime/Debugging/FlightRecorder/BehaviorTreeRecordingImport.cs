using System;
using System.Collections.Generic;
using ArcaneOnyx.GraphCore;
using ArcaneOnyx.VisualScriptingExtension;
using Unity.VisualScripting.FullSerializer;

namespace ArcaneOnyx.BehaviorTree.Debugging
{
    /// <summary>
    /// Reads back what <see cref="BehaviorTreeRecordingDump"/> wrote, so an exported recording can be
    /// interrogated with the same tools as a live one.
    ///
    /// <para>
    /// Unknown event kinds and missing optional fields are skipped rather than fatal, so a recording written
    /// by a newer build still opens in an older one. A malformed document throws.
    /// </para>
    /// </summary>
    public static class BehaviorTreeRecordingImport
    {
        /// <summary>Throws <see cref="FormatException"/> if the text is not valid JSON.</summary>
        public static BehaviorTreeRecordingSnapshot FromJson(string json)
        {
            return FromJson(FsJson.Parse(json));
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

        private static BehaviorTreeRecordingSnapshot FromJson(fsData root)
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
                var id = callSiteJson.Get("id").AsIntOr();
                var fallbackName = id == BehaviorTreeCallSite.RootId
                    ? treeName
                    : BehaviorTreeRecordingNames.Unnamed;

                callSites.Add(new BehaviorTreeCallSite(
                    id,
                    callSiteJson.Has("parent")
                        ? callSiteJson.Get("parent").AsIntOr()
                        : BehaviorTreeCallSite.RootId,
                    ReadGuid(callSiteJson.Get("runNode")),
                    callSiteJson.Get("asset").AsStringOr(fallbackName)));
            }

            return callSites;
        }

        private static List<BehaviorTreeEvent> ReadEvents(fsData eventArray)
        {
            var events = new List<BehaviorTreeEvent>();

            foreach (var eventJson in eventArray.Items())
            {
                if (TryReadEvent(eventJson, out var recorded)) events.Add(recorded);
            }

            return events;
        }

        private static bool TryReadEvent(fsData eventJson, out BehaviorTreeEvent recorded)
        {
            recorded = default;

            if (!Enum.TryParse<BehaviorTreeEventKind>(eventJson.Get("kind").AsStringOr(), out var kind))
            {
                return false;
            }

            var stamp = ReadStamp(eventJson);
            var nodeGuid = ReadGuid(eventJson.Get("node"));

            switch (kind)
            {
                case BehaviorTreeEventKind.NodeEnter:
                    recorded = BehaviorTreeEvent.NodeEntered(stamp, nodeGuid);
                    return true;

                case BehaviorTreeEventKind.NodeExit:
                    Enum.TryParse<ExecutionStatus>(eventJson.Get("status").AsStringOr(), out var status);
                    recorded = BehaviorTreeEvent.NodeExited(stamp, nodeGuid, status);
                    return true;

                case BehaviorTreeEventKind.NodeSkipped:
                    recorded = BehaviorTreeEvent.NodeSkippedByGuard(
                        stamp, nodeGuid, ReadGuid(eventJson.Get("guard")));
                    return true;

                case BehaviorTreeEventKind.NodeAborted:
                    recorded = BehaviorTreeEvent.NodeAbortedByGuard(
                        stamp, nodeGuid, ReadGuid(eventJson.Get("guard")));
                    return true;

                case BehaviorTreeEventKind.NodeTakenOver:
                    recorded = BehaviorTreeEvent.NodeTakenOver(
                        stamp,
                        nodeGuid,
                        ReadGuid(eventJson.Get("guard")),
                        eventJson.Get("preemptor").AsStringOr(null),
                        ReadGuid(eventJson.Get("preemptorNode")));
                    return true;

                case BehaviorTreeEventKind.GuardEval:
                    recorded = BehaviorTreeEvent.GuardChanged(
                        stamp,
                        nodeGuid,
                        ReadGuid(eventJson.Get("owner")),
                        eventJson.Get("result").AsBoolOr());
                    return true;

                case BehaviorTreeEventKind.VariableWrite:
                    recorded = ReadVariableWrite(eventJson, stamp);
                    return true;

                case BehaviorTreeEventKind.TreePushed:
                    recorded = BehaviorTreeEvent.SubTreePushed(
                        stamp, nodeGuid, eventJson.Get("asset").AsStringOr());
                    return true;

                case BehaviorTreeEventKind.TreePopped:
                    recorded = BehaviorTreeEvent.SubTreePopped(
                        stamp, nodeGuid, eventJson.Get("asset").AsStringOr());
                    return true;

                default:
                    return false;
            }
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
                traces.Add(new GuardTrace(
                    traceJson.Get("tick").AsIntOr(),
                    traceJson.Get("seq").AsIntOr(),
                    traceJson.Get("callSite").AsIntOr(),
                    ReadGuid(traceJson.Get("guard")),
                    ReadGuid(traceJson.Get("owner")),
                    traceJson.Get("result").AsBoolOr(),
                    ReadChain(traceJson.Get("chain")),
                    ReadSnapshots(traceJson.Get("snapshots"))));
            }

            return traces;
        }

        private static List<GuardTraceNode> ReadChain(fsData chainArray)
        {
            var chain = new List<GuardTraceNode>();

            foreach (var nodeJson in chainArray.Items())
            {
                chain.Add(new GuardTraceNode(
                    ReadGuid(nodeJson.Get("node")),
                    nodeJson.Get("name").AsStringOr(BehaviorTreeRecordingNames.Unnamed),
                    nodeJson.Get("type").AsStringOr(BehaviorTreeRecordingNames.UnknownNodeType),
                    nodeJson.Get("value").AsStringOr("null"),
                    nodeJson.Get("depth").AsIntOr(),
                    ReadIndexOr(nodeJson, "parent"),
                    ReadIndexOr(nodeJson, "snapshot")));
            }

            return chain;
        }

        private static List<GuardGraphSnapshot> ReadSnapshots(fsData snapshotArray)
        {
            var snapshots = new List<GuardGraphSnapshot>();

            foreach (var snapshotJson in snapshotArray.Items())
            {
                snapshots.Add(new GuardGraphSnapshot(
                    ReadGuid(snapshotJson.Get("owner")),
                    snapshotJson.Get("graph").AsStringOr(BehaviorTreeRecordingNames.Unnamed),
                    ReadWires(snapshotJson.Get("wires"))));
            }

            return snapshots;
        }

        private static List<GuardWireValue> ReadWires(fsData wireArray)
        {
            var wires = new List<GuardWireValue>();

            foreach (var wireJson in wireArray.Items())
            {
                var wasEvaluated = wireJson.Get("evaluated").AsBoolOr();

                wires.Add(new GuardWireValue(
                    ReadGuid(wireJson.Get("connection")),
                    ReadGuid(wireJson.Get("sourceUnit")),
                    wireJson.Get("sourceKey").AsStringOr(""),
                    ReadGuid(wireJson.Get("destUnit")),
                    wireJson.Get("destKey").AsStringOr(""),
                    wasEvaluated ? wireJson.Get("value").AsStringOr("null") : "(not evaluated)",
                    wasEvaluated));
            }

            return wires;
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

        private static int ReadIndexOr(fsData json, string property)
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
