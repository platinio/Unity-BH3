using System.Collections.Generic;
using ArcaneOnyx.GraphCore;
using Unity.VisualScripting;
using UnityEditor;
using UnityEngine;
using GraphGUI = ArcaneOnyx.GraphCore.GraphGUI;

namespace ArcaneOnyx.BehaviorTree
{
    [Widget(typeof(BehaviorTreeTransition))]
    public class BehaviorTreeTransitionWidget : GraphCore.GraphElementWidget<BehaviorTreeCanvas, BehaviorTreeTransition>
    {
        /// <summary>
        /// Builds the hit-test proxies for this transition's three line segments.
        ///
        /// <para>
        /// They are added to the graph because that is what gets them widgets, and therefore picking — but
        /// they are this widget's state and live for as long as it does, which is why the list is here and
        /// not on the transition. It used to be a serialized field on the transition, so opening a tree
        /// silently added three nodes per transition to a file nobody had edited, and every walk over the
        /// element collection paid for them at runtime forever after.
        /// <see cref="BehaviorTreeGraph.IsPersistentElement"/> is what keeps them out of the asset.
        /// </para>
        ///
        /// <para>
        /// <b>This adds unconditionally, which is only safe because a graph has exactly one canvas.</b>
        /// <c>CanvasProvider</c> is a caching <c>SingleDecoratorProvider</c> keyed on the graph, so one
        /// transition can never have two widgets at once — including in a sub-tree preview, which resolves
        /// widgets through the sub-graph's own cached canvas rather than the parent's. Were that to change,
        /// each canvas would add its own three proxies to the one shared element collection and every line
        /// segment would get duplicate hit targets; the guard to restore then is to skip adding when the
        /// graph already holds placeholders owned by this transition.
        /// </para>
        /// </summary>
        public BehaviorTreeTransitionWidget(BehaviorTreeCanvas canvas, BehaviorTreeTransition element) : base(canvas, element)
        {
            for (int n = 0; n < TRANSITION_SECTION_COUNT; n++)
            {
                var placeholder = new PlaceHolderNode(element);
                graph.elements.Add(placeholder);
                placeHolderNodes.Add(placeholder);
            }
        }

        public override void Dispose()
        {
            // Scaffolding outliving the thing it was scaffolding for would leave invisible, still-selectable
            // nodes on the canvas.
            //
            // Each Remove raises CollectionChanged while the widget provider is freeing widgets, so anything
            // subscribing to that collection has to tolerate being called mid-free. The current subscribers
            // do -- Recollect sets a flag, NodeProblemCache.Invalidate clears a dictionary -- and the canvas
            // unsubscribes Recollect before FreeAll. A subscriber that instead walked the widgets would be
            // reading them as they are torn down.
            foreach (var placeholder in placeHolderNodes) graph.elements.Remove(placeholder);

            placeHolderNodes.Clear();

            base.Dispose();
        }

        public const int TRANSITION_SECTION_COUNT = 3;

        /// <summary>This transition's three line-segment hit targets, in order from source to destination.</summary>
        private readonly List<PlaceHolderNode> placeHolderNodes = new List<PlaceHolderNode>(TRANSITION_SECTION_COUNT);
        
        private Edge sourceEdge;
        private Edge destinationEdge;
        private Rect sourcePosition;

        private Rect destinationPosition;
        private GUIContent label { get; } = new GUIContent();
        
        private Vector2 middle;
        private float targetInnerWidth;
        private float currentInnerWidth;
       
        public override bool canDrag => false;
        protected override bool snapToGrid => false;
        public override bool canDelete => true;
        public override bool canSelect => true;

        public override bool canClip => false;

        public override Rect position
        {
            get => element.Position;
            set => element.Position = value;
        }

        public Edge SourceEdge => sourceEdge;
        public Edge DestinationEdge => destinationEdge;

        public override void HandleInput()
        {
            base.HandleInput();

            // Ask the selection, never PlaceHolderNode.IsSelected. That field is a cache refreshed only when
            // the placeholder's own widget runs HandleInput, so after the canvas clears the selection it can
            // still read true here — and this method would then re-add the transition and every placeholder,
            // refilling the selection the user just cleared. Overlapping lines on a busy node make several
            // transitions do it at once, and the selection becomes impossible to move.
            bool placeHolderNodeIsSelected = false;

            foreach (var placeHolderNode in placeHolderNodes)
            {
                if (selection.Contains(placeHolderNode))
                {
                    placeHolderNodeIsSelected = true;
                    break;
                }
            }

            if (placeHolderNodeIsSelected)
            {
                if (!selection.Contains(element)) selection.Add(element);

                foreach (var placeHolderNode in placeHolderNodes)
                {
                    if (!selection.Contains(placeHolderNode)) selection.Add(placeHolderNode);
                }
            }
        }

        public override void DrawOverlay()
        {
            if (IsDangling) return;

            if (!BehaviorTreeScrubOverride.IsRunning(element.destination, element.destination.IsRunning)) return;
            BehaviorTreeGraphDrawer.DrawTransition(graph, this, new WidgetElementState(), Vector2.zero);
        }

        /// <summary>
        /// Missing an end, so there is nothing to draw a line between and nothing to lay out. One rule, asked
        /// of the canvas rather than restated here, because the canvas is what acts on the answer.
        /// </summary>
        private bool IsDangling => BehaviorTreeCanvas.IsDangling(canvas.graph, element);

        public override void DrawBackground()
        {
            if (IsDangling) return;

            DrawConnection(Vector2.zero);
        }

        public override void CachePositionFirstPass()
        {
            // Calculate the size immediately, because other transitions will rely on it for positioning
            targetInnerWidth = Styles.eventIcon.fixedWidth;

            var labelHeight = EditorGUIUtility.singleLineHeight;

            currentInnerWidth = targetInnerWidth;
            currentInnerWidth = Mathf.Lerp(currentInnerWidth, targetInnerWidth, canvas.repaintDeltaTime * Styles.revealSpeed);

            if (Mathf.Abs(targetInnerWidth - currentInnerWidth) < 1)
            {
                currentInnerWidth = targetInnerWidth;
            }

            var innerWidth = currentInnerWidth;
            var innerHeight = labelHeight;

            var edgeSize = InnerToEdgePosition(new Rect(0, 0, innerWidth, innerHeight)).size;
            var edgeWidth = edgeSize.x;
            var edgeHeight = edgeSize.y;

            element.Position.width = edgeWidth;
            element.Position.height = edgeHeight;
        }
        
        protected Rect EdgeToOuterPosition(Rect position)
        {
            return GraphGUI.GetNodeEdgeToOuterPosition(position, NodeShape.Square);
        }
        
        protected Rect InnerToEdgePosition(Rect position)
        {
            return GraphGUI.GetNodeInnerToEdgePosition(position, NodeShape.Square);
        }
        
        public override void CachePosition()
        {
            var innerWidth = innerPosition.width;
            var edgeWidth = edgePosition.width;
            var edgeHeight = edgePosition.height;
            var labelWidth = Styles.label.CalcSize(label).x;
            var labelHeight = EditorGUIUtility.singleLineHeight;

            // A transition whose source or destination has been deleted has nothing to lay out between, so it
            // sits this frame out. Removing it is the canvas repair's job, not layout's -- see
            // BehaviorTreeCanvas.RemoveDanglingElements. This used to be a bare try/catch around the whole
            // block that deleted the transition from the graph on *any* exception, unrecorded: a transient
            // failure -- a widget mid-rebuild during a reload, a bug in the loop below -- read as "a node was
            // removed" and destroyed authored data the user could not undo. It also mutated graph.elements
            // from inside the canvas's own layout pass, while the canvas was iterating it.
            if (IsDangling) return;

            sourcePosition = canvas.Widget(element.source).position;
            destinationPosition = canvas.Widget(element.destination).position;

            // The same measured stack height the drawer routes lines with — never a guard count times a
            // constant, because guards grow past their minimum height when their ports need the room, and
            // an endpoint cached from the constant lands the line sections apart from each other.
            var guardStackHeight = ConditionalExecutionWidget.StackHeightAbove(canvas, element.destination);

            destinationPosition.height += guardStackHeight;
            destinationPosition.position -= new Vector2(0, guardStackHeight);

            sourcePosition.position += new Vector2(0, guardStackHeight);

            LudiqGUIUtility.ClosestPoints(sourcePosition, destinationPosition, out var sourceClosestPoint, out var destinationClosestPoint);


            sourceEdge = Edge.Bottom;
            destinationEdge = Edge.Top;

            sourceEdgeCenter = sourcePosition.GetEdgeCenter(sourceEdge);
            destinationEdgeCenter = destinationPosition.GetEdgeCenter(destinationEdge);

            // The transitions sharing this pair of nodes, in either direction, in graph order. This used to
            // be found by walking every transition in the graph and testing each one -- per transition, per
            // repaint, so quadratic in the graph's transitions to answer a question whose answer is usually
            // "just me". The index groups them once and re-groups when the graph changes.
            var siblingStateTransitions = TransitionSiblingIndex.Of(canvas.graph, element.source, element.destination);

            var siblingIndex = 0;

            for (var i = 0; i < siblingStateTransitions.Count; i++)
            {
                if (!ReferenceEquals(siblingStateTransitions[i], element)) continue;

                siblingIndex = i;
                break;
            }

            // One common axis for the whole sibling set regardless of any member's inversion, taken from the
            // first of them -- arbitrary, but it has to be the same choice for every sibling or they spread
            // along different lines and cross.
            var transitionAxis = siblingStateTransitions.Count > 0
                ? canvas.Widget<BehaviorTreeTransitionWidget>(siblingStateTransitions[0]).sourceEdge.Normal()
                : Vector2.zero;

            // Fix the edge case where the source and destination perfectly overlap

            if (transitionAxis == Vector2.zero)
            {
                transitionAxis = Vector2.right;
            }

            // Calculate the spread axis and origin for the set of siblings

            var spreadAxis = transitionAxis.Perpendicular1().Abs();
            var spreadOrigin = (sourceEdgeCenter + destinationEdgeCenter) / 2;

            if (element.source == element.destination)
            {
                spreadAxis = Vector2.up;
                spreadOrigin = sourcePosition.GetEdgeCenter(Edge.Bottom) - Vector2.down * 10;
            }

            if (BoltCore.Configuration.developerMode && BoltCore.Configuration.debug)
            {
                Handles.BeginGUI();
                Handles.color = Color.yellow;
                Handles.DrawLine(spreadOrigin + spreadAxis * -1000, spreadOrigin + spreadAxis * 1000);
                Handles.EndGUI();
            }

            // Calculate the offset of the current sibling by iterating over its predecessors

            var spreadOffset = 0f;
            var previousSpreadSize = 0f;

            for (var i = 0; i <= siblingIndex; i++)
            {
                var siblingSize = canvas.Widget<BehaviorTreeTransitionWidget>(siblingStateTransitions[i]).outerPosition.size;
                var siblingSizeProjection = GraphGUI.SizeProjection(siblingSize, spreadOrigin, spreadAxis);
                spreadOffset += previousSpreadSize / 2 + siblingSizeProjection / 2;
                previousSpreadSize = siblingSizeProjection;
            }

            if (element.source != element.destination)
            {
                // Calculate the total spread size to center the sibling set

                var totalSpreadSize = 0f;

                for (var i = 0; i < siblingStateTransitions.Count; i++)
                {
                    var siblingSize = canvas.Widget<BehaviorTreeTransitionWidget>(siblingStateTransitions[i]).outerPosition.size;
                    var siblingSizeProjection = GraphGUI.SizeProjection(siblingSize, spreadOrigin, spreadAxis);
                    totalSpreadSize += siblingSizeProjection;
                }

                spreadOffset -= totalSpreadSize / 2;
            }

            // Finally, calculate the positions

            middle = spreadOrigin + spreadOffset * spreadAxis;

            var edgeX = middle.x - edgeWidth / 2;
            var edgeY = middle.y - edgeHeight / 2;

            position = new Rect
                (
                edgeX,
                edgeY,
                edgeWidth,
                edgeHeight
                ).PixelPerfect();

            var innerX = innerPosition.x;
            var innerY = innerPosition.y;

            if (element.source != element.destination)
            {
                entryEdge = destinationEdge;
                exitEdge = sourceEdge;
            }
            else
            {
                entryEdge = sourceEdge;
                exitEdge = destinationEdge;
            }

            entryEdgeCenter = edgePosition.GetEdgeCenter(entryEdge);
            exitEdgeCenter = edgePosition.GetEdgeCenter(exitEdge);

            var x = innerX;

            
            iconPosition = new Rect
                (
                x,
                innerY,
                Styles.eventIcon.fixedWidth,
                Styles.eventIcon.fixedHeight
                ).PixelPerfect();

            x += iconPosition.width;

            var clipWidth = innerWidth - (x - innerX);

            clipPosition = new Rect
                (
                x,
                edgeY,
                clipWidth,
                edgeHeight
                ).PixelPerfect();

            labelInnerPosition = new Rect
                (
                Styles.spaceAroundIcon,
                innerY - edgeY,
                labelWidth,
                labelHeight
                ).PixelPerfect();
            
        }

        public Rect outerPosition { get; set; }

        public Rect innerPosition { get; set; }

        public Rect edgePosition { get; set; }

        public void DrawConnection(Vector2 offset)
        {
            var transitionRects = BehaviorTreeGraphDrawer.DrawTransition(graph, this, new WidgetElementState(), offset);

            UpdatePlaceHolderNodes(transitionRects);
            UpdateSelectionState();
        }

        private void UpdateSelectionState()
        {
            if (!isSelected) return;

            foreach (var placeHolderNode in placeHolderNodes)
            {
                GraphDrawer.DrawSelectionBox(placeHolderNode.Position, 2, Color.cyan);
            }
        }

        private void UpdatePlaceHolderNodes(List<Rect> transitionRects)
        {
            for (int i = 0; i < TRANSITION_SECTION_COUNT; i++)
            {
                placeHolderNodes[i].Position = transitionRects[i];
            }
        }

        public override void BeforeFrame()
        {
            base.BeforeFrame();
            Reposition();
        }

        public Edge exitEdge { get; set; }

        public Vector2 destinationEdgeCenter { get; set; }

        public Vector2 exitEdgeCenter { get; set; }

        private float minBend
        {
            get
            {
                if (element.source != element.destination)
                {
                    return 15;
                }
                else
                {
                    return (middle.y - canvas.Widget(element.source).position.center.y) / 2;
                }
            }
        }

        private float relativeBend => 1 / 4f;

        public Edge entryEdge { get; set; }

        public Vector2 entryEdgeCenter { get; set; }

        public Vector2 sourceEdgeCenter { get; set; }
        public Rect iconPosition { get; private set; }
        public Rect clipPosition { get; private set; }
        public Rect labelInnerPosition { get; private set; }
        
        public static class Styles
        {
            static Styles()
            {
                
                label = new GUIStyle(BoltCore.Styles.nodeLabel);
                label.alignment = TextAnchor.MiddleCenter;
                label.imagePosition = ImagePosition.TextOnly;

                labelInverted = new GUIStyle(label);
                labelInverted.normal.textColor = ColorPalette.unityBackgroundDark;

                eventIcon = new GUIStyle();
                eventIcon.imagePosition = ImagePosition.ImageOnly;
                eventIcon.fixedHeight = 16;
                eventIcon.fixedWidth = 16;
            }

            public static readonly GUIStyle label;

            public static readonly GUIStyle labelInverted;

            public static readonly GUIStyle eventIcon;

            public static readonly float spaceAroundIcon = 5;

            public static readonly float revealSpeed = 15;
        }
    }
}