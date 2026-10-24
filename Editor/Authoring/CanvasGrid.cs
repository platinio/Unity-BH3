using UnityEngine;
using GraphGUI = ArcaneOnyx.GraphCore.GraphGUI;

namespace ArcaneOnyx.BehaviorTree.Authoring
{
    /// <summary>
    /// The grid the canvas snaps a dragged node to, for code that places nodes. A node placed off it jumps
    /// the first time somebody nudges it.
    /// </summary>
    public static class CanvasGrid
    {
        public const float Spacing = GraphGUI.Styles.minorGridSpacing;

        private const float FloatingPointSlack = 0.001f;

        /// <summary>Where a drag would put a node released here.</summary>
        public static Vector2 Nearest(Vector2 position) => GraphGUI.SnapToGrid(position);

        /// <summary>
        /// The first grid line at or after <paramref name="position"/>. A layout rounds this way so that a gap
        /// it measured can only grow, never close.
        /// </summary>
        public static float RoundUp(float position) =>
            Mathf.Ceil(position / Spacing - FloatingPointSlack) * Spacing;

        public static bool IsOnTheGrid(Vector2 position) =>
            Vector2.Distance(position, Nearest(position)) < FloatingPointSlack;
    }
}
