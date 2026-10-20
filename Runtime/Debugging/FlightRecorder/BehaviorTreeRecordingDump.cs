using System;
using ArcaneOnyx.GraphCore;
using ArcaneOnyx.VisualScriptingExtension;
using Unity.VisualScripting.FullSerializer;

namespace ArcaneOnyx.BehaviorTree.Debugging
{
    public static class BehaviorTreeRecordingDump
    {
        public static string ToJson(BehaviorTreeFlightRecorder recorder)
        {
            if (recorder == null) return FsJson.Object().Set("recorder", "(null)").Pretty();

            var json = FsJson.Object();

            AddHeader(json, recorder);
            AddCallSites(json, recorder);
            AddEvents(json, recorder.Events);
            AddTraces(json, recorder);

            return json.Pretty();
        }

        private static void AddHeader(fsData json, BehaviorTreeFlightRecorder recorder)
        {
            var events = recorder.Events;

            json.Set("agent", recorder.AgentName ?? BehaviorTreeRecordingNames.UnnamedAgent);
            json.Set("tree", recorder.TreeName ?? BehaviorTreeRecordingNames.UnnamedTree);
            json.Set("tick", recorder.Tick);
            json.Set("recording", recorder.IsRecording ? "on" : "off");
            json.Set("capacity", events.Capacity);
            json.Set("count", events.Count);
            json.Set("dropped", events.DroppedEventCount);
        }

        private static void AddCallSites(fsData json, BehaviorTreeFlightRecorder recorder)
        {
            var callSites = FsJson.List();

            foreach (var callSite in recorder.CallSites)
            {
                callSites.Add(DescribeCallSite(callSite));
            }

            json.Set("callSites", callSites);
        }

        private static fsData DescribeCallSite(in BehaviorTreeCallSite callSite)
        {
            var json = FsJson.Object();

            json.Set("id", callSite.Id);
            json.Set("asset", callSite.AssetName ?? BehaviorTreeRecordingNames.Unnamed);

            if (!callSite.IsRoot)
            {
                json.Set("parent", callSite.ParentId);
                json.Set("runNode", callSite.RunNodeGuid.ToString());
            }

            return json;
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

        private static fsData DescribeEvent(in BehaviorTreeEvent recorded)
        {
            var json = DescribeStamp(recorded);

            if (recorded.NodeGuid != Guid.Empty) json.Set("node", recorded.NodeGuid.ToString());

            switch (recorded.Kind)
            {
                case BehaviorTreeEventKind.NodeExit:
                    AddExitStatus(json, recorded);
                    break;

                case BehaviorTreeEventKind.GuardEval:
                    AddGuardResult(json, recorded);
                    break;

                case BehaviorTreeEventKind.NodeAborted:
                case BehaviorTreeEventKind.NodeSkipped:
                    AddResponsibleGuard(json, recorded);
                    break;

                case BehaviorTreeEventKind.NodeTakenOver:
                    AddResponsibleGuard(json, recorded);
                    AddPreemptor(json, recorded);
                    break;

                case BehaviorTreeEventKind.VariableWrite:
                    AddVariableWrite(json, recorded);
                    break;

                case BehaviorTreeEventKind.TreePushed:
                case BehaviorTreeEventKind.TreePopped:
                    AddSubTreeAsset(json, recorded);
                    break;
            }

            return json;
        }

        private static fsData DescribeStamp(in BehaviorTreeEvent recorded)
        {
            var json = FsJson.Object();

            json.Set("tick", recorded.Tick);
            json.Set("seq", recorded.Sequence);
            json.Set("frame", recorded.Frame);
            json.Set("time", recorded.Time);
            json.Set("kind", recorded.Kind.ToString());
            json.Set("callSite", recorded.CallSiteId);

            return json;
        }

        private static void AddExitStatus(fsData json, in BehaviorTreeEvent recorded)
        {
            if (recorded.Status == ExecutionStatus.None) return;

            json.Set("status", recorded.Status.ToString());
        }

        private static void AddGuardResult(fsData json, in BehaviorTreeEvent recorded)
        {
            json.Set("result", recorded.GuardResult);

            if (recorded.RelatedGuid != Guid.Empty) json.Set("owner", recorded.RelatedGuid.ToString());
        }

        private static void AddResponsibleGuard(fsData json, in BehaviorTreeEvent recorded)
        {
            if (recorded.RelatedGuid == Guid.Empty) return;

            json.Set("guard", recorded.RelatedGuid.ToString());
        }

        private static void AddPreemptor(fsData json, in BehaviorTreeEvent recorded)
        {
            if (!string.IsNullOrEmpty(recorded.PreemptorName)) json.Set("preemptor", recorded.PreemptorName);

            if (recorded.PreemptorGuid != Guid.Empty) json.Set("preemptorNode", recorded.PreemptorGuid.ToString());
        }

        private static void AddVariableWrite(fsData json, in BehaviorTreeEvent recorded)
        {
            json.Set("key", recorded.VariableName ?? BehaviorTreeRecordingNames.Unnamed);
            json.Set("variableKind", recorded.VariableKind.ToString());
            json.Set("from", recorded.OldValue ?? "null");
            json.Set("to", recorded.NewValue ?? "null");

            AddWriter(json, recorded);
        }

        private static void AddWriter(fsData json, in BehaviorTreeEvent recorded)
        {
            if (recorded.RelatedGuid != Guid.Empty) json.Set("writer", recorded.RelatedGuid.ToString());
            else if (!string.IsNullOrEmpty(recorded.WriterName)) json.Set("writer", recorded.WriterName);
        }

        private static void AddSubTreeAsset(fsData json, in BehaviorTreeEvent recorded)
        {
            json.Set("asset", recorded.SubTreeAssetName ?? BehaviorTreeRecordingNames.NoAssetAssigned);
        }

        /// <summary>
        /// Nested in their events, most events would carry an empty field, and traces that had scrolled
        /// out of their shorter ring would read as though the guards were never traced.
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
                chain.Add(DescribeChainNode(node));
            }

            json.Set("chain", chain);
        }

        private static fsData DescribeChainNode(GuardTraceNode node)
        {
            var json = FsJson.Object();

            json.Set("node", node.NodeGuid.ToString());
            json.Set("name", node.Name ?? BehaviorTreeRecordingNames.Unnamed);
            json.Set("type", node.TypeName ?? BehaviorTreeRecordingNames.UnknownNodeType);
            json.Set("value", node.Value ?? "null");
            json.Set("depth", node.Depth);
            json.Set("parent", node.ParentIndex);

            if (node.HasSnapshot) json.Set("snapshot", node.SnapshotIndex);

            return json;
        }

        private static void AddSnapshots(fsData json, GuardTrace trace)
        {
            if (trace.Snapshots.Count == 0) return;

            var snapshots = FsJson.List();

            foreach (var snapshot in trace.Snapshots)
            {
                snapshots.Add(DescribeSnapshot(snapshot));
            }

            json.Set("snapshots", snapshots);
        }

        private static fsData DescribeSnapshot(GuardGraphSnapshot snapshot)
        {
            var json = FsJson.Object();

            json.Set("owner", snapshot.OwnerNodeGuid.ToString());
            json.Set("graph", snapshot.GraphName ?? BehaviorTreeRecordingNames.Unnamed);
            json.Set("wires", DescribeWires(snapshot));

            return json;
        }

        private static fsData DescribeWires(GuardGraphSnapshot snapshot)
        {
            var wires = FsJson.List();

            foreach (var wire in snapshot.Wires)
            {
                wires.Add(DescribeWire(wire));
            }

            return wires;
        }

        private static fsData DescribeWire(GuardWireValue wire)
        {
            var json = FsJson.Object();

            json.Set("connection", wire.ConnectionGuid.ToString());
            json.Set("sourceUnit", wire.SourceUnitGuid.ToString());
            json.Set("sourceKey", wire.SourceKey ?? "");
            json.Set("destUnit", wire.DestinationUnitGuid.ToString());
            json.Set("destKey", wire.DestinationKey ?? "");
            json.Set("evaluated", wire.WasEvaluated);

            if (wire.WasEvaluated) json.Set("value", wire.Value ?? "null");

            return json;
        }
    }
}
