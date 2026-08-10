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

        private GUIStyle detail;

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
            height += BehaviorTreeBreakpoints.All.Count == 0
                ? HelpHeight(inner)
                : (Line() + RowSpacing) * BehaviorTreeBreakpoints.All.Count;

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

            EditorGUI.LabelField(
                new Rect(x, y, width, Line()),
                stopped.HasValue ? $"Stopped: {stopped.Value.Describe()}" : "Running.",
                detail);

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

        private float DrawRow(float x, float y, float width, BehaviorTreeBreakpoint breakpoint)
        {
            var row = new Rect(x, y, width, Line());

            var toggle = new Rect(row.x, row.y, ToggleWidth, row.height);
            var remove = new Rect(row.xMax - ButtonWidth, row.y, ButtonWidth, row.height);
            var jump = new Rect(remove.x - ButtonWidth - 2.0f, row.y, ButtonWidth, row.height);
            var hits = new Rect(jump.x - HitsWidth - 2.0f, row.y, HitsWidth, row.height);
            var label = new Rect(toggle.xMax, row.y, Mathf.Max(20.0f, hits.x - toggle.xMax - 2.0f), row.height);

            var enabled = GUI.Toggle(toggle, breakpoint.Enabled, GUIContent.none);
            if (enabled != breakpoint.Enabled) BehaviorTreeBreakpointStore.SetEnabled(breakpoint, enabled);

            var text = breakpoint.Describe();
            var tooltip = string.IsNullOrEmpty(breakpoint.TreeName) ? text : $"{text}\nin {breakpoint.TreeName}";

            using (new EditorGUI.DisabledScope(!breakpoint.Enabled))
            {
                GUI.Label(label, new GUIContent(text, tooltip), detail);
            }

            GUI.Label(hits, breakpoint.HitCount == 0 ? "—" : $"{breakpoint.HitCount}×", detail);

            using (new EditorGUI.DisabledScope(!CanSelect(breakpoint)))
            {
                if (GUI.Button(jump, new GUIContent("→", "Select it on the canvas"), EditorStyles.miniButton))
                {
                    Select(breakpoint.TargetGuid);
                }
            }

            if (GUI.Button(remove, new GUIContent("×", "Remove this breakpoint"), EditorStyles.miniButton))
            {
                BehaviorTreeBreakpointStore.Remove(breakpoint);
            }

            return y + row.height + RowSpacing;
        }

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
            var fieldWidth = Mathf.Max(40.0f, (row.width - addWidth - 8.0f) / 2.0f);

            newVariableKey = EditorGUI.TextField(new Rect(row.x, row.y, fieldWidth, row.height), newVariableKey);

            newVariableValue = EditorGUI.TextField(
                new Rect(row.x + fieldWidth + 4.0f, row.y, fieldWidth, row.height), newVariableValue);

            using (new EditorGUI.DisabledScope(string.IsNullOrWhiteSpace(newVariableKey)))
            {
                if (GUI.Button(
                        new Rect(row.xMax - addWidth, row.y, addWidth, row.height), "Add", EditorStyles.miniButton))
                {
                    BehaviorTreeBreakpointStore.SetVariable(newVariableKey.Trim(), newVariableValue.Trim());
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

        private void EnsureStyles()
        {
            detail ??= new GUIStyle(EditorStyles.miniLabel);
        }

        private static float Line() => EditorGUIUtility.singleLineHeight;

        private static float HelpHeight(float width) => EditorGUIUtility.singleLineHeight * 2.5f;
    }
}
