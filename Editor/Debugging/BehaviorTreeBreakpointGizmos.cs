using ArcaneOnyx.BehaviorTree.Debugging;
using ArcaneOnyx.GraphCore;
using UnityEngine;

namespace ArcaneOnyx.BehaviorTree
{
    /// <summary>
    /// The breakpoint dot on a node, and the ring around the node the editor stopped on.
    ///
    /// <para>
    /// Drawn rather than loaded. The alternative was a texture per state beside the existing
    /// <c>Resources/ExecutionStatus/*.png</c>, and it buys less than it looks like it does: the dot has three
    /// states that differ only in colour, so a texture would have to be tinted at draw time anyway, and an
    /// asset per state is a set of files that can drift out of step with the styling around them. One
    /// generated disc, tinted three ways, is the whole thing.
    /// </para>
    /// </summary>
    public static class BehaviorTreeBreakpointGizmos
    {
        private const float DotSize = 14.0f;

        /// <summary>Solid red, the colour a breakpoint is in every debugger anyone has used.</summary>
        private static readonly Color ArmedColor = new(0.85f, 0.19f, 0.19f, 1.0f);

        /// <summary>Armed but switched off: the same shape, drained, so the row is still findable.</summary>
        private static readonly Color DisabledColor = new(0.55f, 0.55f, 0.55f, 0.65f);

        /// <summary>The node the editor is stopped on.</summary>
        private static readonly Color StoppedColor = new(1.0f, 0.65f, 0.1f, 1.0f);

        private static Texture2D disc;

        /// <summary>
        /// Draws the marker for a node, if it has one. Safe to call for every node on every repaint: the
        /// common answer is "nothing armed anywhere", which costs one dictionary count.
        /// </summary>
        public static void Draw(Rect nodeRect, BehaviorTreeNode node)
        {
            if (node == null) return;

            // The ring goes first so the dot sits on top of it rather than under a three-pixel border.
            if (BehaviorTreeBreakpointResponder.IsStoppedOn(node))
            {
                GraphDrawer.DrawSelectionBox(Inflate(nodeRect, 3.0f), 3, StoppedColor);
            }

            var breakpoint = BreakpointOn(node);
            if (breakpoint == null) return;

            var dot = new Rect(
                nodeRect.x - (DotSize * 0.35f),
                nodeRect.y - (DotSize * 0.35f),
                DotSize,
                DotSize);

            var color = breakpoint.Enabled ? ArmedColor : DisabledColor;

            // Not ghosted with BehaviorTreeScrubOverride.Ghost(). Everything else on the canvas dims while the
            // scrubber is parked in the past, because it is showing history — but a breakpoint is armed now and
            // fading it would say it was armed then, which is a claim about the recording rather than about
            // the tree.
            var previous = GUI.color;
            GUI.color = color;
            GUI.DrawTexture(dot, Disc());
            GUI.color = previous;
        }

        /// <summary>
        /// Whatever is armed on this node, node breakpoint or guard breakpoint.
        ///
        /// <para>
        /// Both are checked because a <see cref="ConditionalExecution"/> is itself a node on the canvas and
        /// gets a guard breakpoint rather than a node one, so looking only at the node map would leave every
        /// guard's dot undrawn.
        /// </para>
        /// </summary>
        private static BehaviorTreeBreakpoint BreakpointOn(BehaviorTreeNode node)
        {
            return node is ConditionalExecution
                ? BehaviorTreeBreakpoints.ForGuard(node.guid)
                : BehaviorTreeBreakpoints.ForNode(node.guid);
        }

        private static Rect Inflate(Rect rect, float by)
        {
            return new Rect(rect.x - by, rect.y - by, rect.width + (by * 2.0f), rect.height + (by * 2.0f));
        }

        /// <summary>
        /// A white disc with a soft edge, built once and tinted by the caller. White so that
        /// <see cref="GUI.color"/> multiplies to exactly the colour asked for.
        /// </summary>
        private static Texture2D Disc()
        {
            if (disc != null) return disc;

            const int size = 32;
            const float radius = size * 0.5f;

            disc = new Texture2D(size, size, TextureFormat.RGBA32, false)
            {
                hideFlags = HideFlags.HideAndDontSave,
                filterMode = FilterMode.Bilinear,
                wrapMode = TextureWrapMode.Clamp,
            };

            var pixels = new Color32[size * size];

            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    var distance = Vector2.Distance(new Vector2(x + 0.5f, y + 0.5f), new Vector2(radius, radius));

                    // One pixel of falloff at the rim. Without it the disc has stair-stepped edges that read as
                    // a sloppy square at the zoom levels a graph canvas actually sits at.
                    var alpha = Mathf.Clamp01(radius - distance);

                    pixels[(y * size) + x] = new Color(1.0f, 1.0f, 1.0f, alpha);
                }
            }

            disc.SetPixels32(pixels);
            disc.Apply();

            return disc;
        }
    }
}
