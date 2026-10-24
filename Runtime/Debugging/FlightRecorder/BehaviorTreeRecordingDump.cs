using System;
using ArcaneOnyx.GraphCore;
using ArcaneOnyx.VisualScriptingExtension;
using Unity.VisualScripting.FullSerializer;
using Keys = ArcaneOnyx.BehaviorTree.Debugging.BehaviorTreeRecordingKeys;

namespace ArcaneOnyx.BehaviorTree.Debugging
{
    public static class BehaviorTreeRecordingDump
    {
        public static string ToJson(BehaviorTreeFlightRecorder recorder)
        {
            if (recorder == null)
            {
                return FsJson.Object()
                    .Set(Keys.Recorder, BehaviorTreeRecordingNames.NoRecorder)
                    .Pretty();
            }

            var recordingJson = FsJson.Object();

            AddHeader(recordingJson, recorder);
            AddCallSites(recordingJson, recorder);
            AddEvents(recordingJson, recorder.Events);
            AddTraces(recordingJson, recorder);

            return recordingJson.Pretty();
        }

        private static void AddHeader(fsData recordingJson, BehaviorTreeFlightRecorder recorder)
        {
            var events = recorder.Events;

            recordingJson.Set(Keys.Agent, recorder.AgentName ?? BehaviorTreeRecordingNames.UnnamedAgent);
            recordingJson.Set(Keys.Tree, recorder.TreeName ?? BehaviorTreeRecordingNames.UnnamedTree);
            recordingJson.Set(Keys.Tick, recorder.Tick);
            recordingJson.Set(Keys.Recording, recorder.IsRecording ? "on" : "off");
            recordingJson.Set(Keys.Capacity, events.Capacity);
            recordingJson.Set(Keys.Count, events.Count);
            recordingJson.Set(Keys.Dropped, events.DroppedEventCount);
        }

        private static void AddCallSites(fsData recordingJson, BehaviorTreeFlightRecorder recorder)
        {
            var callSites = FsJson.List();

            foreach (var callSite in recorder.CallSites)
            {
                callSites.Add(DescribeCallSite(callSite));
            }

            recordingJson.Set(Keys.CallSites, callSites);
        }

        private static fsData DescribeCallSite(in BehaviorTreeCallSite callSite)
        {
            var callSiteJson = FsJson.Object();

            callSiteJson.Set(Keys.Id, callSite.Id);
            callSiteJson.Set(Keys.Asset, callSite.AssetName ?? BehaviorTreeRecordingNames.Unnamed);

            if (!callSite.IsRoot)
            {
                callSiteJson.Set(Keys.Parent, callSite.ParentId);
                callSiteJson.Set(Keys.RunNode, callSite.RunNodeGuid.ToString());
            }

            return callSiteJson;
        }

        private static void AddEvents(fsData recordingJson, BehaviorTreeEventRing events)
        {
            var described = FsJson.List();

            foreach (var recorded in events)
            {
                described.Add(DescribeEvent(recorded));
            }

            recordingJson.Set(Keys.Events, described);
        }

        private static fsData DescribeEvent(in BehaviorTreeEvent recorded)
        {
            var eventJson = DescribeStamp(recorded);

            if (recorded.NodeGuid != Guid.Empty) eventJson.Set(Keys.Node, recorded.NodeGuid.ToString());

            switch (recorded.Kind)
            {
                case BehaviorTreeEventKind.NodeExit:
                    AddExitStatus(eventJson, recorded);
                    break;

                case BehaviorTreeEventKind.GuardEval:
                    AddGuardResult(eventJson, recorded);
                    break;

                case BehaviorTreeEventKind.NodeAborted:
                case BehaviorTreeEventKind.NodeSkipped:
                    AddResponsibleGuard(eventJson, recorded);
                    break;

                case BehaviorTreeEventKind.NodeTakenOver:
                    AddResponsibleGuard(eventJson, recorded);
                    AddPreemptor(eventJson, recorded);
                    break;

                case BehaviorTreeEventKind.VariableWrite:
                    AddVariableWrite(eventJson, recorded);
                    break;

                case BehaviorTreeEventKind.TreePushed:
                case BehaviorTreeEventKind.TreePopped:
                    AddSubTreeAsset(eventJson, recorded);
                    break;
            }

            return eventJson;
        }

        private static fsData DescribeStamp(in BehaviorTreeEvent recorded)
        {
            var eventJson = FsJson.Object();

            eventJson.Set(Keys.Tick, recorded.Tick);
            eventJson.Set(Keys.Sequence, recorded.Sequence);
            eventJson.Set(Keys.Frame, recorded.Frame);
            eventJson.Set(Keys.Time, recorded.Time);
            eventJson.Set(Keys.Kind, recorded.Kind.ToString());
            eventJson.Set(Keys.CallSite, recorded.CallSiteId);

            return eventJson;
        }

        private static void AddExitStatus(fsData eventJson, in BehaviorTreeEvent recorded)
        {
            if (recorded.Status == ExecutionStatus.None) return;

            eventJson.Set(Keys.ExitStatus, recorded.Status.ToString());
        }

        private static void AddGuardResult(fsData eventJson, in BehaviorTreeEvent recorded)
        {
            eventJson.Set(Keys.GuardResult, recorded.GuardResult);

            if (recorded.RelatedGuid != Guid.Empty)
            {
                eventJson.Set(Keys.Owner, recorded.RelatedGuid.ToString());
            }
        }

        private static void AddResponsibleGuard(fsData eventJson, in BehaviorTreeEvent recorded)
        {
            if (recorded.RelatedGuid == Guid.Empty) return;

            eventJson.Set(Keys.Guard, recorded.RelatedGuid.ToString());
        }

        private static void AddPreemptor(fsData eventJson, in BehaviorTreeEvent recorded)
        {
            if (!string.IsNullOrEmpty(recorded.PreemptorName))
            {
                eventJson.Set(Keys.Preemptor, recorded.PreemptorName);
            }

            if (recorded.PreemptorGuid != Guid.Empty)
            {
                eventJson.Set(Keys.PreemptorNode, recorded.PreemptorGuid.ToString());
            }
        }

        private static void AddVariableWrite(fsData eventJson, in BehaviorTreeEvent recorded)
        {
            eventJson.Set(Keys.VariableName, recorded.VariableName ?? BehaviorTreeRecordingNames.Unnamed);
            eventJson.Set(Keys.VariableKind, recorded.VariableKind.ToString());
            eventJson.Set(Keys.From, recorded.OldValue ?? BehaviorTreeRecordingNames.NullValue);
            eventJson.Set(Keys.To, recorded.NewValue ?? BehaviorTreeRecordingNames.NullValue);

            AddWriter(eventJson, recorded);
        }

        private static void AddWriter(fsData eventJson, in BehaviorTreeEvent recorded)
        {
            if (recorded.RelatedGuid != Guid.Empty)
            {
                eventJson.Set(Keys.Writer, recorded.RelatedGuid.ToString());
            }
            else if (!string.IsNullOrEmpty(recorded.WriterName))
            {
                eventJson.Set(Keys.Writer, recorded.WriterName);
            }
        }

        private static void AddSubTreeAsset(fsData eventJson, in BehaviorTreeEvent recorded)
        {
            eventJson.Set(
                Keys.Asset, recorded.SubTreeAssetName ?? BehaviorTreeRecordingNames.NoAssetAssigned);
        }

        /// <summary>
        /// Nested in their events, most events would carry an empty field, and traces that had scrolled
        /// out of their shorter ring would read as though the guards were never traced.
        /// </summary>
        private static void AddTraces(fsData recordingJson, BehaviorTreeFlightRecorder recorder)
        {
            if (recorder.TraceCount == 0) return;

            var traces = FsJson.List();

            for (int index = 0; index < recorder.TraceCount; index++)
            {
                var trace = recorder.TraceAt(index);
                if (trace == null) continue;

                traces.Add(DescribeTrace(trace));
            }

            recordingJson.Set(Keys.Traces, traces);
        }

        private static fsData DescribeTrace(GuardTrace trace)
        {
            var traceJson = FsJson.Object();

            traceJson.Set(Keys.Tick, trace.Tick);
            traceJson.Set(Keys.Sequence, trace.Sequence);
            traceJson.Set(Keys.CallSite, trace.CallSiteId);
            traceJson.Set(Keys.Guard, trace.GuardGuid.ToString());
            traceJson.Set(Keys.GuardResult, trace.Result);

            if (trace.OwnerGuid != Guid.Empty)
            {
                traceJson.Set(Keys.Owner, trace.OwnerGuid.ToString());
            }

            AddChain(traceJson, trace);
            AddSnapshots(traceJson, trace);

            return traceJson;
        }

        private static void AddChain(fsData traceJson, GuardTrace trace)
        {
            var chain = FsJson.List();

            foreach (var node in trace.Chain)
            {
                chain.Add(DescribeChainNode(node));
            }

            traceJson.Set(Keys.Chain, chain);
        }

        private static fsData DescribeChainNode(GuardTraceNode node)
        {
            var nodeJson = FsJson.Object();

            nodeJson.Set(Keys.Node, node.NodeGuid.ToString());
            nodeJson.Set(Keys.Name, node.Name ?? BehaviorTreeRecordingNames.Unnamed);
            nodeJson.Set(Keys.Type, node.TypeName ?? BehaviorTreeRecordingNames.UnknownNodeType);
            nodeJson.Set(Keys.Value, node.Value ?? BehaviorTreeRecordingNames.NullValue);
            nodeJson.Set(Keys.Depth, node.Depth);
            nodeJson.Set(Keys.Parent, node.ParentIndex);

            if (node.HasSnapshot) nodeJson.Set(Keys.Snapshot, node.SnapshotIndex);

            return nodeJson;
        }

        private static void AddSnapshots(fsData traceJson, GuardTrace trace)
        {
            if (trace.Snapshots.Count == 0) return;

            var snapshots = FsJson.List();

            foreach (var snapshot in trace.Snapshots)
            {
                snapshots.Add(DescribeSnapshot(snapshot));
            }

            traceJson.Set(Keys.Snapshots, snapshots);
        }

        private static fsData DescribeSnapshot(GuardGraphSnapshot snapshot)
        {
            var snapshotJson = FsJson.Object();

            snapshotJson.Set(Keys.Owner, snapshot.OwnerNodeGuid.ToString());
            snapshotJson.Set(Keys.Graph, snapshot.GraphName ?? BehaviorTreeRecordingNames.Unnamed);
            snapshotJson.Set(Keys.Wires, DescribeWires(snapshot));

            return snapshotJson;
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
            var wireJson = FsJson.Object();

            wireJson.Set(Keys.Connection, wire.ConnectionGuid.ToString());
            wireJson.Set(Keys.SourceUnit, wire.SourceUnitGuid.ToString());
            wireJson.Set(Keys.SourceKey, wire.SourceKey ?? "");
            wireJson.Set(Keys.DestinationUnit, wire.DestinationUnitGuid.ToString());
            wireJson.Set(Keys.DestinationKey, wire.DestinationKey ?? "");
            wireJson.Set(Keys.WasEvaluated, wire.WasEvaluated);

            if (wire.WasEvaluated)
            {
                wireJson.Set(Keys.Value, wire.Value ?? BehaviorTreeRecordingNames.NullValue);
            }

            return wireJson;
        }
    }
}
