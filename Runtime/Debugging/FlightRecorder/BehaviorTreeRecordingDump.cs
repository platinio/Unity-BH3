using System;
using ArcaneOnyx.GraphCore;
using ArcaneOnyx.VisualScriptingExtension;
using Unity.VisualScripting.FullSerializer;

namespace ArcaneOnyx.BehaviorTree.Debugging
{
    /// <summary>
    /// A recording as JSON. Node guids are the same ones <see cref="BehaviorTreeDump"/> emits, so a dump and
    /// a recording together are an account of a bug someone else can read without the scene: the dump says
    /// what was built, the recording says what it did.
    /// </summary>
    public static class BehaviorTreeRecordingDump
    {
        public static string ToJson(BehaviorTreeFlightRecorder recorder)
        {
            if (recorder == null) return FsJson.Object().Set("recorder", "(null)").Pretty();

            var json = FsJson.Object();
            var events = recorder.Events;

            json.Set("agent", recorder.AgentName ?? BehaviorTreeRecordingNames.UnnamedAgent);
            json.Set("tree", recorder.TreeName ?? BehaviorTreeRecordingNames.UnnamedTree);
            json.Set("tick", recorder.Tick);
            json.Set("recording", recorder.IsRecording ? "on" : "off");
            json.Set("capacity", events.Capacity);
            json.Set("count", events.Count);
            json.Set("dropped", events.DroppedEventCount);

            AddCallSites(json, recorder);
            AddEvents(json, events);
            AddTraces(json, recorder);

            return json.Pretty();
        }

        private static void AddCallSites(fsData json, BehaviorTreeFlightRecorder recorder)
        {
            var callSites = FsJson.List();

            foreach (var callSite in recorder.CallSites)
            {
                var entry = FsJson.Object();
                entry.Set("id", callSite.Id);
                entry.Set("asset", callSite.AssetName ?? BehaviorTreeRecordingNames.Unnamed);

                if (!callSite.IsRoot)
                {
                    entry.Set("parent", callSite.ParentId);
                    entry.Set("runNode", callSite.RunNodeGuid.ToString());
                }

                callSites.Add(entry);
            }

            json.Set("callSites", callSites);
        }

        private static void AddEvents(fsData json, BehaviorTreeEventRing events)
        {
            var described = FsJson.List();

            foreach (var recorded in events)
            {
                described.Add(DescribeEvent(recorded));
            }

            json.Set("events", described);
        }

        /// <summary>
        /// Only the fields that mean something for the event's kind are written: a dump where the presence
        /// of a field is itself information reads better than one where every event carries empty slots.
        /// </summary>
        private static fsData DescribeEvent(in BehaviorTreeEvent recorded)
        {
            var json = FsJson.Object();

            json.Set("tick", recorded.Tick);
            json.Set("seq", recorded.Sequence);
            json.Set("frame", recorded.Frame);
            json.Set("time", recorded.Time);
            json.Set("kind", recorded.Kind.ToString());
            json.Set("callSite", recorded.CallSiteId);

            if (recorded.NodeGuid != Guid.Empty) json.Set("node", recorded.NodeGuid.ToString());

            switch (recorded.Kind)
            {
                case BehaviorTreeEventKind.NodeExit:
                    if (recorded.Status != ExecutionStatus.None) json.Set("status", recorded.Status.ToString());
                    break;

                case BehaviorTreeEventKind.GuardEval:
                    json.Set("result", recorded.GuardResult);
                    if (recorded.RelatedGuid != Guid.Empty) json.Set("owner", recorded.RelatedGuid.ToString());
                    break;

                case BehaviorTreeEventKind.NodeAborted:
                case BehaviorTreeEventKind.NodeSkipped:
                    if (recorded.RelatedGuid != Guid.Empty) json.Set("guard", recorded.RelatedGuid.ToString());
                    break;

                case BehaviorTreeEventKind.NodeTakenOver:
                    if (recorded.RelatedGuid != Guid.Empty) json.Set("guard", recorded.RelatedGuid.ToString());
                    if (!string.IsNullOrEmpty(recorded.PreemptorName)) json.Set("preemptor", recorded.PreemptorName);
                    if (recorded.PreemptorGuid != Guid.Empty)
                    {
                        json.Set("preemptorNode", recorded.PreemptorGuid.ToString());
                    }

                    break;

                case BehaviorTreeEventKind.VariableWrite:
                    json.Set("key", recorded.VariableName ?? BehaviorTreeRecordingNames.Unnamed);
                    json.Set("variableKind", recorded.VariableKind.ToString());
                    json.Set("from", recorded.OldValue ?? "null");
                    json.Set("to", recorded.NewValue ?? "null");

                    if (recorded.RelatedGuid != Guid.Empty) json.Set("writer", recorded.RelatedGuid.ToString());
                    else if (!string.IsNullOrEmpty(recorded.WriterName)) json.Set("writer", recorded.WriterName);
                    break;

                case BehaviorTreeEventKind.TreePushed:
                case BehaviorTreeEventKind.TreePopped:
                    json.Set("asset", recorded.SubTreeAssetName ?? BehaviorTreeRecordingNames.NoAssetAssigned);
                    break;
            }

            return json;
        }

        /// <summary>
        /// Guard traces live in their own array, linked to their events by tick and sequence. Nesting them
        /// would mean most events carried an empty field, and a recording whose traces had scrolled out of
        /// their shorter ring would read as though its guards were never traced.
        /// </summary>
        private static void AddTraces(fsData json, BehaviorTreeFlightRecorder recorder)
        {
            if (recorder.TraceCount == 0) return;

            var traces = FsJson.List();

            for (int index = 0; index < recorder.TraceCount; index++)
            {
                var trace = recorder.TraceAt(index);
                if (trace == null) continue;

                traces.Add(DescribeTrace(trace));
            }

            json.Set("traces", traces);
        }

        private static fsData DescribeTrace(GuardTrace trace)
        {
            var json = FsJson.Object();

            json.Set("tick", trace.Tick);
            json.Set("seq", trace.Sequence);
            json.Set("callSite", trace.CallSiteId);
            json.Set("guard", trace.GuardGuid.ToString());
            if (trace.OwnerGuid != Guid.Empty) json.Set("owner", trace.OwnerGuid.ToString());
            json.Set("result", trace.Result);

            AddChain(json, trace);
            AddSnapshots(json, trace);

            return json;
        }

        private static void AddChain(fsData json, GuardTrace trace)
        {
            var chain = FsJson.List();

            foreach (var node in trace.Chain)
            {
                var entry = FsJson.Object();
                entry.Set("node", node.NodeGuid.ToString());
                entry.Set("name", node.Name ?? BehaviorTreeRecordingNames.Unnamed);
                entry.Set("type", node.TypeName ?? BehaviorTreeRecordingNames.UnknownNodeType);
                entry.Set("value", node.Value ?? "null");
                entry.Set("depth", node.Depth);
                entry.Set("parent", node.ParentIndex);

                if (node.HasSnapshot) entry.Set("snapshot", node.SnapshotIndex);

                chain.Add(entry);
            }

            json.Set("chain", chain);
        }

        private static void AddSnapshots(fsData json, GuardTrace trace)
        {
            if (trace.Snapshots.Count == 0) return;

            var snapshots = FsJson.List();

            foreach (var snapshot in trace.Snapshots)
            {
                var entry = FsJson.Object();
                entry.Set("owner", snapshot.OwnerNodeGuid.ToString());
                entry.Set("graph", snapshot.GraphName ?? BehaviorTreeRecordingNames.Unnamed);
                entry.Set("wires", DescribeWires(snapshot));

                snapshots.Add(entry);
            }

            json.Set("snapshots", snapshots);
        }

        private static fsData DescribeWires(GuardGraphSnapshot snapshot)
        {
            var wires = FsJson.List();

            foreach (var wire in snapshot.Wires)
            {
                var described = FsJson.Object();
                described.Set("connection", wire.ConnectionGuid.ToString());
                described.Set("sourceUnit", wire.SourceUnitGuid.ToString());
                described.Set("sourceKey", wire.SourceKey ?? "");
                described.Set("destUnit", wire.DestinationUnitGuid.ToString());
                described.Set("destKey", wire.DestinationKey ?? "");
                described.Set("evaluated", wire.WasEvaluated);

                if (wire.WasEvaluated) described.Set("value", wire.Value ?? "null");

                wires.Add(described);
            }

            return wires;
        }
    }
}
