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
            Styles.label.fontSize = Styles.labelSelected.fontSize = Mathf.RoundToInt(Styles.headerFontSize / graph.zoom);
        }

        private BehaviorTreeGraphAsset GetBehaviorTreeGraphAsset()
        {
            var runBehaviorTreeGraphNode = element as RunBehaviorTreeGraphNode;
            if (runBehaviorTreeGraphNode == null) return null;

            return runBehaviorTreeGraphNode.BehaviorTreeGraphAsset;
        }

        public override void DrawForeground(Vector2 offset, bool IsRepaint, bool useSelection = true)
        {
            if (GetBehaviorTreeGraphAsset() == null)
            {
                position = new Rect(position.position, EmptyNodeSize);
                base.DrawForeground(Vector2.zero, e.IsRepaint);
                return;
            }
          
            position = CalculateSubBehaviorTreeBox();
            DrawSubGraphGroup();

            BehaviorTreeGraph behaviorTreeGraph = GetBehaviorTreeGraph();
            Vector2 entryOffset = behaviorTreeGraph.GetEntryNodeOffset() + offset;
            Vector2 subBehaviorTreeOffset = position.position + (new Vector2(position.size.x / 2.0f, 0.0f)) + entryOffset;
            
            DrawSubTreeNodes(behaviorTreeGraph, subBehaviorTreeOffset);
        }

        private void DrawSubTreeNodes(BehaviorTreeGraph behaviorTreeGraph, Vector2 offset)
        {
            foreach (var graphElement in behaviorTreeGraph.elements)
            {
                if (!graphElement.DrawInSubTree) continue;
                
                if (graphElement is RunBehaviorTreeGraphNode runNode)
                {
                    RunBehaviorTreeNodeElementWidget w = behaviorTreeGraph.Canvas().Widget(runNode) as RunBehaviorTreeNodeElementWidget;
                    w.CachePosition();
                    w.DrawForeground(offset, true, false);
                }
                else if (graphElement is BehaviorTreeNode node)
                {
                    var w = behaviorTreeGraph.Canvas().Widget(node) as BehaviorTreeNodeElementWidget;
                    w.CachePosition();
                    w.DrawForeground(offset, true, false);
                }
                else if (graphElement is BehaviorTreeTransition transition)
                {
                    var w = behaviorTreeGraph.Canvas().Widget(transition) as BehaviorTreeTransitionWidget;
                    w.CachePosition();
                    w.DrawConnection(offset);
                }
            }
        }

        private void DrawSubGraphGroup()
        {
            string name = GetBehaviorTreeGraphAsset().name;
            AdjustLabelFontSize();
            var box = CalculateSubBehaviorTreeBox();
            
            using (LudiqGUI.color.Override(Color.cyan))
            {
                box.position += new Vector2(0, -40.0f);
                Styles.group.Draw(box, false, false, true, false);
            }
            
            var labelPosition = new Rect
            (
                box.x + Styles.label.margin.left,
                box.y + Styles.label.margin.top,
                Styles.label.CalcSize(new GUIContent(name)).x + Styles.label.CalcSize(maxHeadLabelSizeContent).x,
                Styles.group.border.top
            );
            
            EditorGUI.TextField(labelPosition, GUIContent.none, name, selection.Contains(element) ? Styles.labelSelected : Styles.label);
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

        private Rect CalculateSubBehaviorTreeBox()
        {
            
            
            Vector2 margin = new Vector2(300, 200);
            
            var runBehaviorTreeGraphNode = element as RunBehaviorTreeGraphNode;
            if (runBehaviorTreeGraphNode == null || runBehaviorTreeGraphNode.BehaviorTreeGraphAsset == null) return default;

            var behaviorTreeGraph = runBehaviorTreeGraphNode.BehaviorTreeGraphAsset.graph;

            float right = float.MinValue;
            foreach (var graphElement in behaviorTreeGraph.elements)
            {
                if (!graphElement.DrawInSubTree) continue;
                if (graphElement is BehaviorTreeNode node && !(graphElement is PlaceHolderNode) && node.Position.x > right)
                {
                    right = node.Position.x;
                }
            }
            
            float left = float.MaxValue;
            foreach (var graphElement in behaviorTreeGraph.elements)
            {
                if (!graphElement.DrawInSubTree) continue;
                if (graphElement is BehaviorTreeNode node && !(graphElement is PlaceHolderNode) && node.Position.x < left)
                {
                    left = node.Position.x;
                }
            }
            
            float up = float.MaxValue;
            foreach (var graphElement in behaviorTreeGraph.elements)
            {
                if (!graphElement.DrawInSubTree) continue;
                if (graphElement is BehaviorTreeNode node && !(graphElement is PlaceHolderNode) && node.Position.y < up)
                {
                    up = node.Position.y;
                }
            }
            
            float down = float.MinValue;
            foreach (var graphElement in behaviorTreeGraph.elements)
            {
                if (!graphElement.DrawInSubTree) continue;
                if (graphElement is BehaviorTreeNode node && !(graphElement is PlaceHolderNode) && node.Position.y > down)
                {
                    down = node.Position.y;
                }
            }

            //TODO: address comment
            /*
             * in order to make this fast we are not taking into account offsets, we are just calculating the larger side and go with it
             * this can cause blank spaces in the subgraph if it is not properly aligned 
             */
            
            float largerHSide = Mathf.Abs(left) > Mathf.Abs(right)? left : right;
            largerHSide = Mathf.Abs(largerHSide);
            float w = largerHSide * 2.0f;
            
            float largerVSide = Mathf.Abs(down) > Mathf.Abs(up)? down : up;
            largerVSide = Mathf.Abs(largerVSide);
            float h = largerVSide;

            var r = position;
            r.size = new Vector2(w, h);
            r.size += margin;
           
            return r;
        }

        public override void CachePosition()
        {
            var runBehaviorTreeGraphNode = element as RunBehaviorTreeGraphNode;
            if (runBehaviorTreeGraphNode == null || runBehaviorTreeGraphNode.BehaviorTreeGraphAsset == null)
            {
                base.CachePosition();
            }
        }
        
        public static class Styles
        {
            static Styles()
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

