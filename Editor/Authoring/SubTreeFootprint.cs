using System;
using ArcaneOnyx.GraphCore;
using Unity.VisualScripting;
using UnityEngine;
using ICanvas = ArcaneOnyx.GraphCore.ICanvas;

namespace ArcaneOnyx.BehaviorTree.Authoring
{
    /// <summary>
    /// How much canvas a sub-tree takes and how big the node that runs it is drawn.
    ///
    /// <para>
    /// A sub-tree node is not the 150x100 box it is created at: the widget redraws it as a frame around the
    /// whole tree it runs, every repaint. Anything that places nodes has to know that before a frame is
    /// drawn — a branch that is a sub-tree can be thousands of pixels wide — so the measuring lives here
    /// rather than inside the drawing code, and the widget and <see cref="BehaviorTreeLayout"/> read the
    /// same answer.
    /// </para>
    /// </summary>
    public static class SubTreeFootprint
    {
        public static readonly Vector2 Padding = new Vector2(50.0f, 50.0f);

        /// <summary>How far the group frame's header band is drawn above the frame's own rect.</summary>
        public const float GroupHeaderLift = 40.0f;

        public static readonly Vector2 EmptyNodeSize = new Vector2(150.0f, 100.0f);

        /// <summary>
        /// The rectangle a sub-tree occupies in its own graph's coordinates, measured the way it is being
        /// drawn right now.
        /// </summary>
        public static Rect BoundsWhileDrawing(ICanvas canvas, BehaviorTreeGraph graph) =>
            Bounds(graph, node => RoomAboveWhileDrawing(canvas, node));

        /// <summary>
        /// The same rectangle, for a caller with no canvas to draw into. A guard is counted at the height
        /// it is given before anything measures its text, which is the shortest it can be: a layout leaves
        /// a generation's worth of room above a node anyway, and the drawn size is written back the first
        /// time the tree is opened.
        /// </summary>
        public static Rect BoundsOf(BehaviorTreeGraph graph) => Bounds(graph, RoomAbove);

        /// <summary>
        /// The band a node is drawn into above its own rect: the guards anchored to it, and a sub-tree's
        /// header. A node placed directly above this one covers it unless the band is left clear.
        /// </summary>
        public static float RoomAboveWhileDrawing(ICanvas canvas, BehaviorTreeNode node) =>
            WithTheGroupHeader(ConditionalExecutionWidget.StackHeightAbove(canvas, node), node);

        public static float RoomAbove(BehaviorTreeNode node) =>
            WithTheGroupHeader(
                GuardIndex.Of(node).Count * (ConditionalExecutionWidget.Height + ConditionalExecutionWidget.Separation),
                node);

        /// <summary>The frame a sub-tree of these bounds is drawn as, anchored where the node already sits.</summary>
        public static Rect BoxAt(Vector2 anchor, Rect bounds) =>
            bounds.size == Vector2.zero
                ? new Rect(anchor, EmptyNodeSize)
                : new Rect(anchor, bounds.size + Padding * 2.0f);

        /// <summary>
        /// Asked by both the widget and <see cref="BehaviorTreeLayout"/>, so that a collapsed node cannot
        /// be drawn one size and laid out at another.
        /// </summary>
        public static Rect DrawnAs(RunBehaviorTreeGraphNode node) =>
            node.DrawsSubTreePreview
                ? BoxAt(node.Position.position, BoundsOf(node.BehaviorTreeGraphAsset.graph))
                : PlainBoxAt(node);

        public static Rect PlainBoxAt(BehaviorTreeNode node) =>
            new Rect(node.Position.position, ContractPortLayout.SizeForPorts(node));

        private static float WithTheGroupHeader(float guards, BehaviorTreeNode node) =>
            node.DrawsSubTreePreview ? guards + GroupHeaderLift : guards;

        private static Rect Bounds(BehaviorTreeGraph graph, Func<BehaviorTreeNode, float> roomAbove)
        {
            if (graph == null) return default;

            float left = float.MaxValue, top = float.MaxValue;
            float right = float.MinValue, bottom = float.MinValue;

            foreach (var element in graph.elements)
            {
                if (!element.DrawInSubTree) continue;
                if (!(element is BehaviorTreeNode node) || element is PlaceHolderNode) continue;
                if (element is ConditionalExecution) continue;

                var drawn = node.Position;

                left = Mathf.Min(left, drawn.xMin);
                right = Mathf.Max(right, drawn.xMax);
                top = Mathf.Min(top, drawn.yMin - roomAbove(node));
                bottom = Mathf.Max(bottom, drawn.yMax);
            }

            if (left > right) return default;

            return Rect.MinMaxRect(left, top, right, bottom);
        }
    }
}
