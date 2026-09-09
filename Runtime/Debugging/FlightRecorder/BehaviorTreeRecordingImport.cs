using System;
using System.Collections.Generic;
using ArcaneOnyx.GraphCore;
using ArcaneOnyx.VisualScriptingExtension;
using Unity.VisualScripting.FullSerializer;

namespace ArcaneOnyx.BehaviorTree.Debugging
{
    /// <summary>
    /// Reads back what <see cref="BehaviorTreeRecordingDump"/> wrote.
    ///
    /// <para>
    /// The other half of the bug report. An exported recording that cannot be re-opened is a log file; one
    /// that can is evidence a second person can interrogate with the same tools as the first — which is the
    /// whole argument for exporting rather than screenshotting. It also lets an authoring agent read back a
    /// replay of a tree it generated, which is the runtime end of the loop <c>bt_verify</c> started.
    /// </para>
    ///
    /// <para>
    /// Tolerant by design: unknown event kinds and missing optional fields are skipped rather than fatal, so a
    /// recording written by a newer build still opens in an older one. A malformed document is a different
    /// matter and throws.
    /// </para>
    /// </summary>
    public static class BehaviorTreeRecordingImport
    {
        /// <summary>Parses a dump. Throws <see cref="FormatException"/> if the text is not valid JSON.</summary>
        public static BehaviorTreeRecordingSnapshot FromJson(string json)
        {
            var root = FsJson.Parse(json);

            return FromJson(root);
        }

        /// <summary>Parses, or returns null rather than throwing.</summary>
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

            var agent = root.Get("agent").AsStringOr("(agent)");
            var tree = root.Get("tree").AsStringOr("(tree)");
            var tick = root.Get("tick").AsIntOr();
            var dropped = root.Get("dropped").AsIntOr();

            return new BehaviorTreeRecordingSnapshot(
                agent, tree, tick, ReadEvents(root.Get("events")), ReadCallSites(root.Get("callSites"), tree), dropped,
                ReadTraces(root.Get("traces")));
        }

        private static List<GuardTrace> ReadTraces(fsData array)
        {
            var traces = new List<GuardTrace>();

            foreach (var item in array.Items())
            {
                var snapshots = ReadSnapshots(item.Get("snapshots"));
                var chain = new List<GuardTraceNode>();

                foreach (var node in item.Get("chain").Items())
                {
                    chain.Add(new GuardTraceNode(
                        ReadGuid(node.Get("node")),
                        node.Get("name").AsStringOr("(unnamed)"),
                        node.Get("type").AsStringOr("(unknown)"),
                        node.Get("value").AsStringOr("null"),
                        node.Get("depth").AsIntOr(),
                        node.Has("parent") ? node.Get("parent").AsIntOr(-1) : -1,
                        node.Has("snapshot") ? node.Get("snapshot").AsIntOr(-1) : -1));
                }

                traces.Add(new GuardTrace(
                    item.Get("tick").AsIntOr(),
                    item.Get("seq").AsIntOr(),
                    item.Get("callSite").AsIntOr(),
                    ReadGuid(item.Get("guard")),
                    ReadGuid(item.Get("owner")),
                    item.Get("result").AsBoolOr(),
                    chain,
                    snapshots));
            }

            return traces;
        }

        private static List<GuardGraphSnapshot> ReadSnapshots(fsData array)
        {
            var snapshots = new List<GuardGraphSnapshot>();

            foreach (var item in array.Items())
            {
                var wires = new List<GuardWireValue>();

                foreach (var wire in item.Get("wires").Items())
                {
                    var evaluated = wire.Get("evaluated").AsBoolOr();

                    wires.Add(new GuardWireValue(
                        ReadGuid(wire.Get("connection")),
                        ReadGuid(wire.Get("sourceUnit")),
                        wire.Get("sourceKey").AsStringOr(""),
                        ReadGuid(wire.Get("destUnit")),
                        wire.Get("destKey").AsStringOr(""),
                        evaluated ? wire.Get("value").AsStringOr("null") : "(not evaluated)",
                        evaluated));
                }

                snapshots.Add(new GuardGraphSnapshot(
                    ReadGuid(item.Get("owner")), item.Get("graph").AsStringOr("(unnamed)"), wires));
            }

            return snapshots;
        }

        private static List<BehaviorTreeCallSite> ReadCallSites(fsData array, string treeName)
        {
            var callSites = new List<BehaviorTreeCallSite>();

            foreach (var item in array.Items())
            {
                var id = item.Get("id").AsIntOr();

                callSites.Add(new BehaviorTreeCallSite(
                    id,
                    item.Has("parent") ? item.Get("parent").AsIntOr() : BehaviorTreeCallSite.RootId,
                    ReadGuid(item.Get("runNode")),
                    item.Get("asset").AsStringOr(id == BehaviorTreeCallSite.RootId ? treeName : "(unnamed)")));
            }

            return callSites;
        }

        private static List<BehaviorTreeEvent> ReadEvents(fsData array)
        {
            var events = new List<BehaviorTreeEvent>();

            foreach (var item in array.Items())
            {
                if (!Enum.TryParse<BehaviorTreeEventKind>(item.Get("kind").AsStringOr(), out var kind)) continue;

                events.Add(ReadEvent(item, kind));
            }

            return events;
        }

        private static BehaviorTreeEvent ReadEvent(fsData item, BehaviorTreeEventKind kind)
        {
            var related = Guid.Empty;
            var status = ExecutionStatus.None;
            var flag = false;
            string key = null;
            string oldValue = null;
            string newValue = null;
            string writer = null;
            var variableKind = BehaviorTreeVariableKind.None;

            switch (kind)
            {
                case BehaviorTreeEventKind.NodeExit:
                    Enum.TryParse(item.Get("status").AsStringOr(), out status);
                    break;

                case BehaviorTreeEventKind.GuardEval:
                    // The dump names these the other way round from every other kind — the guard is the node
                    // and the thing it protects is the owner — so read them the same way round on the way back.
                    flag = item.Get("result").AsBoolOr();
                    related = ReadGuid(item.Get("owner"));
                    break;

                case BehaviorTreeEventKind.NodeAborted:
                case BehaviorTreeEventKind.NodeSkipped:
                    related = ReadGuid(item.Get("guard"));
                    break;

                case BehaviorTreeEventKind.VariableWrite:
                    key = item.Get("key").AsStringOr();
                    oldValue = item.Get("from").AsStringOr("null");
                    newValue = item.Get("to").AsStringOr("null");

                    // "scope" was this property's name briefly, so it is still read: a recording exported in
                    // that window should not silently regroup. Absent altogether — a recording from before
                    // the kind was recorded at all — falls back to Object rather than to the enum's own
                    // default, because None means "not a variable write" here and would put every old write
                    // in a scope of its own. A recording written before this type existed spells that same
                    // "not applicable" as "Flow", which no longer parses — and lands on the same fallback.
                    var storedKind = item.Get("variableKind").AsStringOr();
                    if (string.IsNullOrEmpty(storedKind)) storedKind = item.Get("scope").AsStringOr();

                    variableKind = Enum.TryParse<BehaviorTreeVariableKind>(storedKind, out var parsedKind)
                        ? parsedKind
                        : BehaviorTreeVariableKind.Object;

                    // One field carries two things: a node guid when a node wrote it, a plain name when a
                    // sensor did. Which one it is, is exactly whether it parses as a guid.
                    var written = item.Get("writer").AsStringOr();
                    if (Guid.TryParse(written, out var writerGuid)) related = writerGuid;
                    else writer = written;
                    break;

                case BehaviorTreeEventKind.TreePushed:
                case BehaviorTreeEventKind.TreePopped:
                    key = item.Get("asset").AsStringOr();
                    break;
            }

            return BehaviorTreeEvent.Create(
                kind,
                item.Get("tick").AsIntOr(),
                item.Get("seq").AsIntOr(),
                item.Get("frame").AsIntOr(),
                item.Get("time").AsFloatOr(),
                item.Get("callSite").AsIntOr(),
                ReadGuid(item.Get("node")),
                related,
                status,
                flag,
                key,
                oldValue,
                newValue,
                variableKind,
                writer);
        }

        private static Guid ReadGuid(fsData value)
        {
            var text = value.AsStringOr();

            return !string.IsNullOrEmpty(text) && Guid.TryParse(text, out var guid) ? guid : Guid.Empty;
        }
    }
}
