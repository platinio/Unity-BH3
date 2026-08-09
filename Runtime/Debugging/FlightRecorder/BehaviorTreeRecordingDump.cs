using System;
using ArcaneOnyx.GraphCore;
using ArcaneOnyx.UnityExtensions;

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
            var json = new JsonWriter();

            if (recorder == null)
            {
                json.OpenObject();
                json.Property("recorder", "(null)");
                json.CloseObject();

                return json.ToString();
            }

            json.OpenObject();

            json.Property("agent", recorder.AgentName ?? "(agent)");
            json.Property("tree", recorder.TreeName ?? "(tree)");
            json.Property("tick", recorder.Tick);
            json.Property("recording", recorder.IsRecording ? "on" : "off");

            var events = recorder.Events;
            json.Property("capacity", events.Capacity);
            json.Property("count", events.Count);

            // Non-zero means the window has scrolled past the start of the story. Worth saying out loud:
            // reading a clipped recording as though it were complete is how a wrong conclusion gets drawn.
            json.Property("dropped", events.Dropped);

            WriteCallSites(json, recorder);
            WriteEvents(json, events);
            WriteTraces(json, recorder);

            json.CloseObject();

            return json.ToString();
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
        private static void WriteTraces(JsonWriter json, BehaviorTreeFlightRecorder recorder)
        {
            if (recorder.TraceCount == 0) return;

            json.PropertyName("traces");
            json.OpenArray();

            for (int i = 0; i < recorder.TraceCount; i++)
            {
                var trace = recorder.TraceAt(i);
                if (trace == null) continue;

                json.OpenObject();

                json.Property("tick", trace.Tick);
                json.Property("seq", trace.Sequence);
                json.Property("callSite", trace.CallSiteId);
                json.Property("guard", trace.GuardGuid.ToString());
                if (trace.OwnerGuid != Guid.Empty) json.Property("owner", trace.OwnerGuid.ToString());
                json.Property("result", trace.Result);

                WriteChain(json, trace);
                WriteSnapshots(json, trace);

                json.CloseObject();
            }

            json.CloseArray();
        }

        private static void WriteChain(JsonWriter json, GuardTrace trace)
        {
            json.PropertyName("chain");
            json.OpenArray();

            foreach (var node in trace.Chain)
            {
                json.OpenObject();
                json.Property("node", node.NodeGuid.ToString());
                json.Property("name", node.Name ?? "(unnamed)");
                json.Property("type", node.TypeName ?? "(unknown)");
                json.Property("value", node.Value ?? "null");
                json.Property("depth", node.Depth);
                json.Property("parent", node.ParentIndex);

                if (node.HasSnapshot) json.Property("snapshot", node.SnapshotIndex);

                json.CloseObject();
            }

            json.CloseArray();
        }

        private static void WriteSnapshots(JsonWriter json, GuardTrace trace)
        {
            if (trace.Snapshots.Count == 0) return;

            json.PropertyName("snapshots");
            json.OpenArray();

            foreach (var snapshot in trace.Snapshots)
            {
                json.OpenObject();
                json.Property("owner", snapshot.OwnerNodeGuid.ToString());
                json.Property("graph", snapshot.GraphName ?? "(unnamed)");

                json.PropertyName("wires");
                json.OpenArray();

                foreach (var wire in snapshot.Wires)
                {
                    json.OpenObject();
                    json.Property("connection", wire.ConnectionGuid.ToString());
                    json.Property("sourceUnit", wire.SourceUnitGuid.ToString());
                    json.Property("sourceKey", wire.SourceKey ?? "");
                    json.Property("destUnit", wire.DestinationUnitGuid.ToString());
                    json.Property("destKey", wire.DestinationKey ?? "");

                    // Written even when nothing traversed the wire: "(not evaluated)" is information about
                    // which way the flow went, not a missing field.
                    json.Property("evaluated", wire.WasEvaluated);
                    if (wire.WasEvaluated) json.Property("value", wire.Value ?? "null");

                    json.CloseObject();
                }

                json.CloseArray();
                json.CloseObject();
            }

            json.CloseArray();
        }

        private static void WriteCallSites(JsonWriter json, BehaviorTreeFlightRecorder recorder)
        {
            json.PropertyName("callSites");
            json.OpenArray();

            foreach (var callSite in recorder.CallSites)
            {
                json.OpenObject();
                json.Property("id", callSite.Id);
                json.Property("asset", callSite.AssetName ?? "(unnamed)");

                if (!callSite.IsRoot)
                {
                    json.Property("parent", callSite.ParentId);
                    json.Property("runNode", callSite.RunNodeGuid.ToString());
                }

                json.CloseObject();
            }

            json.CloseArray();
        }

        private static void WriteEvents(JsonWriter json, BehaviorTreeEventRing events)
        {
            json.PropertyName("events");
            json.OpenArray();

            foreach (var recorded in events)
            {
                WriteEvent(json, recorded);
            }

            json.CloseArray();
        }

        private static void WriteEvent(JsonWriter json, in BehaviorTreeEvent recorded)
        {
            json.OpenObject();

            json.Property("tick", recorded.Tick);
            json.Property("seq", recorded.Sequence);
            json.Property("frame", recorded.Frame);
            json.Property("time", recorded.Time);
            json.Property("kind", recorded.Kind.ToString());
            json.Property("callSite", recorded.CallSiteId);

            if (recorded.NodeGuid != Guid.Empty) json.Property("node", recorded.NodeGuid.ToString());

            switch (recorded.Kind)
            {
                case BehaviorTreeEventKind.NodeExit:
                    if (recorded.Status != ExecutionStatus.None) json.Property("status", recorded.Status.ToString());
                    break;

                case BehaviorTreeEventKind.GuardEval:
                    // NodeGuid is the guard on this event and RelatedGuid is who it protects, which is the
                    // opposite of every other kind. Named accordingly so a reader is not left guessing.
                    json.Property("result", recorded.Flag);
                    if (recorded.RelatedGuid != Guid.Empty) json.Property("owner", recorded.RelatedGuid.ToString());
                    break;

                case BehaviorTreeEventKind.NodeAborted:
                case BehaviorTreeEventKind.NodeSkipped:
                    if (recorded.RelatedGuid != Guid.Empty) json.Property("guard", recorded.RelatedGuid.ToString());
                    break;

                case BehaviorTreeEventKind.VariableWrite:
                    json.Property("key", recorded.Key ?? "(unnamed)");
                    json.Property("from", recorded.OldValue ?? "null");
                    json.Property("to", recorded.NewValue ?? "null");

                    if (recorded.RelatedGuid != Guid.Empty) json.Property("writer", recorded.RelatedGuid.ToString());
                    else if (!string.IsNullOrEmpty(recorded.Writer)) json.Property("writer", recorded.Writer);
                    break;

                case BehaviorTreeEventKind.TreePushed:
                case BehaviorTreeEventKind.TreePopped:
                    json.Property("asset", recorded.Key ?? "(none assigned)");
                    break;
            }

            json.CloseObject();
        }
    }
}
