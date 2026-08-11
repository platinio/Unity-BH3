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
        /// <summary>
        /// Big enough to read at the zoom levels a graph is actually edited at. The number is the whole point
        /// of the badge, so it loses to nothing else on the node.
        /// </summary>
        private const float BadgeSize = 26.0f;

        /// <summary>Ordinary priority. Muted, because the number is reference information, not an alert.</summary>
        private static readonly Color BadgeColor = new(0.20f, 0.22f, 0.26f, 0.96f);

        /// <summary>
        /// This child sits in a different place left-to-right than its priority says. Amber rather than red:
        /// it is legal and sometimes deliberate, but it is always worth knowing.
        /// </summary>
        private static readonly Color DisagreeingColor = new(0.85f, 0.6f, 0.1f, 0.97f);

        private static readonly Color TextColor = new(0.96f, 0.97f, 0.98f, 1.0f);

        /// <summary>A thin dark ring so the badge reads against a light node header as well as a dark one.</summary>
        private static readonly Color OutlineColor = new(0.06f, 0.07f, 0.09f, 0.9f);

        /// <summary>
        /// Refilled on every call rather than allocated. This runs for every node on every repaint, so a list
        /// per node per frame is the real cost here — not the scan, which is a handful of comparisons.
        /// </summary>
        private static readonly List<BehaviorTreeTransition> Siblings = new();

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

            node.graph.ChildTransitionsInPriorityOrder(parent, Siblings);
            if (Siblings.Count < 2) return;

            int priority = -1;
            bool layoutAgrees = true;

            for (int index = 0; index < Siblings.Count; index++)
            {
                if (Siblings[index].destination == node) priority = index;

                if (index > 0 &&
                    Siblings[index].destination.Position.x < Siblings[index - 1].destination.Position.x)
                {
                    layoutAgrees = false;
                }
            }

            if (priority < 0) return;

            var badge = new Rect(
                nodeRect.xMax - (BadgeSize * 0.6f),
                nodeRect.y - (BadgeSize * 0.4f),
                BadgeSize,
                BadgeSize);

            var previousColor = GUI.color;

            // Ring first, disc on top of it, so the outline reads as a border rather than a halo.
            GUI.color = OutlineColor;
            GUI.DrawTexture(badge, Disc());

            GUI.color = layoutAgrees ? BadgeColor : DisagreeingColor;
            GUI.DrawTexture(Shrink(badge, 2.0f), Disc());

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

        private static Rect Shrink(Rect rect, float by)
        {
            return new Rect(rect.x + by, rect.y + by, rect.width - (by * 2.0f), rect.height - (by * 2.0f));
        }

        private static GUIStyle LabelStyle()
        {
            if (labelStyle != null) return labelStyle;

            labelStyle = new GUIStyle(GUI.skin.label)
            {
                alignment = TextAnchor.MiddleCenter,
                fontSize = 13,
                fontStyle = FontStyle.Bold,
            };

            labelStyle.normal.textColor = TextColor;

            return labelStyle;
        }

        /// <summary>
        /// A white disc with a soft edge, built once and tinted by the caller. White so that
        /// <see cref="GUI.color"/> multiplies to exactly the colour asked for.
        /// </summary>
        private static Texture2D disc;

        private static Texture2D Disc()
        {
            if (disc != null) return disc;

            const int size = 64;
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

                    // One pixel of falloff at the rim, or the circle has stair-stepped edges that read as a
                    // sloppy square at the zoom levels a graph canvas actually sits at.
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
