using System.Collections.Generic;
using System.Linq;
using ArcaneOnyx.GraphCore;
using Unity.VisualScripting;
using UnityEditor;
using UnityEngine;

namespace ArcaneOnyx.BehaviorTree
{
    [Widget(typeof(RunBehaviorTreeGraphNode))]
    public class RunBehaviorTreeNodeElementWidget : BehaviorTreeNodeElementWidget
    {
        public RunBehaviorTreeNodeElementWidget(BehaviorTreeCanvas canvas, BehaviorTreeNode element) : base(canvas, element)
        {
        }

        public override Rect position
        {
            get => element.Position;
            set => element.Position = value;
        }
        
        private static readonly GUIContent maxHeadLabelSizeContent = new GUIContent("M");
        
        private Vector2 EmptyNodeSize => new (150.0f, 100f);
        
        private void AdjustLabelFontSize()
        {
            RunBehaviorTreeNodeElementWidgetStyles.label.fontSize = RunBehaviorTreeNodeElementWidgetStyles.labelSelected.fontSize = Mathf.RoundToInt(RunBehaviorTreeNodeElementWidgetStyles.headerFontSize / graph.zoom);
        }

        private BehaviorTreeGraphAsset GetBehaviorTreeGraphAsset()
        {
            var runBehaviorTreeGraphNode = element as RunBehaviorTreeGraphNode;
            if (runBehaviorTreeGraphNode == null) return null;

            return runBehaviorTreeGraphNode.BehaviorTreeGraphAsset;
        }

        public override void DrawForeground(Vector2 offset, bool IsRepaint, bool useSelection = true)
        {
            if (GetBehaviorTreeGraphAsset() == null || GraphWillCauseRecursion())
            {
                position = new Rect(position.position, EmptyNodeSize);
                base.CachePosition();
                base.DrawForeground(offset, IsRepaint, useSelection);
                return;
            }
          
            position = CalculateSubBehaviorTreeBox();
            DrawSubGraphGroup(offset);

            // The sub-tree's own coordinates mean nothing to this canvas, so the whole preview is drawn
            // through one translation: its bounds' corner lands one padding inside the frame's. Everything
            // in the preview — nodes, guards, wires — shifts by this same vector, which is what keeps them
            // attached to each other no matter where the sub-tree was authored.
            BehaviorTreeGraph behaviorTreeGraph = GetBehaviorTreeGraph();
            Vector2 subBehaviorTreeOffset = position.position + SubTreePadding - CalculateSubTreeBounds(behaviorTreeGraph).position + offset;

            DrawSubTreeNodes(behaviorTreeGraph, subBehaviorTreeOffset);

            DrawParameterPorts();
        }

        protected override void DrawTitle(Vector2 offset, string title)
        {
            base.DrawTitle(offset, GraphWillCauseRecursion()? "RECURSION ERROR!" : element.NodeName);
        }

        private bool GraphWillCauseRecursion()
        {
            if (!(element is RunBehaviorTreeGraphNode runBehaviorTreeGraphNode)) return false;
            
            var runStack = new Stack<BehaviorTreeGraphAsset>(new[] { runBehaviorTreeGraphNode.BehaviorTreeGraphAsset });
            return runBehaviorTreeGraphNode.GraphWillCauseRecursion(runStack);
        }

        private void DrawSubTreeNodes(BehaviorTreeGraph behaviorTreeGraph, Vector2 offset)
        {
            var subCanvas = behaviorTreeGraph.Canvas();

            // Every node is cached before anything draws: a wire reads port handle positions off the node
            // widgets, and a guard anchors to its owner, so a single interleaved pass would hand some of
            // them last frame's layout.
            foreach (var graphElement in behaviorTreeGraph.elements)
            {
                if (!graphElement.DrawInSubTree) continue;

                if (graphElement is BehaviorTreeNode node && subCanvas.Widget(node) is BehaviorTreeNodeElementWidget w)
                {
                    w.CacheForPreview();
                }
            }

            // Nested frames first: a frame is the ground its siblings stand on, and its translucent fill
            // painted after a wire or a node that happens to overlap it — a variable node beside a guard,
            // say — buries them under the frame.
            foreach (var graphElement in behaviorTreeGraph.elements)
            {
                if (!graphElement.DrawInSubTree) continue;

                if (graphElement is RunBehaviorTreeGraphNode runNode && subCanvas.Widget(runNode) is RunBehaviorTreeNodeElementWidget w)
                {
                    w.DrawSubTreePreview(offset);
                }
            }

            // Wires over the frames — their endpoints sit on guards and ports that overlap them — but
            // under the nodes they connect.
            foreach (var graphElement in behaviorTreeGraph.elements)
            {
                if (!graphElement.DrawInSubTree) continue;

                if (graphElement is BehaviorTreeTransition transition)
                {
                    var w = subCanvas.Widget(transition) as BehaviorTreeTransitionWidget;
                    w.CachePosition();
                    w.DrawConnection(offset);
                }
                else if (graphElement is IPortConnection connection)
                {
                    // Both ends have to be in the preview — a wire from a node kind that opts out of it
                    // would point at empty space.
                    if (!connection.source.behaviorTreeNode.DrawInSubTree) continue;
                    if (!connection.destination.behaviorTreeNode.DrawInSubTree) continue;

                    if (subCanvas.Widget(connection) is IPortConnectionWidget w)
                    {
                        w.CachePosition();
                        w.DrawConnection(offset);
                    }
                }
            }

            foreach (var graphElement in behaviorTreeGraph.elements)
            {
                if (!graphElement.DrawInSubTree || graphElement is RunBehaviorTreeGraphNode) continue;

                if (graphElement is BehaviorTreeNode node && subCanvas.Widget(node) is BehaviorTreeNodeElementWidget w)
                {
                    w.DrawSubTreePreview(offset);
                }
            }
        }

        private void DrawSubGraphGroup(Vector2 offset)
        {
            string name = GetBehaviorTreeGraphAsset().name;
            AdjustLabelFontSize();
            var box = CalculateSubBehaviorTreeBox();
            
            using (LudiqGUI.color.Override(Color.cyan))
            {
                box.position += new Vector2(0, -GROUP_HEADER_LIFT) + offset;
                RunBehaviorTreeNodeElementWidgetStyles.group.Draw(box, false, false, true, false);
            }
            
            var labelPosition = new Rect
            (
                box.x + RunBehaviorTreeNodeElementWidgetStyles.label.margin.left,
                box.y + RunBehaviorTreeNodeElementWidgetStyles.label.margin.top,
                RunBehaviorTreeNodeElementWidgetStyles.label.CalcSize(new GUIContent(name)).x + RunBehaviorTreeNodeElementWidgetStyles.label.CalcSize(maxHeadLabelSizeContent).x,
                RunBehaviorTreeNodeElementWidgetStyles.group.border.top
            );
            
            EditorGUI.TextField(labelPosition, GUIContent.none, name, selection.Contains(element) ? RunBehaviorTreeNodeElementWidgetStyles.labelSelected : RunBehaviorTreeNodeElementWidgetStyles.label);
        }

        private BehaviorTreeGraph GetBehaviorTreeGraph()
        {
            var runBehaviorTreeGraphNode = element as RunBehaviorTreeGraphNode;

            if (runBehaviorTreeGraphNode.BehaviorTreeGraphInstance != null)
            {
                return runBehaviorTreeGraphNode.BehaviorTreeGraphInstance;
            }
            
            return runBehaviorTreeGraphNode.BehaviorTreeGraphAsset.graph;
        }

        /// <summary>
        /// /
        /// </summary>
        public override void DrawForeground()
        {
            var runBehaviorTreeGraphNode = element as RunBehaviorTreeGraphNode;
            if (runBehaviorTreeGraphNode == null || runBehaviorTreeGraphNode.BehaviorTreeGraphAsset == null)
            {
                position = new Rect(position.position, EmptyNodeSize);
                base.DrawForeground(Vector2.zero, e.IsRepaint);
                return;
            }
            
            DrawForeground(Vector2.zero, e.IsRepaint);
        }

        /// <summary>Room the frame keeps between its edges and the sub-tree's bounds, per side.</summary>
        private static readonly Vector2 SubTreePadding = new Vector2(50.0f, 50.0f);

        /// <summary>How far the group frame's header band is drawn above the frame's own rect.</summary>
        private const float GROUP_HEADER_LIFT = 40.0f;

        private Rect CalculateSubBehaviorTreeBox()
        {
            var runBehaviorTreeGraphNode = element as RunBehaviorTreeGraphNode;
            if (runBehaviorTreeGraphNode == null || runBehaviorTreeGraphNode.BehaviorTreeGraphAsset == null) return default;

            var bounds = CalculateSubTreeBounds(GetBehaviorTreeGraph());
            if (bounds.size == Vector2.zero) return new Rect(position.position, EmptyNodeSize);

            var r = position;
            r.size = bounds.size + SubTreePadding * 2.0f;

            return r;
        }

        /// <summary>
        /// The rectangle the sub-tree actually occupies, in its own graph's coordinates: full node rects,
        /// the guard stacks above their owners, and a nested frame's header band. This is what sizes the
        /// frame, so anything drawn in the preview has to be counted here or it leaks outside the box —
        /// which is exactly what the "larger side times two" estimate this replaces did to any tree not
        /// authored symmetrically around its own origin.
        /// </summary>
        private Rect CalculateSubTreeBounds(BehaviorTreeGraph behaviorTreeGraph)
        {
            var subCanvas = behaviorTreeGraph.Canvas();

            float left = float.MaxValue, up = float.MaxValue;
            float right = float.MinValue, down = float.MinValue;

            foreach (var graphElement in behaviorTreeGraph.elements)
            {
                if (!graphElement.DrawInSubTree) continue;
                if (!(graphElement is BehaviorTreeNode node) || graphElement is PlaceHolderNode) continue;

                // A guard has no authored position — its widget anchors it to its owner — so its room is
                // counted from the owner below via the same measured stack height the widgets place it with.
                if (graphElement is ConditionalExecution) continue;

                var nodePosition = node.Position;
                var top = nodePosition.yMin - ConditionalExecutionWidget.StackHeightAbove(subCanvas, node);
                if (node is RunBehaviorTreeGraphNode) top -= GROUP_HEADER_LIFT;

                left = Mathf.Min(left, nodePosition.xMin);
                right = Mathf.Max(right, nodePosition.xMax);
                up = Mathf.Min(up, top);
                down = Mathf.Max(down, nodePosition.yMax);
            }

            if (left > right) return default;

            return Rect.MinMaxRect(left, up, right, down);
        }

        public override void CachePosition()
        {
            var runBehaviorTreeGraphNode = element as RunBehaviorTreeGraphNode;
            if (runBehaviorTreeGraphNode == null || runBehaviorTreeGraphNode.BehaviorTreeGraphAsset == null)
            {
                base.CachePosition();
                return;
            }
            
            CacheParameterPortPositions();
        }

        /// <summary>
        /// Puts the parameter ports on the left edge of the sub-tree frame, below the name header.
        /// <para>
        /// A branch's parameters belong to the frame as a whole rather than to any node inside it, which
        /// is what the edge placement is for: the same place a caller looks to see what the branch takes.
        /// </para>
        /// </summary>
        private void CacheParameterPortPositions()
        {
            var box = CalculateSubBehaviorTreeBox();
            if (box.width <= 0f) return;

            CachePortPositions(box.y + RunBehaviorTreeNodeElementWidgetStyles.headerHeight, box.x, box.width);
            CacheChildWidgetPositions();
        }

        /// <summary>
        /// Draws the ports the frame carries. The group path never reaches the base node drawing, so
        /// without this a parameter exists on the node and is invisible on the canvas: declared, and
        /// impossible to connect anything to.
        /// </summary>
        private void DrawParameterPorts()
        {
            if (element.valueInputs == null || !element.valueInputs.Any()) return;

            DrawChildWidgets();
        }

        /// <summary>
        /// Adds the refresh this node needs, because its ports deliberately do not follow the sub-tree on their
        /// own.
        /// <para>
        /// Ports are matched by key, so rebuilding them the moment a branch's contract changes would drop every
        /// connection whose name no longer exists, at every call site at once and without saying so. Refreshing
        /// is therefore a decision — and this is where someone makes it. The entry reports how far the node has
        /// drifted so the decision is an informed one rather than a guess.
        /// </para>
        /// </summary>
        protected override IEnumerable<DropdownOption> contextOptions
        {
            get
            {
                foreach (var dropdownOption in base.contextOptions)
                {
                    yield return dropdownOption;
                }

                if (!(element is RunBehaviorTreeGraphNode runNode) || runNode.BehaviorTreeGraphAsset == null) yield break;

                var drift = runNode.DescribeContractDrift();

                var label = drift.Count == 0
                    ? "Refresh Parameters (up to date)"
                    : $"Refresh Parameters ({drift.Count} change(s) in {runNode.BehaviorTreeGraphAsset.name})";

                yield return new DropdownOption((System.Action)(() =>
                {
                    UndoUtility.RecordEditedObject("Refresh Sub-Tree Parameters");

                    // Say what it did. A refresh that removes a port silently removes whatever fed it, and the
                    // canvas alone will not make that obvious on a large tree.
                    if (drift.Count > 0)
                    {
                        Debug.Log($"[{runNode.NodeName}] refreshed parameters:{System.Environment.NewLine}  " +
                                  string.Join(System.Environment.NewLine + "  ", drift));
                    }

                    var dropped = runNode.RefreshParameters();

                    foreach (var line in dropped) Debug.LogWarning($"[BehaviorTree] {line}");

                    // The contract just changed, which is the one moment this node is resized. Doing it here
                    // rather than on the draw path is what keeps an author's own drag from being overwritten.
                    Authoring.ContractPortLayout.ResizeToFitPorts(runNode);

                    // The refresh is the fix for whatever the badge was reporting, so it has to stop
                    // reporting it now rather than at the next import.
                    Authoring.NodeProblemCache.Invalidate();

                    GUI.changed = true;
                }), label);
            }
        }
        
        private static class RunBehaviorTreeNodeElementWidgetStyles
        {
            static RunBehaviorTreeNodeElementWidgetStyles()
            {
                @group = new GUIStyle();
                @group.normal.background = BoltCore.Resources.LoadTexture("Group.png", new TextureResolution[] { 64 }, CreateTextureOptions.PixelPerfect)?.Single();
                @group.onNormal.background = @group.normal.background;
                
                @group.border = new RectOffset(16, 16, 25, 16);

                label = new GUIStyle();
                label.normal.textColor = new Color(1, 1, 1, 0.75f);
                label.alignment = TextAnchor.MiddleLeft;
                label.padding = new RectOffset(0, 5, 0, 0);
                label.margin = new RectOffset(10, 0, 0, 0);

                labelSelected = new GUIStyle(label);
            }

            public static readonly float headerFontSize = 14;

            public static readonly GUIStyle group;

            public static readonly GUIStyle label;

            public static readonly GUIStyle labelSelected;

            public static float headerHeight => @group.border.top;

            public static Color AdjustColor(Color color, bool selected)
            {
                float hue, saturation, value;
                var alpha = color.a;
                Color.RGBToHSV(color, out hue, out saturation, out value);

                var saturationBoost = 0.25f;
                var valueBoost = 0.25f;
                var alphaAttenuation = -0.4f;
                var minAlpha = 0.25f;

                if (selected)
                {
                    if (saturation == 0)
                    {
                        hue = 0.59f; // Tealish, same as selection rectangle
                    }

                    if (value == 0)
                    {
                        value = 1;
                    }

                    saturation = Mathf.Clamp(saturation + saturationBoost, 0.6f, 1);
                    value = Mathf.Clamp(value + valueBoost, 0.6f, 1);
                    alpha = Mathf.Clamp(alpha, minAlpha - alphaAttenuation, 1);
                }
                else
                {
                    if (saturation > 1 - saturationBoost &&
                        value > 1 - valueBoost)
                    {
                        alpha += alphaAttenuation;
                    }
                }

                alpha = Mathf.Clamp(alpha, minAlpha, 1);

                return Color.HSVToRGB(hue, saturation, value).WithAlpha(alpha);
            }
        }
    }

}

