using System.Collections.Generic;
using UnityEngine;

namespace ArcaneOnyx.BehaviorTree
{
    /// <summary>
    /// The execution-order number on each child of a composite, and the warning when the picture disagrees
    /// with it.
    ///
    /// <para>
    /// Priority is the serialized transition index, not canvas position. That is the right truth to store —
    /// tidying a layout no longer changes what an agent does — but it costs the designer the one cue they had,
    /// because previously "leftmost" and "first" were the same statement and now they need not be. The badge
    /// puts the number back on the canvas so effective priority never has to be inferred.
    /// </para>
    ///
    /// <para>
    /// Numbered from 1, matching how Unreal labels execution order and how a designer says "the second
    /// branch". The stored index is 0-based; only this label is not.
    /// </para>
    /// </summary>
    public static class BehaviorTreePriorityBadge
    {
        private const float BadgeSize = 16.0f;

        /// <summary>Ordinary priority. Muted, because the number is reference information, not an alert.</summary>
        private static readonly Color BadgeColor = new(0.22f, 0.24f, 0.28f, 0.92f);

        /// <summary>
        /// This child sits in a different place left-to-right than its priority says. Amber rather than red:
        /// it is legal and sometimes deliberate, but it is always worth knowing.
        /// </summary>
        private static readonly Color DisagreeingColor = new(0.85f, 0.6f, 0.1f, 0.95f);

        private static readonly Color TextColor = new(0.92f, 0.93f, 0.95f, 1.0f);

        private static GUIStyle labelStyle;

        /// <summary>
        /// Draws the priority number for a node, if it has one.
        /// <para>
        /// Safe to call for every node on every repaint. Nodes that are not a composite's child return
        /// immediately, and so does the common case of a container with a single child, where a number would
        /// be noise — there is no priority decision to make when there is nothing to be higher priority than.
        /// </para>
        /// </summary>
        public static void Draw(Rect nodeRect, BehaviorTreeNode node)
        {
            if (node?.graph == null) return;

            var parent = ParentOf(node);
            if (parent is not ContainerNode) return;

            var byPriority = node.graph.ChildrenInPriorityOrder(parent);
            if (byPriority.Count < 2) return;

            int priority = byPriority.IndexOf(node);
            if (priority < 0) return;

            var badge = new Rect(
                nodeRect.xMax - (BadgeSize * 0.65f),
                nodeRect.y - (BadgeSize * 0.35f),
                BadgeSize,
                BadgeSize);

            var color = LayoutAgreesWithPriority(byPriority) ? BadgeColor : DisagreeingColor;

            var previousColor = GUI.color;

            GUI.color = color;
            GUI.DrawTexture(badge, Texture2D.whiteTexture);
            GUI.color = previousColor;

            GUI.Label(badge, (priority + 1).ToString(), LabelStyle());
        }

        /// <summary>
        /// The node this one hangs under, or null when nothing points at it.
        /// <para>
        /// Read from the transitions rather than from <see cref="ContainerNode.GetChildren"/>, because that
        /// list is only populated at awake and the canvas is drawn for trees that have never run.
        /// </para>
        /// </summary>
        private static BehaviorTreeNode ParentOf(BehaviorTreeNode node)
        {
            foreach (var transition in node.graph.Transitions)
            {
                if (transition?.destination == node) return transition.source;
            }

            return null;
        }

        /// <summary>
        /// Whether reading these children left to right gives the same order as their priorities.
        /// <para>
        /// They are already in priority order, so the question is only whether that sequence also happens to
        /// ascend in x. A tree generated from code, or one edited outside the canvas, can easily have correct
        /// priorities that read wrong on screen, and that mismatch is what the amber badge reports.
        /// </para>
        /// </summary>
        private static bool LayoutAgreesWithPriority(List<BehaviorTreeNode> byPriority)
        {
            for (int index = 1; index < byPriority.Count; index++)
            {
                if (byPriority[index].Position.x < byPriority[index - 1].Position.x) return false;
            }

            return true;
        }

        private static GUIStyle LabelStyle()
        {
            if (labelStyle != null) return labelStyle;

            labelStyle = new GUIStyle(GUI.skin.label)
            {
                alignment = TextAnchor.MiddleCenter,
                fontSize = 10,
                fontStyle = FontStyle.Bold,
            };

            labelStyle.normal.textColor = TextColor;

            return labelStyle;
        }
    }
}
