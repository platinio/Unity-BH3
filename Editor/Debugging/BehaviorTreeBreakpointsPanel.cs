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
    /// Everything currently armed, in one list.
    ///
    /// <para>
    /// Not in the spec's UI line, which is only "right-click → toggle, dot on canvas". It is here because
    /// those two alone leave a breakpoint findable exactly when you do not need to find it: a dot marks a node
    /// that is on screen, and the breakpoint you want to clear is the one inside a sub-tree you closed an hour
    /// ago. Unreal reached the same place — its Blueprint Debugger carries a breakpoint list beside the
    /// per-node toggles, for the same reason.
    /// </para>
    ///
    /// <para>
    /// It is also the only place a variable breakpoint's value match can be typed. A node and a guard are
    /// things you can point at on a canvas; a variable name is not.
    /// </para>
    /// </summary>
    public sealed class BehaviorTreeBreakpointsPanel : ISidebarPanelContent
    {
        private const float Padding = 6.0f;
        private const float RowSpacing = 2.0f;
        private const float ToggleWidth = 18.0f;
        private const float ButtonWidth = 24.0f;
        private const float HitsWidth = 46.0f;

        private string newVariableKey = string.Empty;
        private string newVariableValue = string.Empty;
        private BehaviorTreeVariableCompare newVariableCompare = BehaviorTreeVariableCompare.Equals;

        /// <summary>Which row has its extra controls open. Only one at a time — the sidebar is a narrow column.</summary>
        private BehaviorTreeBreakpoint expanded;

        /// <summary>
        /// The fill behind the row the editor stopped on. The same amber as the canvas ring
        /// (<see cref="BehaviorTreeBreakpointGizmos"/>), because they are two views of one fact and a reader
        /// should not have to learn that twice — the ringed node and the filled row are the same colour on
        /// purpose. Alpha is low enough that the text on top stays readable in both editor skins.
        /// </summary>
        private static readonly Color StoppedFill = new(1.0f, 0.65f, 0.1f, 0.28f);

        private GUIStyle detail;
        private GUIStyle rowLabel;
        private GUIStyle stoppedLabel;
        private GUIStyle warning;

        // Qualified because Unity.VisualScripting has an IGraphContext of its own and both usings are in scope
        // here — the same collision family as BREAK-1's port types and Finding 26's node names.
        public BehaviorTreeBreakpointsPanel(GraphCore.IGraphContext context)
        {
            this.context = context;

            titleContent = new GUIContent("Breakpoints", BoltCore.Icons.errorState?[IconSize.Small]);
        }

        public GraphCore.IGraphContext context { get; }

        public object sidebarControlHint => typeof(BehaviorTreeBreakpointsPanel);

        public GUIContent titleContent { get; }

        public Vector2 minSize => new(300.0f, 160.0f);

        public void OnGUI(Rect position)
        {
            EnsureStyles();

            var x = position.x + Padding;
            var width = position.width - (Padding * 2.0f);
            var y = position.y + Padding;

            y = DrawControls(x, y, width);
            y = DrawStatus(x, y, width);
            y = DrawList(x, y, width);

            DrawVariableComposer(x, y, width);
        }

        public float GetHeight(float width)
        {
            EnsureStyles();

            var inner = width - (Padding * 2.0f);

            // controls row, status line, the "add a variable breakpoint" composer and its label
            var height = (Padding * 2.0f) + ((Line() + RowSpacing) * 4.0f);

            height += RecordingIsOff ? HelpHeight(inner) + RowSpacing : 0.0f;

            if (BehaviorTreeBreakpoints.All.Count == 0) return height + HelpHeight(inner);

            foreach (var breakpoint in BehaviorTreeBreakpoints.All)
            {
                height += Line() + RowSpacing;

                if (breakpoint.Diagnostic != null) height += Line() + RowSpacing;

                if (!ReferenceEquals(expanded, breakpoint)) continue;

                // the hit-count row, plus the operator row and its numeric-only hint for a variable
                height += Line() + RowSpacing;

                if (breakpoint.Kind != BehaviorTreeBreakpointKind.Variable) continue;

                height += Line() + RowSpacing;

                if (BehaviorTreeBreakpoint.IsOrdering(breakpoint.Compare)) height += Line() + RowSpacing;
            }

            return height;
        }

        /// <summary>
        /// Whether the thing breakpoints ride on has been switched off.
        ///
        /// <para>
        /// Worth a line on screen rather than a paragraph in a doc comment. Breakpoints are matched inside the
        /// flight recorder, so turning recording off turns them off too, and the failure is silent and
        /// bewildering: every breakpoint still listed, still ticked, and nothing ever stopping.
        /// </para>
        /// </summary>
        private static bool RecordingIsOff => !BehaviorTreeFlightRecorders.GloballyEnabled;

        private float DrawControls(float x, float y, float width)
        {
            var row = new Rect(x, y, width, Line());
            var clearWidth = 70.0f;
            var toggleWidth = Mathf.Max(60.0f, width - clearWidth - 4.0f);

            var enabled = GUI.Toggle(
                new Rect(row.x, row.y, toggleWidth, row.height),
                BehaviorTreeBreakpoints.GloballyEnabled,
                new GUIContent(
                    "Breakpoints enabled",
                    "Off keeps every breakpoint armed but stops any of them firing."));

            if (enabled != BehaviorTreeBreakpoints.GloballyEnabled) BehaviorTreeBreakpoints.GloballyEnabled = enabled;

            using (new EditorGUI.DisabledScope(BehaviorTreeBreakpoints.All.Count == 0))
            {
                if (GUI.Button(
                        new Rect(row.xMax - clearWidth, row.y, clearWidth, row.height),
                        "Clear All",
                        EditorStyles.miniButton))
                {
                    BehaviorTreeBreakpointStore.Clear();
                }
            }

            return y + row.height + RowSpacing;
        }

        private float DrawStatus(float x, float y, float width)
        {
            if (RecordingIsOff)
            {
                var message =
                    "Recording is off, so nothing can fire. Breakpoints are matched inside the flight " +
                    "recorder — turn BehaviorTreeFlightRecorders.GloballyEnabled back on.";

                var height = HelpHeight(width);
                EditorGUI.HelpBox(new Rect(x, y, width, height), message, MessageType.Warning);
                y += height + RowSpacing;
            }

            var stopped = BehaviorTreeBreakpointResponder.Current;
            var line = new Rect(x, y, width, Line());

            if (stopped.HasValue)
            {
                // Says which agent and which tick, which the row below cannot: the row identifies the
                // breakpoint, this identifies the moment. With several agents on one tree they are different
                // questions.
                EditorGUI.DrawRect(line, StoppedFill);
                EditorGUI.LabelField(line, $"▶ {stopped.Value.Describe()}", stoppedLabel);
            }
            else
            {
                // Which agent, not just "running". A breakpoint that did not fire because a different zombie
                // tripped it is the most confusing thing this feature can do, and the only cure is saying up
                // front whose events are being watched. Not a picker — it follows whatever the window is
                // already pointing at.
                var agent = BehaviorTreeBreakpointResponder.FilteredTo;

                EditorGUI.LabelField(
                    line,
                    agent != null
                        ? $"Watching {agent.name}. Breakpoints fire for this agent only."
                        : "No agent resolved — breakpoints fire for any agent running these trees.",
                    detail);
            }

            return y + Line() + RowSpacing;
        }

        private float DrawList(float x, float y, float width)
        {
            if (BehaviorTreeBreakpoints.All.Count == 0)
            {
                var height = HelpHeight(width);

                EditorGUI.HelpBox(
                    new Rect(x, y, width, height),
                    "Nothing armed. Right-click a node or a guard on the canvas, or add a variable below.",
                    MessageType.Info);

                return y + height + RowSpacing;
            }

            // Copied because a row's delete button mutates the store mid-draw, and IMGUI would be iterating the
            // list it just had an element pulled out of.
            var snapshot = new List<BehaviorTreeBreakpoint>(BehaviorTreeBreakpoints.All);

            foreach (var breakpoint in snapshot)
            {
                y = DrawRow(x, y, width, breakpoint);
            }

            return y;
        }

        /// <summary>Whether the editor is stopped on this one right now.</summary>
        private static bool IsStopped(BehaviorTreeBreakpoint breakpoint)
        {
            var stopped = BehaviorTreeBreakpointResponder.Current;

            return stopped.HasValue && ReferenceEquals(stopped.Value.Breakpoint, breakpoint);
        }

        private float DrawRow(float x, float y, float width, BehaviorTreeBreakpoint breakpoint)
        {
            var row = new Rect(x, y, width, Line());
            var stopped = IsStopped(breakpoint);

            // The whole row is repainted, not just the text. With six breakpoints armed and half of them
            // conditional, working out which one stopped the editor meant reading every row and comparing hit
            // counts — and a variable breakpoint often rings no node on the canvas either, because its writer
            // is a sensor rather than something on screen. A filled row is the one thing you cannot miss.
            if (stopped)
            {
                EditorGUI.DrawRect(new Rect(row.x - 2.0f, row.y - 1.0f, row.width + 4.0f, row.height + 2.0f), StoppedFill);
            }

            var toggle = new Rect(row.x, row.y, ToggleWidth, row.height);
            var remove = new Rect(row.xMax - ButtonWidth, row.y, ButtonWidth, row.height);
            var jump = new Rect(remove.x - ButtonWidth - 2.0f, row.y, ButtonWidth, row.height);
            var hits = new Rect(jump.x - HitsWidth - 2.0f, row.y, HitsWidth, row.height);
            var label = new Rect(toggle.xMax, row.y, Mathf.Max(20.0f, hits.x - toggle.xMax - 2.0f), row.height);

            var enabled = GUI.Toggle(toggle, breakpoint.Enabled, GUIContent.none);
            if (enabled != breakpoint.Enabled) BehaviorTreeBreakpointStore.SetEnabled(breakpoint, enabled);

            var text = stopped ? $"▶  {breakpoint.Describe()}" : breakpoint.Describe();
            var tooltip = string.IsNullOrEmpty(breakpoint.TreeName)
                ? breakpoint.Describe()
                : $"{breakpoint.Describe()}\nin {breakpoint.TreeName}";

            using (new EditorGUI.DisabledScope(!breakpoint.Enabled))
            {
                // The label is the fold: clicking it opens the operator and hit-count controls, which are only
                // wanted occasionally and would otherwise take three rows on every breakpoint in a column
                // narrow enough to be a sidebar.
                if (GUI.Button(label, new GUIContent(text, tooltip), stopped ? stoppedLabel : rowLabel))
                {
                    expanded = ReferenceEquals(expanded, breakpoint) ? null : breakpoint;
                }
            }

            GUI.Label(hits, HitsLabel(breakpoint), stopped ? stoppedLabel : detail);

            using (new EditorGUI.DisabledScope(!CanSelect(breakpoint)))
            {
                if (GUI.Button(jump, new GUIContent("→", "Select it on the canvas"), EditorStyles.miniButton))
                {
                    Select(breakpoint.TargetGuid);
                }
            }

            if (GUI.Button(remove, new GUIContent("×", "Remove this breakpoint"), EditorStyles.miniButton))
            {
                if (ReferenceEquals(expanded, breakpoint)) expanded = null;

                BehaviorTreeBreakpointStore.Remove(breakpoint);
                return y + row.height + RowSpacing;
            }

            y += row.height + RowSpacing;

            if (breakpoint.Diagnostic != null)
            {
                var note = new Rect(x + IndentWidth(), y, width - IndentWidth(), Line());
                GUI.Label(note, new GUIContent($"⚠ {breakpoint.Diagnostic}", breakpoint.Diagnostic), warning);
                y += Line() + RowSpacing;
            }

            if (ReferenceEquals(expanded, breakpoint)) y = DrawRowControls(x + IndentWidth(), y, width - IndentWidth(), breakpoint);

            return y;
        }

        /// <summary>
        /// Matches and fires shown separately once they can differ.
        ///
        /// <para>
        /// "0×" beside a breakpoint that has matched twelve times looks exactly like one that does not work.
        /// The gap is the whole point of a hit-count condition, so it has to be visible while it is being
        /// counted down.
        /// </para>
        /// </summary>
        private static string HitsLabel(BehaviorTreeBreakpoint breakpoint)
        {
            if (breakpoint.HitCount > 0) return $"{breakpoint.HitCount}×";

            return breakpoint.MatchCount > 0 ? $"0/{breakpoint.MatchCount}" : "—";
        }

        /// <summary>The per-row controls: the operator and its operand for a variable, and the hit count for any kind.</summary>
        private float DrawRowControls(float x, float y, float width, BehaviorTreeBreakpoint breakpoint)
        {
            if (breakpoint.Kind == BehaviorTreeBreakpointKind.Variable)
            {
                var row = new Rect(x, y, width, Line());
                var operatorWidth = Mathf.Min(110.0f, width * 0.45f);

                var compare = (BehaviorTreeVariableCompare)EditorGUI.EnumPopup(
                    new Rect(row.x, row.y, operatorWidth, row.height), breakpoint.Compare);

                var valueRect = new Rect(row.x + operatorWidth + 4.0f, row.y, Mathf.Max(30.0f, width - operatorWidth - 4.0f), row.height);

                // Disabled rather than hidden for Changed: the field vanishing as you pick "any write" reads
                // as the panel losing what you typed.
                using (new EditorGUI.DisabledScope(compare == BehaviorTreeVariableCompare.Changed))
                {
                    var value = EditorGUI.TextField(valueRect, breakpoint.ExpectedValue ?? string.Empty);

                    if (compare != breakpoint.Compare || value != (breakpoint.ExpectedValue ?? string.Empty))
                    {
                        BehaviorTreeBreakpointStore.SetVariable(breakpoint.VariableKey, value, compare);
                    }
                }

                y += Line() + RowSpacing;

                if (BehaviorTreeBreakpoint.IsOrdering(compare))
                {
                    var hint = new Rect(x, y, width, Line());
                    GUI.Label(hint, "Ordering compares numbers only.", detail);
                    y += Line() + RowSpacing;
                }
            }

            var hitRow = new Rect(x, y, width, Line());
            var occurrence = EditorGUI.IntField(hitRow, "Break on hit #", breakpoint.BreakOnHit);

            if (occurrence != breakpoint.BreakOnHit) BehaviorTreeBreakpointStore.SetBreakOnHit(breakpoint, occurrence);

            return y + Line() + RowSpacing;
        }

        private static float IndentWidth() => 14.0f;

        /// <summary>
        /// The row that arms a variable breakpoint, since there is nothing on the canvas to right-click for
        /// one. Leaving the value blank means any write, which is the common case and so is the default.
        /// </summary>
        private void DrawVariableComposer(float x, float y, float width)
        {
            EditorGUI.LabelField(new Rect(x, y, width, Line()), "Break on a variable write", detail);
            y += Line() + RowSpacing;

            var row = new Rect(x, y, width, Line());
            var addWidth = 44.0f;
            var operatorWidth = Mathf.Min(96.0f, width * 0.3f);
            var fieldWidth = Mathf.Max(30.0f, (row.width - addWidth - operatorWidth - 12.0f) / 2.0f);

            newVariableKey = EditorGUI.TextField(
                new Rect(row.x, row.y, fieldWidth, row.height), newVariableKey);

            newVariableCompare = (BehaviorTreeVariableCompare)EditorGUI.EnumPopup(
                new Rect(row.x + fieldWidth + 4.0f, row.y, operatorWidth, row.height), newVariableCompare);

            using (new EditorGUI.DisabledScope(newVariableCompare == BehaviorTreeVariableCompare.Changed))
            {
                newVariableValue = EditorGUI.TextField(
                    new Rect(row.x + fieldWidth + operatorWidth + 8.0f, row.y, fieldWidth, row.height),
                    newVariableValue);
            }

            using (new EditorGUI.DisabledScope(string.IsNullOrWhiteSpace(newVariableKey)))
            {
                if (GUI.Button(
                        new Rect(row.xMax - addWidth, row.y, addWidth, row.height), "Add", EditorStyles.miniButton))
                {
                    BehaviorTreeBreakpointStore.SetVariable(
                        newVariableKey.Trim(), newVariableValue.Trim(), newVariableCompare);

                    newVariableKey = string.Empty;
                    newVariableValue = string.Empty;
                    GUI.FocusControl(null);
                }
            }
        }

        private bool CanSelect(BehaviorTreeBreakpoint breakpoint)
        {
            return breakpoint.Kind != BehaviorTreeBreakpointKind.Variable && FindOnCanvas(breakpoint.TargetGuid) != null;
        }

        private void Select(Guid guid)
        {
            var node = FindOnCanvas(guid);
            if (node != null) context.selection.Select(node);
        }

        private BehaviorTreeNode FindOnCanvas(Guid guid)
        {
            if (guid == Guid.Empty || context?.graph is not BehaviorTreeGraph graph) return null;

            foreach (var node in graph.Nodes)
            {
                if (node != null && node.guid == guid) return node;
            }

            return null;
        }

        /// <summary>
        /// Sized off <see cref="EditorStyles.label"/> rather than <c>miniLabel</c>, which is 9pt and was too
        /// small to scan — this panel is read while the editor is stopped and you are trying to work out what
        /// happened, not glanced at.
        /// </summary>
        private void EnsureStyles()
        {
            if (detail != null) return;

            detail = new GUIStyle(EditorStyles.label) { alignment = TextAnchor.MiddleLeft, fontSize = 12 };

            rowLabel = new GUIStyle(detail) { alignment = TextAnchor.MiddleLeft };

            // Bold as well as filled, so the stopped row survives a screenshot, a colour-blind reader and a
            // theme that renders the fill weaker than it does here.
            stoppedLabel = new GUIStyle(rowLabel) { fontStyle = FontStyle.Bold };

            warning = new GUIStyle(EditorStyles.label)
            {
                wordWrap = true,
                fontSize = 11,
                normal = { textColor = new Color(0.9f, 0.6f, 0.15f) },
            };
        }

        private static float Line() => EditorGUIUtility.singleLineHeight;

        private static float HelpHeight(float width) => EditorGUIUtility.singleLineHeight * 2.5f;
    }
}
