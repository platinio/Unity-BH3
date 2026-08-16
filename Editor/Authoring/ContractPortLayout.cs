using System.Collections.Generic;
using UnityEngine;

namespace ArcaneOnyx.BehaviorTree.Authoring
{
    /// <summary>
    /// Sizes a node to the ports it declares.
    ///
    /// <para>
    /// <b>Why this exists.</b> Nothing in BH3 overrides <c>StartingSize</c>, so every node is created at
    /// <c>BaseGraphNode</c>'s 150x100 and stays there. A node whose ports come from a contract — a sub-tree
    /// with six parameters, or a Function-backed variable — is therefore drawn at a size chosen before
    /// anyone knew how many ports it would have. The canvas does not grow to fit; it squeezes, clamping the
    /// spacing between ports so they all fit inside the box
    /// (<c>BehaviorTreeNodeElementWidget.DrawPortColumn</c>). This is a defect that predates Functions: it
    /// is live today on sub-trees.
    /// </para>
    ///
    /// <para>
    /// <b>When it runs, and why that is the whole design.</b> Only on a contract change — assigning a
    /// Function, or refreshing either node's parameters. Deliberately never from the draw path, never on
    /// selection, and never on load. The size it writes is exact rather than grow-only (the tool owner's
    /// call), which is safe precisely because the trigger is a discrete authoring action: an exact fit
    /// stamped every frame would fight an author who is dragging a node's edge, where one stamped when the
    /// contract changes is a tidy-up they asked for.
    /// </para>
    ///
    /// <para>
    /// <b>Why not the alternatives.</b> Overriding <c>StartingSize</c> is read once at creation, so it never
    /// reacts to a contract change, and runtime code cannot measure text. Growing the drawn box at draw time
    /// desynchronises the picture from <c>Position</c>, which is what hit-testing and connection routing
    /// read.
    /// </para>
    /// </summary>
    public static class ContractPortLayout
    {
        /// <summary>
        /// The header allowance <c>BehaviorTreeNodeElementWidget.CachePosition</c> adds above the port
        /// section. Read from the widget rather than copied: this was the one number here that could not be
        /// referenced, until the widget's bare literal became a named constant.
        /// </summary>
        private static float HeaderAndFooterHeight => BehaviorTreeNodeElementWidget.HEADER_AND_FOOTER_HEIGHT;

        /// <summary>Read from the widget, not copied: the drawn spacing and the computed size must agree.</summary>
        private static float SpaceBetweenPorts => BehaviorTreeNodeElementWidget.Styles.spaceBetweenPorts;

        /// <summary>
        /// Handle, edge spacing and the gap between label and node edge — doubled, because a row has an
        /// input side and an output side.
        /// </summary>
        private static float PortChromeWidth =>
            2.0f * (PortWidget<ValueInput>.Styles.handleSize.x
                    + PortWidget<ValueInput>.Styles.spaceBetweenEdgeAndHandle
                    + PortWidget<ValueInput>.Styles.spaceAfterEdge
                    + PortWidget<ValueInput>.Styles.spaceBetweenIconAndLabel);

        /// <summary>What a node is never made narrower than, matching <c>BaseGraphNode.StartingSize</c>.</summary>
        private const float MinimumWidth = 150.0f;

        private const float MinimumHeight = 100.0f;

        /// <summary>
        /// Writes the node's size from the ports it currently declares. Returns the rect it wrote, so a
        /// command can report it rather than an author having to look.
        /// </summary>
        public static Rect ResizeToFitPorts(BehaviorTreeNode node)
        {
            if (node == null) return default;

            var rowHeight = UnityEditor.EditorGUIUtility.singleLineHeight;

            var inputRows = CountAndMeasure(node.valueInputs, node.controlInputs, out var widestInput);
            var outputRows = CountAndMeasure(node.valueOutputs, node.controlOutputs, out var widestOutput);

            var rows = Mathf.Max(inputRows, outputRows);

            var height = HeaderAndFooterHeight;
            if (rows > 0) height += rows * rowHeight + (rows - 1) * SpaceBetweenPorts;

            // Both columns are drawn on the same row, so the width has to hold the widest of each side at
            // once rather than the widest label overall.
            var width = widestInput + widestOutput + PortChromeWidth;

            var position = node.Position;
            position.width = Mathf.Max(MinimumWidth, width);
            position.height = Mathf.Max(MinimumHeight, height);
            node.Position = position;

            return position;
        }

        /// <summary>
        /// How many port rows one side has, and how wide its widest label draws.
        /// <para>
        /// Measured with <c>GUI.skin</c>, which is why this lives in the editor assembly: only editor code
        /// can ask how wide a string renders, and a port label's width is the whole reason a node with a
        /// long parameter name needs to be wider than one without.
        /// </para>
        /// </summary>
        private static int CountAndMeasure(
            IEnumerable<ValueInput> valuePorts, IEnumerable<ControlInput> controlPorts, out float widest)
        {
            widest = 0.0f;
            var rows = 0;

            foreach (var port in valuePorts)
            {
                rows++;
                widest = Mathf.Max(widest, MeasureLabel(port.key));
            }

            foreach (var port in controlPorts)
            {
                rows++;
                widest = Mathf.Max(widest, MeasureLabel(port.key));
            }

            return rows;
        }

        private static int CountAndMeasure(
            IEnumerable<ValueOutput> valuePorts, IEnumerable<ControlOutput> controlPorts, out float widest)
        {
            widest = 0.0f;
            var rows = 0;

            foreach (var port in valuePorts)
            {
                rows++;
                widest = Mathf.Max(widest, MeasureLabel(port.key));
            }

            foreach (var port in controlPorts)
            {
                rows++;
                widest = Mathf.Max(widest, MeasureLabel(port.key));
            }

            return rows;
        }

        /// <summary>
        /// Approximate width of one character, used when text cannot be measured for real.
        /// Deliberately generous: a node slightly too wide is untidy, one too narrow clips its labels.
        /// </summary>
        private const float FallbackCharacterWidth = 8.0f;

        /// <summary>
        /// How wide a port label draws.
        ///
        /// <para>
        /// <c>GUI.skin</c> throws outside <c>OnGUI</c>, and the callers that matter most —
        /// <c>fn_refresh_ports</c> and <c>bt_refresh_sub_tree_ports</c> — are pipeline commands that never
        /// run inside one. So the real measurement is attempted and an estimate is used when it is not
        /// available, rather than the resize being unavailable exactly where it is most useful. The estimate
        /// only affects width, and only until the next refresh made from the canvas.
        /// </para>
        /// </summary>
        private static float MeasureLabel(string label)
        {
            if (string.IsNullOrEmpty(label)) return 0.0f;

            try
            {
                return GUI.skin.label.CalcSize(new GUIContent(label)).x;
            }
            catch (System.ArgumentException)
            {
                return label.Length * FallbackCharacterWidth;
            }
        }
    }
}
