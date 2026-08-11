using System;
using System.Collections.Generic;
using Unity.VisualScripting;

namespace ArcaneOnyx.BehaviorTree.Debugging
{
    /// <summary>
    /// Every variable the recording saw written, grouped by the store it lives in, valued as of one tick.
    ///
    /// <para>
    /// Pure C# over an <see cref="IBehaviorTreeRecording"/>, like the timeline and the explainer, so a
    /// recording exported from someone else's playtest reads exactly as well as the agent in front of you and
    /// the model can be tested without standing up a scene.
    /// </para>
    ///
    /// <para>
    /// <b>Derived only from writes.</b> A variable that was declared but never written emits no events and so
    /// is absent here — deliberately. The alternative, merging in the live agent's declarations, would put
    /// rows that mean "right now" beside rows that mean "at tick 400" and change which of the two you were
    /// reading depending on whether the scrubber happened to be parked. One kind of row, one meaning.
    /// </para>
    /// </summary>
    public sealed class BehaviorTreeVariableWatch
    {
        /// <summary>
        /// How many writes a row keeps. Enough to see a pattern — a value flickering between two states, or a
        /// counter climbing — without turning the panel into the event log the Flight Recorder window already
        /// is. <see cref="BehaviorTreeVariableWatchRow.WriteCount"/> still reports the total, so a clipped
        /// history says so rather than reading as complete.
        /// </summary>
        public const int DefaultHistoryLimit = 12;

        private BehaviorTreeVariableWatch(int tick, IReadOnlyList<BehaviorTreeVariableWatchScope> scopes)
        {
            Tick = tick;
            Scopes = scopes;
        }

        /// <summary>The tick every value in here is the value at.</summary>
        public int Tick { get; }

        /// <summary>Agent state first, then one scope per running branch, then the wider stores.</summary>
        public IReadOnlyList<BehaviorTreeVariableWatchScope> Scopes { get; }

        public bool IsEmpty
        {
            get
            {
                for (int i = 0; i < Scopes.Count; i++)
                {
                    if (Scopes[i].Rows.Count > 0) return false;
                }

                return true;
            }
        }

        /// <summary>
        /// Replays the recording's writes up to <paramref name="tick"/> and reports where every variable
        /// stood.
        /// </summary>
        /// <param name="recording">The recording to read. Null yields an empty watch rather than throwing.</param>
        /// <param name="tick">
        /// The vantage point. Negative means the end of the recording, matching
        /// <see cref="BehaviorTreeExplainer.Explain"/> — the two are read side by side and a different
        /// convention between them would be a trap.
        /// </param>
        /// <param name="topology">Optional, and supplies writer names only. Without it a writer is its guid.</param>
        /// <param name="historyLimit">Writes kept per row. Clamped to at least one.</param>
        public static BehaviorTreeVariableWatch At(
            IBehaviorTreeRecording recording,
            int tick = -1,
            IBehaviorTreeTopology topology = null,
            int historyLimit = DefaultHistoryLimit)
        {
            if (recording == null)
            {
                return new BehaviorTreeVariableWatch(0, Array.Empty<BehaviorTreeVariableWatchScope>());
            }

            var vantage = tick < 0 ? recording.Tick : tick;
            var limit = Math.Max(1, historyLimit);

            var builders = new Dictionary<ScopeKey, ScopeBuilder>();

            for (int i = 0; i < recording.EventCount; i++)
            {
                var recorded = recording.EventAt(i);

                if (recorded.Kind != BehaviorTreeEventKind.VariableWrite) continue;
                if (recorded.Tick > vantage) break;
                if (string.IsNullOrEmpty(recorded.Key)) continue;

                var key = ScopeKey.For(recorded);

                if (!builders.TryGetValue(key, out var scope))
                {
                    scope = new ScopeBuilder(key);
                    builders.Add(key, scope);
                }

                scope.Add(recorded, topology, limit);
            }

            return new BehaviorTreeVariableWatch(vantage, Build(builders, recording));
        }

        private static IReadOnlyList<BehaviorTreeVariableWatchScope> Build(
            Dictionary<ScopeKey, ScopeBuilder> builders, IBehaviorTreeRecording recording)
        {
            var ordered = new List<ScopeBuilder>(builders.Values);

            ordered.Sort(static (left, right) =>
            {
                var byKind = Rank(left.Key.Kind).CompareTo(Rank(right.Key.Kind));

                return byKind != 0 ? byKind : left.Key.CallSiteId.CompareTo(right.Key.CallSiteId);
            });

            var labels = Labels(ordered, recording);
            var scopes = new List<BehaviorTreeVariableWatchScope>(ordered.Count);

            for (int i = 0; i < ordered.Count; i++)
            {
                scopes.Add(ordered[i].Build(labels[i]));
            }

            return scopes;
        }

        /// <summary>
        /// Agent state first because it is what a designer looks for, then the branch scopes in call-site
        /// order — which is the order the branches were entered — and the process-wide stores last, since a
        /// behaviour bug is almost never in them.
        /// </summary>
        private static int Rank(VariableKind kind)
        {
            switch (kind)
            {
                case VariableKind.Object: return 0;
                case VariableKind.Graph: return 1;
                case VariableKind.Scene: return 2;
                case VariableKind.Application: return 3;
                case VariableKind.Saved: return 4;
                default: return 5;
            }
        }

        /// <summary>
        /// Names for the scopes, with duplicates resolved.
        ///
        /// <para>
        /// A shared branch running at two call sites produces two scopes with the same asset name and
        /// different values in them. Left alone that reads as one branch contradicting itself, so the call
        /// site id is appended — but only to the names that actually collide, because <c>Combat #3</c>
        /// everywhere would be noise on the trees where it means nothing.
        /// </para>
        /// </summary>
        private static string[] Labels(List<ScopeBuilder> ordered, IBehaviorTreeRecording recording)
        {
            var names = new string[ordered.Count];
            var counts = new Dictionary<string, int>();

            for (int i = 0; i < ordered.Count; i++)
            {
                names[i] = BaseLabel(ordered[i].Key, recording);
                counts.TryGetValue(names[i], out var seen);
                counts[names[i]] = seen + 1;
            }

            for (int i = 0; i < ordered.Count; i++)
            {
                if (counts[names[i]] > 1) names[i] = $"{names[i]} #{ordered[i].Key.CallSiteId}";
            }

            return names;
        }

        private static string BaseLabel(ScopeKey key, IBehaviorTreeRecording recording)
        {
            switch (key.Kind)
            {
                case VariableKind.Object: return "agent";
                case VariableKind.Scene: return "scene";
                case VariableKind.Application: return "application";
                case VariableKind.Saved: return "saved";
            }

            if (key.CallSiteId == BehaviorTreeCallSite.RootId)
            {
                return string.IsNullOrEmpty(recording.TreeName) ? "root tree" : recording.TreeName;
            }

            var callSites = recording.CallSites;

            for (int i = 0; i < callSites.Count; i++)
            {
                if (callSites[i].Id != key.CallSiteId) continue;

                return string.IsNullOrEmpty(callSites[i].AssetName)
                    ? $"call site {key.CallSiteId}"
                    : callSites[i].AssetName;
            }

            // A write from a call site the table never registered. Possible on a clipped recording, where the
            // push scrolled out of the ring while the writes did not.
            return $"call site {key.CallSiteId}";
        }

        /// <summary>
        /// Identifies a store. Only <see cref="VariableKind.Graph"/> is per call site — everything else is one
        /// store the whole tree shares, so folding the call site in would split agent state into a group per
        /// branch that happened to write it.
        /// </summary>
        private readonly struct ScopeKey : IEquatable<ScopeKey>
        {
            public readonly VariableKind Kind;
            public readonly int CallSiteId;

            private ScopeKey(VariableKind kind, int callSiteId)
            {
                Kind = kind;
                CallSiteId = callSiteId;
            }

            public static ScopeKey For(in BehaviorTreeEvent recorded)
            {
                return recorded.VariableKind == VariableKind.Graph
                    ? new ScopeKey(VariableKind.Graph, recorded.CallSiteId)
                    : new ScopeKey(recorded.VariableKind, -1);
            }

            public bool Equals(ScopeKey other) => Kind == other.Kind && CallSiteId == other.CallSiteId;

            public override bool Equals(object obj) => obj is ScopeKey other && Equals(other);

            public override int GetHashCode() => unchecked(((int)Kind * 397) ^ CallSiteId);
        }

        private sealed class ScopeBuilder
        {
            private readonly Dictionary<string, RowBuilder> rows = new();

            public ScopeBuilder(ScopeKey key)
            {
                Key = key;
            }

            public ScopeKey Key { get; }

            public void Add(in BehaviorTreeEvent recorded, IBehaviorTreeTopology topology, int limit)
            {
                if (!rows.TryGetValue(recorded.Key, out var row))
                {
                    row = new RowBuilder(recorded.Key);
                    rows.Add(recorded.Key, row);
                }

                row.Add(recorded, topology, limit);
            }

            public BehaviorTreeVariableWatchScope Build(string label)
            {
                var built = new List<BehaviorTreeVariableWatchRow>(rows.Count);

                foreach (var row in rows.Values)
                {
                    built.Add(row.Build());
                }

                built.Sort(static (left, right) => string.CompareOrdinal(left.Key, right.Key));

                return new BehaviorTreeVariableWatchScope(Key.Kind, Key.CallSiteId, label, built);
            }
        }

        private sealed class RowBuilder
        {
            private readonly string key;

            /// <summary>Most recent first, so the cap drops the oldest and <c>[0]</c> is always the latest.</summary>
            private readonly List<BehaviorTreeVariableWatchWrite> history = new();

            private int count;

            public RowBuilder(string key)
            {
                this.key = key;
            }

            public void Add(in BehaviorTreeEvent recorded, IBehaviorTreeTopology topology, int limit)
            {
                count++;

                history.Insert(0, new BehaviorTreeVariableWatchWrite(
                    recorded.Tick,
                    recorded.Sequence,
                    recorded.CallSiteId,
                    recorded.OldValue ?? "null",
                    recorded.NewValue ?? "null",
                    recorded.RelatedGuid,
                    WriterName(recorded, topology)));

                if (history.Count > limit) history.RemoveAt(history.Count - 1);
            }

            public BehaviorTreeVariableWatchRow Build() => new(key, history, count);

            /// <summary>
            /// A node's display name when the topology knows it, the sensor's own name for an out-of-tree
            /// write, and the guid when neither — the same fallback ladder the timeline uses for lane labels,
            /// so the two panels never call one node two things.
            /// </summary>
            private static string WriterName(in BehaviorTreeEvent recorded, IBehaviorTreeTopology topology)
            {
                if (recorded.RelatedGuid != Guid.Empty)
                {
                    if (topology != null &&
                        topology.TryGetNode(recorded.RelatedGuid, out var node) &&
                        !string.IsNullOrEmpty(node.DisplayName))
                    {
                        return node.DisplayName;
                    }

                    return recorded.RelatedGuid.ToString();
                }

                return string.IsNullOrEmpty(recorded.Writer) ? "(unknown)" : recorded.Writer;
            }
        }
    }
}
