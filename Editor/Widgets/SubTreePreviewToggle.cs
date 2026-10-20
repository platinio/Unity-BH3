using UnityEngine;

namespace ArcaneOnyx.BehaviorTree
{
    /// <summary>
    /// The button that collapses a sub-tree node's preview to a plain box and expands it again.
    /// </summary>
    public static class SubTreePreviewToggle
    {
        /// <summary>
        /// Big enough to hit at the zoom levels a graph is edited at, and small enough to sit inside the
        /// padding the preview frame already leaves empty at its corner.
        /// </summary>
        private const float ButtonSize = 22.0f;

        private const float InsetFromTheLeftEdge = 4.0f;

        private const float InsetFromTheTopEdge = 14.0f;

        private const float GlyphThickness = 3.0f;

        private const float GlyphLength = 12.0f;

        private const float OutlineThickness = 2.0f;

        private static readonly Color ButtonColor = new(0.20f, 0.22f, 0.26f, 0.96f);

        /// <summary>A dark ring, so the button reads against a pale node header as well as a dark one.</summary>
        private static readonly Color OutlineColor = new(0.06f, 0.07f, 0.09f, 0.9f);

        private static readonly Color GlyphColor = new(0.96f, 0.97f, 0.98f, 1.0f);

        /// <summary>
        /// Carries the hover text. Reused rather than allocated: this runs for every sub-tree node on every
        /// repaint, and <c>GUI.Label</c> reads the content and keeps nothing.
        /// </summary>
        private static readonly GUIContent tooltipContent = new GUIContent();

        private const string ExpandTooltip = "Expand this sub-tree's preview";

        private const string CollapseTooltip = "Collapse this sub-tree to a named box";

        public static Rect ButtonOn(Rect nodeRect) =>
            new Rect(
                nodeRect.x + InsetFromTheLeftEdge,
                nodeRect.y + InsetFromTheTopEdge,
                ButtonSize,
                ButtonSize);

        public static bool IsHit(Rect nodeRect, RunBehaviorTreeGraphNode node, Vector2 mousePosition) =>
            CanBeToggled(node) && ButtonOn(nodeRect).Contains(mousePosition);

        /// <summary>
        /// Draws a minus while the preview is showing and a plus while it is collapsed: the glyph says what
        /// the click will do, not what the node is.
        /// </summary>
        public static void Draw(Rect nodeRect, RunBehaviorTreeGraphNode node)
        {
            if (!CanBeToggled(node)) return;

            var button = ButtonOn(nodeRect);
            var previousColor = GUI.color;

            // Ring first, face on top of it, so the outline reads as a border rather than a halo.
            GUI.color = OutlineColor;
            GUI.DrawTexture(button, Texture2D.whiteTexture);

            GUI.color = ButtonColor;
            GUI.DrawTexture(Shrink(button, OutlineThickness), Texture2D.whiteTexture);

            GUI.color = GlyphColor;
            GUI.DrawTexture(HorizontalBarOf(button), Texture2D.whiteTexture);

            if (node.IsSubTreePreviewCollapsed)
            {
                GUI.DrawTexture(VerticalBarOf(button), Texture2D.whiteTexture);
            }

            GUI.color = previousColor;

            tooltipContent.tooltip = node.IsSubTreePreviewCollapsed ? ExpandTooltip : CollapseTooltip;
            GUI.Label(button, tooltipContent);
        }

        /// <summary>A node with no branch referenced has no preview either way, so it gets no button.</summary>
        private static bool CanBeToggled(RunBehaviorTreeGraphNode node) =>
            node != null && node.BehaviorTreeGraphAsset != null;

        private static Rect HorizontalBarOf(Rect button) =>
            new Rect(
                button.center.x - (GlyphLength / 2.0f),
                button.center.y - (GlyphThickness / 2.0f),
                GlyphLength,
                GlyphThickness);

        private static Rect VerticalBarOf(Rect button) =>
            new Rect(
                button.center.x - (GlyphThickness / 2.0f),
                button.center.y - (GlyphLength / 2.0f),
                GlyphThickness,
                GlyphLength);

        private static Rect Shrink(Rect rect, float by) =>
            new Rect(rect.x + by, rect.y + by, rect.width - (by * 2.0f), rect.height - (by * 2.0f));
    }
}
