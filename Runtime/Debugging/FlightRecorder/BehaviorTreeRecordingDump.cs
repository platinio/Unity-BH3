using System;
using ArcaneOnyx.GraphCore;
using ArcaneOnyx.VisualScriptingExtension;
using Unity.VisualScripting.FullSerializer;

namespace ArcaneOnyx.BehaviorTree.Debugging
{
    /// <summary>
    /// A recording as JSON.
    ///
    /// <para>
    /// Node guids here are the same guids <see cref="BehaviorTreeDump"/> emits, so a recording and a tree
    /// dump together are a complete account of a bug that someone else can read without the scene, the
    /// prefab, or the reproduction steps. That pairing is the point: the tree dump says what was built, the
    /// recording says what it did.
    /// </para>
    ///
    /// <para>
    /// Only fields that mean something for a given event kind are written. A dump where every event carries
    /// an empty guard guid and a null value is harder to read than one where the presence of a field is
    /// itself information.
    /// </para>
    /// </summary>
    public static class BehaviorTreeRecordingDump
    {
        public static string ToJson(BehaviorTreeFlightRecorder recorder)
        {
            if (recorder == null) return FsJson.Object().Set("recorder", "(null)").Pretty();

            var json = FsJson.Object();

            json.Set("agent", recorder.AgentName ?? "(agent)");
            json.Set("tree", recorder.TreeName ?? "(tree)");
            json.Set("tick", recorder.Tick);
            json.Set("recording", recorder.IsRecording ? "on" : "off");

            var events = recorder.Events;
            json.Set("capacity", events.Capacity);
            json.Set("count", events.Count);

            // Non-zero means the window has scrolled past the start of the story. Worth saying out loud:
            // reading a clipped recording as though it were complete is how a wrong conclusion gets drawn.
            json.Set("dropped", events.Dropped);

            AddCallSites(json, recorder);
            AddEvents(json, events);
            AddTraces(json, recorder);

            return json.Pretty();
        }

        /// <summary>
        /// Guard traces, in their own array rather than nested inside the events they belong to.
        ///
        /// <para>
        /// They are linked by tick and sequence, which is a weaker join than nesting would be, but a trace is
        /// captured only on a transition and lives in a much shorter ring than the events — so nesting would
        /// mean most events carried an empty field, and a recording whose traces had scrolled away would look
        /// as though its guards had never been traced rather than as though the window had moved.
        /// </para>
        /// </summary>
        private static void AddTraces(fsData json, BehaviorTreeFlightRecorder recorder)
        {
            if (recorder.TraceCount == 0) return;

            var traces = FsJson.List();

            for (int i = 0; i < recorder.TraceCount; i++)
            {
                var trace = recorder.TraceAt(i);
                if (trace == null) continue;

                var entry = FsJson.Object();

                entry.Set("tick", trace.Tick);
                entry.Set("seq", trace.Sequence);
                entry.Set("callSite", trace.CallSiteId);
                entry.Set("guard", trace.GuardGuid.ToString());
                if (trace.OwnerGuid != Guid.Empty) entry.Set("owner", trace.OwnerGuid.ToString());
                entry.Set("result", trace.Result);

                AddChain(entry, trace);
                AddSnapshots(entry, trace);

                traces.Add(entry);
            }

            json.Set("traces", traces);
        }

        private static void AddChain(fsData json, GuardTrace trace)
        {
            var chain = FsJson.List();

            foreach (var node in trace.Chain)
            {
                var entry = FsJson.Object();
                entry.Set("node", node.NodeGuid.ToString());
                entry.Set("name", node.Name ?? "(unnamed)");
                entry.Set("type", node.TypeName ?? "(unknown)");
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
                entry.Set("graph", snapshot.GraphName ?? "(unnamed)");

                var wires = FsJson.List();

                foreach (var wire in snapshot.Wires)
                {
                    var described = FsJson.Object();
                    described.Set("connection", wire.ConnectionGuid.ToString());
                    described.Set("sourceUnit", wire.SourceUnitGuid.ToString());
                    described.Set("sourceKey", wire.SourceKey ?? "");
                    described.Set("destUnit", wire.DestinationUnitGuid.ToString());
                    described.Set("destKey", wire.DestinationKey ?? "");

                    // Written even when nothing traversed the wire: "(not evaluated)" is information about
                    // which way the flow went, not a missing field.
                    described.Set("evaluated", wire.WasEvaluated);
                    if (wire.WasEvaluated) described.Set("value", wire.Value ?? "null");

                    wires.Add(described);
                }

                entry.Set("wires", wires);
                snapshots.Add(entry);
            }

            json.Set("snapshots", snapshots);
        }

        private static void AddCallSites(fsData json, BehaviorTreeFlightRecorder recorder)
        {
            var callSites = FsJson.List();

            foreach (var callSite in recorder.CallSites)
            {
                var entry = FsJson.Object();
                entry.Set("id", callSite.Id);
                entry.Set("asset", callSite.AssetName ?? "(unnamed)");

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
                    // NodeGuid is the guard on this event and RelatedGuid is who it protects, which is the
                    // opposite of every other kind. Named accordingly so a reader is not left guessing.
                    json.Set("result", recorded.Flag);
                    if (recorded.RelatedGuid != Guid.Empty) json.Set("owner", recorded.RelatedGuid.ToString());
                    break;

                case BehaviorTreeEventKind.NodeAborted:
                case BehaviorTreeEventKind.NodeSkipped:
                    if (recorded.RelatedGuid != Guid.Empty) json.Set("guard", recorded.RelatedGuid.ToString());
                    break;

                case BehaviorTreeEventKind.VariableWrite:
                    json.Set("key", recorded.Key ?? "(unnamed)");
                    // Qualified rather than "kind", which this object already uses for the event kind.
                    json.Set("variableKind", recorded.VariableKind.ToString());
                    json.Set("from", recorded.OldValue ?? "null");
                    json.Set("to", recorded.NewValue ?? "null");

                    if (recorded.RelatedGuid != Guid.Empty) json.Set("writer", recorded.RelatedGuid.ToString());
                    else if (!string.IsNullOrEmpty(recorded.Writer)) json.Set("writer", recorded.Writer);
                    break;

                case BehaviorTreeEventKind.TreePushed:
                case BehaviorTreeEventKind.TreePopped:
                    json.Set("asset", recorded.Key ?? "(none assigned)");
                    break;
            }

            return json;
        }
    }
}
