using System;
using System.Collections.Generic;
using ArcaneOnyx.GraphCore;
using ArcaneOnyx.UnityExtensions;

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
            var root = JsonReader.Parse(json);

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

        private static BehaviorTreeRecordingSnapshot FromJson(JsonValue root)
        {
            if (root.IsNull) return null;

            var agent = root["agent"].AsString("(agent)");
            var tree = root["tree"].AsString("(tree)");
            var tick = root["tick"].AsInt();
            var dropped = root["dropped"].AsInt();

            return new BehaviorTreeRecordingSnapshot(
                agent, tree, tick, ReadEvents(root["events"]), ReadCallSites(root["callSites"], tree), dropped);
        }

        private static List<BehaviorTreeCallSite> ReadCallSites(JsonValue array, string treeName)
        {
            var callSites = new List<BehaviorTreeCallSite>();

            foreach (var item in array.Items)
            {
                var id = item["id"].AsInt();

                callSites.Add(new BehaviorTreeCallSite(
                    id,
                    item.Has("parent") ? item["parent"].AsInt() : BehaviorTreeCallSite.RootId,
                    ReadGuid(item["runNode"]),
                    item["asset"].AsString(id == BehaviorTreeCallSite.RootId ? treeName : "(unnamed)")));
            }

            return callSites;
        }

        private static List<BehaviorTreeEvent> ReadEvents(JsonValue array)
        {
            var events = new List<BehaviorTreeEvent>();

            foreach (var item in array.Items)
            {
                if (!Enum.TryParse<BehaviorTreeEventKind>(item["kind"].AsString(), out var kind)) continue;

                events.Add(ReadEvent(item, kind));
            }

            return events;
        }

        private static BehaviorTreeEvent ReadEvent(JsonValue item, BehaviorTreeEventKind kind)
        {
            var related = Guid.Empty;
            var status = ExecutionStatus.None;
            var flag = false;
            string key = null;
            string oldValue = null;
            string newValue = null;
            string writer = null;

            switch (kind)
            {
                case BehaviorTreeEventKind.NodeExit:
                    Enum.TryParse(item["status"].AsString(), out status);
                    break;

                case BehaviorTreeEventKind.GuardEval:
                    // The dump names these the other way round from every other kind — the guard is the node
                    // and the thing it protects is the owner — so read them the same way round on the way back.
                    flag = item["result"].AsBool();
                    related = ReadGuid(item["owner"]);
                    break;

                case BehaviorTreeEventKind.NodeAborted:
                case BehaviorTreeEventKind.NodeSkipped:
                    related = ReadGuid(item["guard"]);
                    break;

                case BehaviorTreeEventKind.VariableWrite:
                    key = item["key"].AsString();
                    oldValue = item["from"].AsString("null");
                    newValue = item["to"].AsString("null");

                    // One field carries two things: a node guid when a node wrote it, a plain name when a
                    // sensor did. Which one it is, is exactly whether it parses as a guid.
                    var written = item["writer"].AsString();
                    if (Guid.TryParse(written, out var writerGuid)) related = writerGuid;
                    else writer = written;
                    break;

                case BehaviorTreeEventKind.TreePushed:
                case BehaviorTreeEventKind.TreePopped:
                    key = item["asset"].AsString();
                    break;
            }

            return BehaviorTreeEvent.Create(
                kind,
                item["tick"].AsInt(),
                item["seq"].AsInt(),
                item["frame"].AsInt(),
                item["time"].AsFloat(),
                item["callSite"].AsInt(),
                ReadGuid(item["node"]),
                related,
                status,
                flag,
                key,
                oldValue,
                newValue,
                writer);
        }

        private static Guid ReadGuid(JsonValue value)
        {
            var text = value.AsString();

            return !string.IsNullOrEmpty(text) && Guid.TryParse(text, out var guid) ? guid : Guid.Empty;
        }
    }
}
