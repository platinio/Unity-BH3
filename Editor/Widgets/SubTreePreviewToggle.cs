using UnityEngine;

namespace ArcaneOnyx.BehaviorTree
{
    /// <summary>
    /// The button that collapses a sub-tree node's preview to a plain box and expands it again.
    /// </summary>
    public static class SubTreePreviewToggle
    {
        /// <summary>The priority badge's size, so the two markers read as a pair across the node's top.</summary>
        private const float ButtonSize = 26.0f;


        private const float GlyphThickness = 3.0f;

        private const float GlyphLength = 12.0f;

        private const float OutlineThickness = 2.0f;

        private static readonly Color ButtonColor = new(0.20f, 0.22f, 0.26f, 0.96f);

        /// <summary>A dark ring, so the button reads against a pale node header as well as a dark one.</summary>
        private static readonly Color OutlineColor = new(0.06f, 0.07f, 0.09f, 0.9f);

        private static readonly Color GlyphColor = new(0.96f, 0.97f, 0.98f, 1.0f);

        /// <summary>Reused rather than allocated per repaint.</summary>
        private static readonly GUIContent tooltipContent = new();

        private const string ExpandTooltip = "Expand this sub-tree's preview";

        private const string CollapseTooltip = "Collapse this sub-tree to a named box";

        /// <summary>
        /// Centred on the node's top-left corner, so a quarter of it covers the corner and the rest hangs
        /// outside the box.
        /// <para>
        /// Outside rather than inside because a node has almost no room there: the port rows begin a few
        /// pixels below the top edge, and a collapsed node is little else.
        /// </para>
        /// </summary>
        public static Rect ButtonOn(Rect nodeRect) =>
            new Rect(
                nodeRect.x - (ButtonSize / 2.0f),
                nodeRect.y - (ButtonSize / 2.0f),
                ButtonSize,
                ButtonSize);

        /// <summary>
        /// How much room the button takes at a node's top-left corner, so a header drawn in the same band
        /// can start beside it instead of underneath it.
        /// </summary>
        public static float RoomNeededAtTheCorner => ButtonOn(new Rect(0.0f, 0.0f, 0.0f, 0.0f)).xMax;

        /// <summary>
        /// Enough to clear the button and whatever the next marker hangs past the corner by: the breakpoint
        /// dot, the one marker that follows it, is anchored a little left of the node edge itself.
        /// </summary>
        private const float GapAfterTheButton = 10.0f;

        /// <summary>
        /// How much of this node's top-left corner the button claims, so another marker drawn there can
        /// start clear of it. Zero for a node that never shows one, which is every node but this one.
        /// </summary>
        public static float RoomTakenOn(BehaviorTreeNode node) =>
            node is RunBehaviorTreeGraphNode runNode && CanBeToggled(runNode)
                ? RoomNeededAtTheCorner + GapAfterTheButton
                : 0.0f;

        public static bool IsHit(Rect nodeRect, RunBehaviorTreeGraphNode node, Vector2 mousePosition) =>
            CanBeToggled(node) && ButtonOn(nodeRect).Contains(mousePosition);

        /// <summary>
        /// The glyph says what the click will do rather than what the node is, so a collapsed preview
        /// offers a plus and a showing one a minus.
        /// </summary>
        public static bool ShowsPlusGlyph(RunBehaviorTreeGraphNode node) => node.IsSubTreePreviewCollapsed;

        public static void Draw(Rect nodeRect, RunBehaviorTreeGraphNode node)
        {
            if (!CanBeToggled(node)) return;

            var button = ButtonOn(nodeRect);
            var previousColor = GUI.color;

            // Ring first, face on top of it, so the outline reads as a border rather than a halo.
            GUI.color = OutlineColor;
            GUI.DrawTexture(button, Texture2D.whiteTexture);

            GUI.color = ButtonColor;
            GUI.DrawTexture(Inset(button, OutlineThickness), Texture2D.whiteTexture);

            GUI.color = GlyphColor;
            GUI.DrawTexture(HorizontalBarOf(button), Texture2D.whiteTexture);

            if (ShowsPlusGlyph(node))
            {
                GUI.DrawTexture(VerticalBarOf(button), Texture2D.whiteTexture);
            }

            GUI.color = previousColor;

            DrawHoverText(button, ShowsPlusGlyph(node) ? ExpandTooltip : CollapseTooltip);
        }

        /// <summary>
        /// An empty label over the button. The button itself is drawn from textures, and a label is the
        /// only thing IMGUI will attach hover text to.
        /// </summary>
        private static void DrawHoverText(Rect button, string text)
        {
            tooltipContent.tooltip = text;
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

        private static Rect Inset(Rect rect, float pixels) =>
            new Rect(
                rect.x + pixels,
                rect.y + pixels,
                rect.width - (pixels * 2.0f),
                rect.height - (pixels * 2.0f));
    }
}
