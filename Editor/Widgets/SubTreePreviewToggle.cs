using UnityEngine;

namespace ArcaneOnyx.BehaviorTree
{
    public static class SubTreePreviewToggle
    {
        private const float ButtonSize = 26.0f;

        private const float GapAfterTheButton = 10.0f;

        private const float OutlineThickness = 2.0f;

        private const float GlyphThickness = 3.0f;

        private const float GlyphLength = 12.0f;

        private static readonly Color ButtonColor = new(0.20f, 0.22f, 0.26f, 0.96f);

        private static readonly Color OutlineColor = new(0.06f, 0.07f, 0.09f, 0.9f);

        private static readonly Color GlyphColor = new(0.96f, 0.97f, 0.98f, 1.0f);

        private const string ExpandTooltip = "Expand this sub-tree's preview";

        private const string CollapseTooltip = "Collapse this sub-tree to a named box";

        /// <summary>Reused rather than allocated per repaint.</summary>
        private static readonly GUIContent tooltipContent = new();

        public static Rect ButtonOn(Rect nodeRect) =>
            CentredOn(nodeRect.position, ButtonSize);

        public static bool IsShownOn(BehaviorTreeNode node) =>
            node is RunBehaviorTreeGraphNode runNode && RunsABranch(runNode);

        /// <summary>
        /// How much of a node's top-left corner the button claims, so another marker drawn there can start
        /// clear of it.
        /// </summary>
        public static float RoomTakenOn(BehaviorTreeNode node) =>
            IsShownOn(node) ? (ButtonSize / 2.0f) + GapAfterTheButton : 0.0f;

        /// <summary>A canvas offers a widget only the events landing in its hot area.</summary>
        public static Rect HotAreaFor(Rect nodeRect, BehaviorTreeNode node) =>
            IsShownOn(node) ? Union(nodeRect, ButtonOn(nodeRect)) : nodeRect;

        public static bool IsHit(Rect nodeRect, RunBehaviorTreeGraphNode node, Vector2 mousePosition) =>
            RunsABranch(node) && ButtonOn(nodeRect).Contains(mousePosition);

        /// <summary>The glyph says what the click will do, not what the node is.</summary>
        public static bool OffersToExpand(RunBehaviorTreeGraphNode node) =>
            node.IsSubTreePreviewCollapsed;

        public static void Draw(Rect nodeRect, RunBehaviorTreeGraphNode node)
        {
            if (!RunsABranch(node)) return;

            var button = ButtonOn(nodeRect);

            DrawFace(button);
            DrawGlyph(button, node);
            DrawHoverText(button, OffersToExpand(node) ? ExpandTooltip : CollapseTooltip);
        }

        private static void DrawFace(Rect button)
        {
            Fill(button, OutlineColor);
            Fill(Inset(button, OutlineThickness), ButtonColor);
        }

        private static void DrawGlyph(Rect button, RunBehaviorTreeGraphNode node)
        {
            Fill(HorizontalBarOf(button), GlyphColor);

            if (OffersToExpand(node)) Fill(VerticalBarOf(button), GlyphColor);
        }

        /// <summary>A label is the only thing IMGUI will attach hover text to, and this button is textures.</summary>
        private static void DrawHoverText(Rect button, string text)
        {
            tooltipContent.tooltip = text;
            GUI.Label(button, tooltipContent);
        }

        private static void Fill(Rect rect, Color color)
        {
            var previousColor = GUI.color;

            GUI.color = color;
            GUI.DrawTexture(rect, Texture2D.whiteTexture);

            GUI.color = previousColor;
        }

        private static bool RunsABranch(RunBehaviorTreeGraphNode node) =>
            node != null && node.BehaviorTreeGraphAsset != null;

        private static Rect CentredOn(Vector2 point, float size) =>
            new Rect(point.x - (size / 2.0f), point.y - (size / 2.0f), size, size);

        private static Rect HorizontalBarOf(Rect button) =>
            CentredOn(button.center, GlyphLength, GlyphThickness);

        private static Rect VerticalBarOf(Rect button) =>
            CentredOn(button.center, GlyphThickness, GlyphLength);

        private static Rect CentredOn(Vector2 point, float width, float height) =>
            new Rect(point.x - (width / 2.0f), point.y - (height / 2.0f), width, height);

        private static Rect Inset(Rect rect, float pixels) =>
            new Rect(
                rect.x + pixels,
                rect.y + pixels,
                rect.width - (pixels * 2.0f),
                rect.height - (pixels * 2.0f));

        private static Rect Union(Rect first, Rect second) =>
            Rect.MinMaxRect(
                Mathf.Min(first.xMin, second.xMin),
                Mathf.Min(first.yMin, second.yMin),
                Mathf.Max(first.xMax, second.xMax),
                Mathf.Max(first.yMax, second.yMax));
    }
}
