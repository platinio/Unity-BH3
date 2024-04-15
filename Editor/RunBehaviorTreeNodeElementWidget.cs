using Platinio.GraphCore;
using Unity.VisualScripting;
using UnityEngine;

namespace Platinio.BehaviorTree
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
        
        private Vector2 EmptyNodeSize => new (150.0f, 100f);


        public override void DrawForeground(Vector2 offset, bool IsRepaint, bool useSelection = true)
        {
            var runBehaviorTreeGraphNode = element as RunBehaviorTreeGraphNode;
            if (runBehaviorTreeGraphNode == null || runBehaviorTreeGraphNode.BehaviorTreeGraphAsset == null)
            {
                position = new Rect(position.position, EmptyNodeSize);
                base.DrawForeground(Vector2.zero, e.IsRepaint);
                return;
            }

            position = CalculateBox();
            var box = CalculateBox();
            //GraphDrawer.DrawSelectionBox(box, 2, element.Color);

            BehaviorTreeGraph behaviorTreeGraph = null;

            if (runBehaviorTreeGraphNode.BehaviorTreeGraphInstance != null)
            {
                behaviorTreeGraph = runBehaviorTreeGraphNode.BehaviorTreeGraphInstance;
            }
            else
            {
                behaviorTreeGraph = runBehaviorTreeGraphNode.BehaviorTreeGraphAsset.graph;
            }
            
            Vector2 entryOffset = new Vector2();

            foreach (var node in behaviorTreeGraph.Nodes)
            {
                if (node is Entry entryNode)
                {
                    Vector2 sizeOffset = new Vector2(entryNode.Position.size.x * 0.5f, entryNode.Position.size.y * 0.5f);
                    sizeOffset += new Vector2(0, -50.0f);
                    entryOffset = (entryNode.Position.position + sizeOffset) * -1;
                }
            }

            entryOffset += offset;
            
            foreach (var graphElement in behaviorTreeGraph.elements)
            {
                if (graphElement is RunBehaviorTreeGraphNode runNode)
                {
                    RunBehaviorTreeNodeElementWidget w = behaviorTreeGraph.Canvas().Widget(runNode) as RunBehaviorTreeNodeElementWidget;
                    w.DrawForeground(position.position + (new Vector2(box.size.x / 2.0f, 0.0f)) + entryOffset, true, false);
                }
                else if (graphElement is BehaviorTreeNode node)
                {
                    var w = behaviorTreeGraph.Canvas().Widget(node) as BehaviorTreeNodeElementWidget;
                    w.DrawForeground(position.position + (new Vector2(box.size.x / 2.0f, 0.0f)) + entryOffset, true, false);
                }
                else if (graphElement is BehaviorTreeTransition transition)
                {
                    var w = behaviorTreeGraph.Canvas().Widget(transition) as BehaviorTreeTransitionWidget;
                    w.DrawConnection(position.position + (new Vector2(box.size.x / 2.0f, 0.0f)) + entryOffset);
                }
            }
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

        private Rect CalculateBox()
        {
            Vector2 margin = new Vector2(300, 300);
            
            var runBehaviorTreeGraphNode = element as RunBehaviorTreeGraphNode;
            if (runBehaviorTreeGraphNode == null || runBehaviorTreeGraphNode.BehaviorTreeGraphAsset == null) return default;

            var behaviorTreeGraph = runBehaviorTreeGraphNode.BehaviorTreeGraphAsset.graph;

            float right = float.MinValue;
            foreach (var graphElement in behaviorTreeGraph.elements)
            {
                if (graphElement is BehaviorTreeNode node && !(graphElement is PlaceHolderNode) && node.Position.x > right)
                {
                    right = node.Position.x;
                }
            }
            
            float left = float.MaxValue;
            foreach (var graphElement in behaviorTreeGraph.elements)
            {
                if (graphElement is BehaviorTreeNode node && !(graphElement is PlaceHolderNode) && node.Position.x < left)
                {
                    left = node.Position.x;
                }
            }
            
            float up = float.MaxValue;
            foreach (var graphElement in behaviorTreeGraph.elements)
            {
                if (graphElement is BehaviorTreeNode node && !(graphElement is PlaceHolderNode) && node.Position.y < up)
                {
                    up = node.Position.y;
                }
            }
            
            float down = float.MinValue;
            foreach (var graphElement in behaviorTreeGraph.elements)
            {
                if (graphElement is BehaviorTreeNode node && !(graphElement is PlaceHolderNode) && node.Position.y > down)
                {
                    down = node.Position.y;
                }
            }

            float w = Mathf.Abs(left - right);
            float h = Mathf.Abs(up - down);

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
                return;
            }

            BehaviorTreeGraph behaviorTreeGraph = null;

            if (runBehaviorTreeGraphNode.BehaviorTreeGraphInstance != null)
            {
                behaviorTreeGraph = runBehaviorTreeGraphNode.BehaviorTreeGraphInstance;
            }
            else
            {
                behaviorTreeGraph = runBehaviorTreeGraphNode.BehaviorTreeGraphAsset.graph;
            }

            foreach (var graphElement in behaviorTreeGraph.elements)
            {
                if (graphElement is RunBehaviorTreeGraphNode runNode)
                {
                    var w = behaviorTreeGraph.Canvas().Widget(runNode) as RunBehaviorTreeNodeElementWidget;
                    w.CachePosition();
                }
                else if (graphElement is BehaviorTreeNode node)
                {
                    var w = behaviorTreeGraph.Canvas().Widget(node) as BehaviorTreeNodeElementWidget;
                    w.CachePosition();
                }
                else if (graphElement is BehaviorTreeTransition transition)
                {
                    var w = behaviorTreeGraph.Canvas().Widget(transition) as BehaviorTreeTransitionWidget;
                    w.CachePosition();
                }
            }
        }
    }

}

