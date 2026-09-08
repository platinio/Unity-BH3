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

        /// <summary>The filter box is taller than a row — it is typed into, not just read.</summary>
        private const float FilterHeight = 22.0f;

        private const float ClearButtonWidth = 22.0f;

        private static readonly string RecordingOffMessage =
            BehaviorTreeRecordingSwitch.OffWarning("no variable writes are being captured");

        /// <summary>Named so the placeholder can tell focused-and-empty from unfocused-and-empty.</summary>
        private const string FilterControlName = "BehaviorTreeVariableWatchFilter";

        /// <summary>Expanded rows, keyed by scope and variable so two scopes' copies fold independently.</summary>
        private readonly HashSet<string> expanded = new();

        private string filter = string.Empty;

        private BehaviorTreeVariableWatch watch;
        private BehaviorTreeRecordingStamp cachedStamp = BehaviorTreeRecordingStamp.None;
        private int cachedAtTick = int.MinValue;

        /// <summary>
        /// Which tree asset each call site was running, remembered so the project is searched once per call
        /// site instead of once per expanded history row per repaint.
        ///
        /// <para>
        /// Deliberately <em>not</em> invalidated with <see cref="watch"/>. That cache turns over every tick
        /// while an agent plays, and this answer does not depend on the tick or the event count at all — a
        /// call site's asset is decided by the recording's call-site registry and by what is on the canvas.
        /// Clearing the two together would have left a project-wide asset search running at tick rate, which
        /// is the same cost under a different name.
        /// </para>
        /// </summary>
        private readonly Dictionary<int, Remembered<BehaviorTreeGraphAsset>> assetByCallSite = new();
        private IBehaviorTreeRecording assetsFor;
        private BehaviorTreeGraph assetsOnCanvas;

        private GUIStyle valueStyle;
        private GUIStyle writerStyle;
        private GUIStyle scopeStyle;
        private GUIStyle helpStyle;
        private GUIStyle filterStyle;
        private GUIStyle placeholderStyle;

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

            if (BehaviorTreeRecordingSwitch.Silences(recording)) DrawRecordingOffWarning(x, ref y, width);

            if (recording == null)
            {
                DrawHelp(x, ref y, width, NoRecordingMessage());
                return;
            }

            y = DrawHeader(x, y, width, recording);
            y = DrawFilter(x, y, width);

            var current = WatchFor(recording);

            if (current.IsEmpty)
            {
                DrawHelp(x, ref y, width, EmptyMessage(recording));
                return;
            }

            var shown = 0;

            for (int i = 0; i < current.Scopes.Count; i++)
            {
                shown += DrawScope(x, ref y, width, current.Scopes[i], recording);
            }

            if (shown == 0) DrawHelp(x, ref y, width, NoMatchMessage());
        }

        public float GetHeight(float width)
        {
            EnsureStyles();

            var recording = CurrentRecording();
            var inner = width - Padding * 2.0f;

            var warning = BehaviorTreeRecordingSwitch.Silences(recording) ? WarningHeight(inner) + RowSpacing : 0.0f;

            if (recording == null) return Padding * 2.0f + warning + HelpHeight(NoRecordingMessage(), inner);

            var height = Padding * 2.0f + warning + Line() + FilterHeight + RowSpacing * 2.0f;
            var current = WatchFor(recording);

            if (current.IsEmpty) return height + HelpHeight(EmptyMessage(recording), inner);

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

            return shown == 0 ? height + HelpHeight(NoMatchMessage(), inner) : height;
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
        ///
        /// <para>
        /// The playhead is compared separately from the recording's own position, and both halves are
        /// load-bearing. Without the stamp this froze: with no timeline open the playhead is a constant -1,
        /// which left the event count as the only moving part of the key -- and that saturates at the ring's
        /// capacity, so after 2048 events the table stopped rebuilding while the header above it went on
        /// printing a live tick that kept advancing.
        /// </para>
        /// </summary>
        private BehaviorTreeVariableWatch WatchFor(IBehaviorTreeRecording recording)
        {
            var atTick = CurrentTick(recording);
            var stamp = BehaviorTreeRecordingStamp.Of(recording);

            if (watch != null && cachedStamp.Equals(stamp) && cachedAtTick == atTick) return watch;

            watch = BehaviorTreeVariableWatch.At(recording, atTick, CurrentTopology());
            cachedStamp = stamp;
            cachedAtTick = atTick;

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

        /// <summary>
        /// The filter box. Taller and larger than a toolbar search field, and it says what it is when empty —
        /// an unlabelled search box in a panel of unlabelled columns is a control you have to experiment with
        /// to understand.
        /// </summary>
        private float DrawFilter(float x, float y, float width)
        {
            var clearWidth = string.IsNullOrEmpty(filter) ? 0.0f : ClearButtonWidth;
            var field = new Rect(x, y, width - clearWidth, FilterHeight);

            GUI.SetNextControlName(FilterControlName);
            filter = EditorGUI.TextField(field, filter, filterStyle);

            if (string.IsNullOrEmpty(filter) && GUI.GetNameOfFocusedControl() != FilterControlName)
            {
                // Drawn over the empty field rather than as a separate label, so it disappears the moment
                // there is real text to read.
                var hint = new Rect(field.x + 4.0f, field.y, field.width - 4.0f, field.height);
                GUI.Label(hint, "Filter variables by name…", placeholderStyle);
            }

            if (clearWidth > 0.0f &&
                GUI.Button(new Rect(field.xMax, y, clearWidth, FilterHeight), "✕", EditorStyles.miniButton))
            {
                filter = string.Empty;
                GUI.FocusControl(null);
            }

            return y + FilterHeight + RowSpacing;
        }

        /// <summary>
        /// Why the table is empty, worded so the reader can act on it.
        ///
        /// <para>
        /// "No variable writes recorded" is true and useless when the reason is that the debugger is pointed
        /// at a different agent from the one doing the writing — a scene with a second agent makes an empty
        /// table look like a broken panel, and the filter box above it look broken with it. So when other
        /// agents are recording, say so and say how to switch.
        /// </para>
        /// </summary>
        private static string NoRecordingMessage()
        {
            return Application.isPlaying
                ? "No agent is recording."
                : "Enter play mode, or open a recording in the Timeline panel.";
        }

        private string NoMatchMessage() => $"No variable matches “{filter}”.";

        private static string EmptyMessage(IBehaviorTreeRecording recording)
        {
            var reason = recording.EventCount == 0
                ? $"{recording.AgentName} has recorded nothing yet."
                : $"{recording.AgentName} recorded no variable writes at or before this tick.";

            // Counted, never scanned: asking every recorder whether it holds a write would walk every ring on
            // every repaint, and the count alone is enough to point somewhere.
            var others = BehaviorTreeFlightRecorders.Active.Count - 1;

            if (others <= 0) return reason;

            return reason
                   + (others == 1 ? " 1 other agent is" : $" {others} other agents are")
                   + " recording — select it in the hierarchy to switch the debugger to it.";
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

            HandleBreakpointMenu(rect, row);

            y += Line() + RowSpacing;

            if (isExpanded) DrawHistory(x + IndentWidth, ref y, width - IndentWidth, row, recording);
        }

        /// <summary>
        /// Right-click a row to break the next time that variable is written.
        ///
        /// <para>
        /// This is where a variable breakpoint belongs, and it is the reason Component 6 was built on top of
        /// this panel rather than beside it. A node and a guard can be pointed at on a canvas; a variable can
        /// only be pointed at here, and the value offered — "break when it is written <em>this</em> again" —
        /// is the row's own current value, which is exactly the thing someone staring at a table of variables
        /// wants to stop on. Typing it by hand into the breakpoints panel is the fallback, not the path.
        /// </para>
        ///
        /// <para>
        /// The match is against the recorded rendering of the value, capped at 64 characters like everything
        /// else the recorder keeps, so the offer is only made when the row's value is short enough to still be
        /// whole. Offering it on a truncated value would arm a breakpoint that silently never fires.
        /// </para>
        /// </summary>
        private void HandleBreakpointMenu(Rect rect, BehaviorTreeVariableWatchRow row)
        {
            var e = Event.current;

            if (e.type != EventType.ContextClick || !rect.Contains(e.mousePosition)) return;

            var armed = BehaviorTreeBreakpoints.ForVariable(row.Key);
            var menu = new GenericMenu();

            menu.AddItem(
                new GUIContent($"Break on any write to {row.Key}"),
                armed != null && armed.Compare == BehaviorTreeVariableCompare.Changed,
                () => BehaviorTreeBreakpointStore.SetVariable(row.Key));

            var value = row.Value;
            var usable = !string.IsNullOrEmpty(value) && value.Length < BehaviorTreeEvent.MaxValueLength;

            if (usable)
            {
                AddCompareItem(menu, armed, row.Key, value, BehaviorTreeVariableCompare.Equals);
                AddCompareItem(menu, armed, row.Key, value, BehaviorTreeVariableCompare.NotEquals);

                // Ordering is offered only when the value in front of the reader is a number, because that is
                // the only case where it can ever match. A greyed-out "<" beside a GameObject says why it is
                // not on offer; an enabled one would arm a breakpoint that quietly never fires.
                var numeric = BehaviorTreeBreakpoint.TryParseNumber(value, out _);

                AddOrderingItem(menu, armed, row.Key, value, BehaviorTreeVariableCompare.LessThan, numeric);
                AddOrderingItem(menu, armed, row.Key, value, BehaviorTreeVariableCompare.GreaterThan, numeric);

                AddCompareItem(menu, armed, row.Key, value, BehaviorTreeVariableCompare.Contains);
            }

            menu.AddSeparator(string.Empty);

            if (armed == null) menu.AddDisabledItem(new GUIContent("Remove breakpoint"));
            else menu.AddItem(new GUIContent("Remove breakpoint"), false, () => BehaviorTreeBreakpointStore.Remove(armed));

            menu.ShowAsContext();
            e.Use();
        }

        private static void AddCompareItem(
            GenericMenu menu,
            BehaviorTreeBreakpoint armed,
            string key,
            string value,
            BehaviorTreeVariableCompare compare)
        {
            var on = armed != null && armed.Compare == compare && armed.ExpectedValue == value;

            menu.AddItem(
                new GUIContent($"Break when {key} {BehaviorTreeBreakpoint.Symbol(compare)} {value}"),
                on,
                () => BehaviorTreeBreakpointStore.SetVariable(key, value, compare));
        }

        private static void AddOrderingItem(
            GenericMenu menu,
            BehaviorTreeBreakpoint armed,
            string key,
            string value,
            BehaviorTreeVariableCompare compare,
            bool numeric)
        {
            var label = new GUIContent($"Break when {key} {BehaviorTreeBreakpoint.Symbol(compare)} {value}");

            if (!numeric)
            {
                menu.AddDisabledItem(label);
                return;
            }

            AddCompareItem(menu, armed, key, value, compare);
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
            var height = HelpHeight(message, width);

            EditorGUI.LabelField(new Rect(x, y, width, height), message, helpStyle);
            y += height;
        }

        private void DrawRecordingOffWarning(float x, ref float y, float width)
        {
            var height = WarningHeight(width);

            EditorGUI.HelpBox(new Rect(x, y, width, height), RecordingOffMessage, MessageType.Warning);
            y += height + RowSpacing;
        }

        private static float WarningHeight(float width)
        {
            return LudiqGUIUtility.GetHelpBoxHeight(RecordingOffMessage, MessageType.Warning, width);
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
            filterStyle = new GUIStyle(EditorStyles.textField) { fontSize = 12 };
            placeholderStyle = new GUIStyle(EditorStyles.label)
            {
                fontSize = 12,
                fontStyle = FontStyle.Italic,
                normal = { textColor = new Color(0.5f, 0.5f, 0.5f, 0.9f) },
            };
        }

        /// <summary>
        /// Row height. Two points above <see cref="EditorGUIUtility.singleLineHeight"/> so the larger text has
        /// somewhere to sit — at the default height a 12pt row reads as cramped.
        /// </summary>
        private static float Line() => EditorGUIUtility.singleLineHeight + 2.0f;

        /// <summary>
        /// Measured rather than assumed: these messages name an agent and explain how to switch to another,
        /// so a fixed two lines clips them at exactly the width a sidebar tends to be.
        /// </summary>
        private float HelpHeight(string message, float width)
        {
            return Mathf.Max(Line(), helpStyle.CalcHeight(new GUIContent(message), Mathf.Max(1.0f, width)));
        }

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

            var onCanvas = context?.graph as BehaviorTreeGraph;

            // A different recording or a different canvas is a different question, and both are cheap to
            // notice. A call site the registry has not grown yet is simply a miss, resolved once.
            if (!ReferenceEquals(assetsFor, recording) || !ReferenceEquals(assetsOnCanvas, onCanvas))
            {
                assetByCallSite.Clear();
                assetsFor = recording;
                assetsOnCanvas = onCanvas;
            }
            else if (assetByCallSite.TryGetValue(callSiteId, out var entry) && entry.TryReuse(out var remembered))
            {
                return remembered;
            }

            var resolved = ResolveAssetForCallSite(callSiteId, recording, onCanvas);
            assetByCallSite[callSiteId] = new Remembered<BehaviorTreeGraphAsset>(resolved);

            return resolved;
        }

        private BehaviorTreeGraphAsset ResolveAssetForCallSite(
            int callSiteId, IBehaviorTreeRecording recording, BehaviorTreeGraph onCanvas)
        {
            var callSites = recording.CallSites;
            BehaviorTreeCallSite? found = null;

            for (int i = 0; i < callSites.Count; i++)
            {
                if (callSites[i].Id != callSiteId) continue;

                found = callSites[i];
                break;
            }

            if (found == null) return null;

            if (FindIn(onCanvas, found.Value.RunNodeGuid) is RunBehaviorTreeGraphNode run &&
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
