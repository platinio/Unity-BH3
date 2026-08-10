using System;
using System.Collections.Generic;
using ArcaneOnyx.BehaviorTree.Debugging;
using ArcaneOnyx.GraphCore;
using Unity.VisualScripting;
using UnityEditor;
using UnityEngine;

namespace ArcaneOnyx.BehaviorTree
{
    /// <summary>
    /// The variable watch: what every variable held at the tick the scrubber is parked on, and who put it
    /// there.
    ///
    /// <para>
    /// A renderer over <see cref="BehaviorTreeVariableWatch"/>, which is pure C# over an
    /// <see cref="IBehaviorTreeRecording"/> — so this class cannot form its own opinion about what a value
    /// was, and an exported recording reads exactly like a live agent.
    /// </para>
    ///
    /// <para>
    /// <b>It answers for the playhead, never for the present.</b> Every value here is reconstructed from the
    /// writes at or before the timeline's tick, so the table and the ghosted canvas beside it always describe
    /// the same moment. That also means a variable declared but never written is absent: it emitted no
    /// events, and inventing a row for it would be the panel speaking for the agent rather than for the
    /// recording. To read live values, use the Blackboard panel or the Flight Recorder window.
    /// </para>
    ///
    /// <para>
    /// It has no agent picker and no Load button by design. The timeline owns both answers and publishes them
    /// through <see cref="BehaviorTreeDebugSession"/>; a second, independent answer here is how a panel ends
    /// up describing one recording while the canvas shows another.
    /// </para>
    /// </summary>
    public sealed class BehaviorTreeVariableWatchPanel : IAnchoredSidebarPanelContent
    {
        private const float Padding = 6.0f;
        private const float RowSpacing = 2.0f;
        private const float IndentWidth = 12.0f;
        private const float JumpButtonWidth = 24.0f;
        private const float TickColumnWidth = 62.0f;

        /// <summary>Below this, a row drops the writer rather than clipping both it and the value.</summary>
        private const float WriterColumnMinWidth = 260.0f;

        /// <summary>Expanded rows, keyed by scope and variable so two scopes' copies fold independently.</summary>
        private readonly HashSet<string> expanded = new();

        private string filter = string.Empty;

        private BehaviorTreeVariableWatch watch;
        private IBehaviorTreeRecording cachedRecording;
        private int cachedTick = int.MinValue;
        private int cachedEventCount = -1;

        private GUIStyle valueStyle;
        private GUIStyle writerStyle;
        private GUIStyle scopeStyle;
        private GUIStyle helpStyle;

        public BehaviorTreeVariableWatchPanel(GraphCore.IGraphContext context)
        {
            this.context = context;

            titleContent = new GUIContent("Variable Watch", BoltCore.Icons.variablesWindow?[IconSize.Small]);
        }

        public GraphCore.IGraphContext context { get; }

        public object sidebarControlHint => typeof(BehaviorTreeVariableWatchPanel);

        public GUIContent titleContent { get; }

        public Vector2 minSize => new(300.0f, 160.0f);

        /// <summary>
        /// Opens on the right, opposite the authoring panels, so it can be read at the same time as Why and
        /// the Blackboard rather than stacked under them. The user's own anchor choice wins from then on.
        /// </summary>
        public SidebarAnchor preferredAnchor => SidebarAnchor.Right;

        public void OnGUI(Rect position)
        {
            EnsureStyles();

            var x = position.x + Padding;
            var width = position.width - Padding * 2.0f;
            var y = position.y + Padding;

            var recording = CurrentRecording();

            if (recording == null)
            {
                DrawHelp(x, ref y, width, Application.isPlaying
                    ? "No agent is recording. Check BehaviorTreeFlightRecorders.GloballyEnabled."
                    : "Enter play mode, or open a recording in the Timeline panel.");
                return;
            }

            y = DrawHeader(x, y, width, recording);
            y = DrawFilter(x, y, width);

            var current = WatchFor(recording);

            if (current.IsEmpty)
            {
                DrawHelp(x, ref y, width, recording.EventCount == 0
                    ? "Nothing recorded yet."
                    : "No variable writes recorded at or before this tick.");
                return;
            }

            var shown = 0;

            for (int i = 0; i < current.Scopes.Count; i++)
            {
                shown += DrawScope(x, ref y, width, current.Scopes[i], recording);
            }

            if (shown == 0) DrawHelp(x, ref y, width, $"No variable matches “{filter}”.");
        }

        public float GetHeight(float width)
        {
            EnsureStyles();

            var recording = CurrentRecording();
            var inner = width - Padding * 2.0f;

            if (recording == null) return Padding * 2.0f + HelpHeight(inner);

            var height = Padding * 2.0f + (Line() + RowSpacing) * 2.0f;
            var current = WatchFor(recording);

            if (current.IsEmpty) return height + HelpHeight(inner);

            var shown = 0;

            for (int i = 0; i < current.Scopes.Count; i++)
            {
                var rows = MatchCount(current.Scopes[i]);
                if (rows == 0) continue;

                shown += rows;
                height += Line() + RowSpacing;

                foreach (var row in current.Scopes[i].Rows)
                {
                    if (!Matches(row)) continue;

                    height += Line() + RowSpacing;
                    if (IsExpanded(current.Scopes[i], row)) height += HistoryHeight(row);
                }
            }

            return shown == 0 ? height + HelpHeight(inner) : height;
        }

        #region Source

        /// <summary>
        /// Whatever the timeline is showing, and only if the timeline has something to show. The fallback is
        /// the live agent the rest of the debugger would resolve — which is what makes this panel usable
        /// before the timeline dock has ever been opened, without giving it a second opinion when it has.
        /// </summary>
        private IBehaviorTreeRecording CurrentRecording()
        {
            return BehaviorTreeDebugSession.Recording
                   ?? BehaviorTreeDebugTarget.Resolve(context, out _)?.FlightRecorder;
        }

        /// <summary>
        /// The tick to answer for: the timeline's playhead when this is the recording it belongs to, and the
        /// newest tick otherwise. -1 means "the end of the recording" to the model.
        /// </summary>
        private int CurrentTick(IBehaviorTreeRecording recording) => BehaviorTreeDebugSession.TickFor(recording);

        private IBehaviorTreeTopology CurrentTopology()
        {
            // Names only. Resolved from the live agent when there is one, because a loaded recording's writer
            // guids belong to a tree that may not be the one on this canvas — and a wrong name is worse than a
            // guid, which at least cannot be mistaken for an answer.
            if (BehaviorTreeDebugSession.Recording is BehaviorTreeFlightRecorder or null)
            {
                var machine = BehaviorTreeDebugTarget.Resolve(context, out _);
                if (machine != null) return BehaviorTreeGraphTopology.From(machine);
            }

            return context?.graph is BehaviorTreeGraph graph ? BehaviorTreeGraphTopology.From(graph) : null;
        }

        /// <summary>
        /// Rebuilt only when something it depends on moved. IMGUI asks for a height and then draws, so an
        /// uncached build would replay the ring twice per repaint of a panel that is open all session.
        /// </summary>
        private BehaviorTreeVariableWatch WatchFor(IBehaviorTreeRecording recording)
        {
            var tick = CurrentTick(recording);

            if (watch != null &&
                ReferenceEquals(cachedRecording, recording) &&
                cachedTick == tick &&
                cachedEventCount == recording.EventCount)
            {
                return watch;
            }

            watch = BehaviorTreeVariableWatch.At(recording, tick, CurrentTopology());
            cachedRecording = recording;
            cachedTick = tick;
            cachedEventCount = recording.EventCount;

            return watch;
        }

        #endregion

        #region Drawing

        private float DrawHeader(float x, float y, float width, IBehaviorTreeRecording recording)
        {
            var tick = CurrentTick(recording);
            var scrubbing = tick >= 0 && BehaviorTreeDebugSession.IsScrubbing;

            var label = scrubbing
                ? $"⏸ values at tick {tick} — {recording.AgentName}"
                : $"● live — tick {recording.Tick} — {recording.AgentName}";

            var rect = new Rect(x, y, width, Line());

            if (scrubbing) EditorGUI.DrawRect(rect, new Color(0.65f, 0.35f, 0.05f, 0.85f));

            GUI.Label(rect, label, scrubbing ? EditorStyles.whiteMiniLabel : EditorStyles.miniLabel);

            return y + Line() + RowSpacing;
        }

        private float DrawFilter(float x, float y, float width)
        {
            filter = EditorGUI.TextField(new Rect(x, y, width, Line()), filter, EditorStyles.toolbarSearchField);

            return y + Line() + RowSpacing;
        }

        /// <summary>Draws one store and its rows. Returns how many rows survived the filter.</summary>
        private int DrawScope(
            float x, ref float y, float width, BehaviorTreeVariableWatchScope scope,
            IBehaviorTreeRecording recording)
        {
            var matched = MatchCount(scope);
            if (matched == 0) return 0;

            GUI.Label(
                new Rect(x, y, width, Line()),
                $"{scope.Label}  ({scope.Kind})",
                scopeStyle);

            y += Line() + RowSpacing;

            foreach (var row in scope.Rows)
            {
                if (!Matches(row)) continue;

                DrawRow(x + IndentWidth, ref y, width - IndentWidth, scope, row, recording);
            }

            return matched;
        }

        private void DrawRow(
            float x, ref float y, float width, BehaviorTreeVariableWatchScope scope,
            BehaviorTreeVariableWatchRow row, IBehaviorTreeRecording recording)
        {
            var rect = new Rect(x, y, width, Line());
            var isExpanded = IsExpanded(scope, row);

            var foldout = new Rect(rect.x, rect.y, IndentWidth, rect.height);
            var jump = new Rect(rect.xMax - JumpButtonWidth, rect.y, JumpButtonWidth, rect.height);
            var tick = new Rect(jump.x - TickColumnWidth, rect.y, TickColumnWidth, rect.height);
            var body = new Rect(
                foldout.xMax, rect.y, Mathf.Max(0.0f, tick.x - foldout.xMax - 2.0f), rect.height);

            if (EditorGUI.Foldout(foldout, isExpanded, GUIContent.none) != isExpanded) Toggle(scope, row);

            // The writer shares the row when the panel is wide enough for it, and retreats into the expanded
            // history when it is not. A sidebar can be dragged narrow, and a name clipped mid-word beside a
            // value is worse than a name one fold away.
            var writer = row.Latest.WriterName;
            var writerWidth = body.width >= WriterColumnMinWidth
                ? Mathf.Min(body.width * 0.4f, writerStyle.CalcSize(new GUIContent(writer)).x)
                : 0.0f;

            var label = new Rect(body.x, body.y, body.width - writerWidth, body.height);

            GUI.Label(label, new GUIContent($"{row.Key} = {row.Value}", $"{row.Key} = {row.Value}"), valueStyle);

            if (writerWidth > 0.0f)
            {
                GUI.Label(new Rect(label.xMax, body.y, writerWidth, body.height), writer, writerStyle);
            }

            GUI.Label(tick, $"@{row.LastWriteTick}", writerStyle);

            // The jump-to-the-write hook the spec named: the row says when it changed, the scrubber shows what
            // the tree looked like when it did.
            if (GUI.Button(jump, new GUIContent("⏱", $"Scrub to tick {row.LastWriteTick}"), EditorStyles.miniButton))
            {
                BehaviorTreeTimelinePanel.RequestScrub(row.LastWriteTick);
            }

            y += Line() + RowSpacing;

            if (isExpanded) DrawHistory(x + IndentWidth, ref y, width - IndentWidth, row, recording);
        }

        private void DrawHistory(
            float x, ref float y, float width, BehaviorTreeVariableWatchRow row,
            IBehaviorTreeRecording recording)
        {
            for (int i = 0; i < row.History.Count; i++)
            {
                var write = row.History[i];
                var rect = new Rect(x, y, width, Line());

                var jump = new Rect(rect.xMax - JumpButtonWidth, rect.y, JumpButtonWidth, rect.height);
                var select = new Rect(jump.x - JumpButtonWidth - 2.0f, rect.y, JumpButtonWidth, rect.height);
                var body = new Rect(rect.x, rect.y, Mathf.Max(0.0f, select.x - rect.x - 2.0f), rect.height);

                GUI.Label(
                    body,
                    $"@{write.Tick}  {write.OldValue} → {write.NewValue}   by {write.WriterName}",
                    writerStyle);

                var target = TargetFor(write, recording);

                using (new EditorGUI.DisabledScope(!target.CanFollow))
                {
                    // The tooltip carries the reason even while disabled, so "why is this grey" is answerable
                    // by hovering rather than by reading the source.
                    if (GUI.Button(select, new GUIContent("→", target.Tooltip), EditorStyles.miniButton))
                    {
                        Follow(target);
                    }
                }

                if (GUI.Button(jump, new GUIContent("⏱", $"Scrub to tick {write.Tick}"), EditorStyles.miniButton))
                {
                    BehaviorTreeTimelinePanel.RequestScrub(write.Tick);
                }

                y += Line() + RowSpacing;
            }

            if (!row.HistoryClipped) return;

            GUI.Label(
                new Rect(x, y, width, Line()),
                $"…{row.WriteCount - row.History.Count} older write(s) not shown",
                writerStyle);

            y += Line() + RowSpacing;
        }

        private void DrawHelp(float x, ref float y, float width, string message)
        {
            var height = HelpHeight(width);

            EditorGUI.LabelField(new Rect(x, y, width, height), message, helpStyle);
            y += height;
        }

        /// <summary>
        /// Sizes are set here rather than inherited from <see cref="EditorStyles"/> because the defaults this
        /// panel would otherwise take — <c>miniLabel</c> for the secondary text — are small enough to be
        /// genuinely hard to read in a table you scan rather than glance at.
        /// </summary>
        private void EnsureStyles()
        {
            if (valueStyle != null) return;

            valueStyle = new GUIStyle(EditorStyles.label) { fontSize = 12 };
            writerStyle = new GUIStyle(EditorStyles.miniLabel) { fontSize = 11 };
            scopeStyle = new GUIStyle(EditorStyles.miniLabel) { fontSize = 12, fontStyle = FontStyle.Bold };
            helpStyle = new GUIStyle(EditorStyles.wordWrappedMiniLabel) { fontSize = 11 };
        }

        /// <summary>
        /// Row height. Two points above <see cref="EditorGUIUtility.singleLineHeight"/> so the larger text has
        /// somewhere to sit — at the default height a 12pt row reads as cramped.
        /// </summary>
        private static float Line() => EditorGUIUtility.singleLineHeight + 2.0f;

        private static float HelpHeight(float width) => Line() * 2.0f;

        private float HistoryHeight(BehaviorTreeVariableWatchRow row)
        {
            var lines = row.History.Count + (row.HistoryClipped ? 1 : 0);

            return lines * (Line() + RowSpacing);
        }

        #endregion

        #region Rows

        private bool Matches(BehaviorTreeVariableWatchRow row)
        {
            return string.IsNullOrEmpty(filter) ||
                   row.Key.IndexOf(filter, StringComparison.OrdinalIgnoreCase) >= 0;
        }

        private int MatchCount(BehaviorTreeVariableWatchScope scope)
        {
            if (string.IsNullOrEmpty(filter)) return scope.Rows.Count;

            var count = 0;

            foreach (var row in scope.Rows)
            {
                if (Matches(row)) count++;
            }

            return count;
        }

        private static string RowId(BehaviorTreeVariableWatchScope scope, BehaviorTreeVariableWatchRow row)
        {
            return $"{scope.Kind}:{scope.CallSiteId}:{row.Key}";
        }

        private bool IsExpanded(BehaviorTreeVariableWatchScope scope, BehaviorTreeVariableWatchRow row)
        {
            return expanded.Contains(RowId(scope, row));
        }

        private void Toggle(BehaviorTreeVariableWatchScope scope, BehaviorTreeVariableWatchRow row)
        {
            var id = RowId(scope, row);

            if (!expanded.Add(id)) expanded.Remove(id);
        }

        #endregion

        #region Writer

        /// <summary>
        /// Where the → button would take you, and what to say about it.
        ///
        /// <para>
        /// Almost every writer worth chasing is <em>not</em> on the open canvas. A sensor is not a node at
        /// all, and a node that writes agent state usually does so from inside a branch, whose asset is a
        /// different canvas — so a button that only selected within the current graph was disabled in exactly
        /// the cases it existed for. It now opens the writer's own asset first.
        /// </para>
        /// </summary>
        private readonly struct WriterTarget
        {
            public readonly BehaviorTreeGraphAsset Asset;
            public readonly Guid Guid;
            public readonly string Tooltip;

            private WriterTarget(BehaviorTreeGraphAsset asset, Guid guid, string tooltip)
            {
                Asset = asset;
                Guid = guid;
                Tooltip = tooltip;
            }

            public bool CanFollow => Guid != Guid.Empty;

            public static WriterTarget None(string why) => new(null, Guid.Empty, why);

            public static WriterTarget OnCanvas(Guid guid, string name) =>
                new(null, guid, $"Select '{name}' on this canvas");

            public static WriterTarget InAsset(BehaviorTreeGraphAsset asset, Guid guid, string name) =>
                new(asset, guid, $"Open {asset.name} and select '{name}'");
        }

        private WriterTarget TargetFor(in BehaviorTreeVariableWatchWrite write, IBehaviorTreeRecording recording)
        {
            if (!write.HasLocatableWriter)
            {
                return WriterTarget.None(
                    $"{write.WriterName} wrote this from outside the tree — there is no node to select.");
            }

            if (FindIn(context?.graph as BehaviorTreeGraph, write.WriterGuid) != null)
            {
                return WriterTarget.OnCanvas(write.WriterGuid, write.WriterName);
            }

            var asset = AssetForCallSite(write.CallSiteId, recording);

            if (asset == null || FindIn(asset.graph, write.WriterGuid) == null)
            {
                return WriterTarget.None(
                    $"'{write.WriterName}' is not on this canvas, and the branch it ran in could not be resolved "
                    + "to an asset.");
            }

            return WriterTarget.InAsset(asset, write.WriterGuid, write.WriterName);
        }

        private void Follow(in WriterTarget target)
        {
            if (!target.CanFollow) return;

            if (target.Asset == null)
            {
                Select(context, FindIn(context?.graph as BehaviorTreeGraph, target.Guid));
                return;
            }

            // Opening swaps the window's context, and with it this panel's, so the selection has to be made
            // against whichever context the window ends up on rather than the one captured here.
            AssetDatabase.OpenAsset(target.Asset);

            var window = GraphCore.GraphWindow.active;
            var opened = window?.context;

            Select(opened, FindIn(opened?.graph as BehaviorTreeGraph, target.Guid));
        }

        private static void Select(GraphCore.IGraphContext target, BehaviorTreeNode node)
        {
            if (target == null || node == null) return;

            target.selection.Select(node);
        }

        /// <summary>
        /// The asset a call site was running, resolved exactly where possible.
        ///
        /// <para>
        /// The call site's <c>RunNodeGuid</c> names the <c>RunBehaviorTreeGraphNode</c> that pushed it, and
        /// when that node is on the open canvas it holds the asset reference itself — no guessing. Only when
        /// it is not (a branch nested two deep, say) does this fall back to matching the recorded asset
        /// <i>name</i>, and an ambiguous name resolves to nothing rather than to the wrong tree.
        /// </para>
        /// </summary>
        private BehaviorTreeGraphAsset AssetForCallSite(int callSiteId, IBehaviorTreeRecording recording)
        {
            if (recording == null || callSiteId == BehaviorTreeCallSite.RootId) return null;

            var callSites = recording.CallSites;
            BehaviorTreeCallSite? found = null;

            for (int i = 0; i < callSites.Count; i++)
            {
                if (callSites[i].Id != callSiteId) continue;

                found = callSites[i];
                break;
            }

            if (found == null) return null;

            if (FindIn(context?.graph as BehaviorTreeGraph, found.Value.RunNodeGuid) is RunBehaviorTreeGraphNode run &&
                run.BehaviorTreeGraphAsset != null)
            {
                return run.BehaviorTreeGraphAsset;
            }

            return AssetNamed(found.Value.AssetName);
        }

        private static BehaviorTreeGraphAsset AssetNamed(string assetName)
        {
            if (string.IsNullOrEmpty(assetName)) return null;

            var guids = AssetDatabase.FindAssets($"\"{assetName}\" t:{nameof(BehaviorTreeGraphAsset)}");
            BehaviorTreeGraphAsset match = null;

            foreach (var guid in guids)
            {
                var path = AssetDatabase.GUIDToAssetPath(guid);
                var candidate = AssetDatabase.LoadAssetAtPath<BehaviorTreeGraphAsset>(path);

                if (candidate == null || candidate.name != assetName) continue;

                // A second exact match means the name does not identify a tree. Better to offer nothing than
                // to open someone else's Attack.
                if (match != null) return null;

                match = candidate;
            }

            return match;
        }

        private static BehaviorTreeNode FindIn(BehaviorTreeGraph graph, Guid guid)
        {
            if (graph == null || guid == Guid.Empty) return null;

            foreach (var node in graph.Nodes)
            {
                if (node != null && node.guid == guid) return node;
            }

            return null;
        }

        #endregion
    }
}
