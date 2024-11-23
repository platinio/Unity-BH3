using ArcaneOnyx.GraphCore;
using Unity.VisualScripting;
using UnityEngine;

namespace ArcaneOnyx.BehaviorTree
{
    [Widget(typeof(ConditionalExecution))]
    public class ConditionalExecutionWidget : BehaviorTreeNodeElementWidget
    {
        public override bool canDrag => false;
        public override bool canDelete => true;
        
        public override Rect position
        {
            get => element.Position;
            set => element.Position = value;
        }

        public override float zIndex {
            get
            {
                var conditionalExecution = node as ConditionalExecution;
                var owner = conditionalExecution?.Owner;
                
                if (conditionalExecution != null && owner != null && conditionalExecution.graph.elements.Contains(owner))
                {
                    var w = canvas.Widget(owner);
                    if (w == null) return 0;
                    
                    return w.zIndex + 0.5f;
                }

                return 0;
            }
            set { }
        }

        public ConditionalExecutionWidget(BehaviorTreeCanvas canvas, BehaviorTreeNode element) : base(canvas, element)
        {
            
        }
        
        public override void DrawForeground()
        {
            DrawForeground(Vector2.zero, e.IsRepaint);
        }

        public override void CachePosition()
        {
            base.CachePosition();
            
            float height = 40.0f;
            float separation = 5.0f;
            
            var owner = (node as ConditionalExecution)?.Owner;
            if (owner == null || !node.graph.elements.Contains(owner)) return;
            
            int index = owner.GetConditionalIndex(node as ConditionalExecution) + 1;

            var widget = canvas.Widget(owner);
            Rect p = widget.position;
            p.height = height;
            p.position -= new Vector2(0, (p.height / 2.0f) * index);
            p.position -= new Vector2(0, ((height / 2.0f) + separation) * index);
            
            TittleRect = p;
            position = p;

            zIndex = widget.zIndex + 1;
        }

        public override void DrawForeground(Vector2 offset, bool IsRepaint, bool useSelection = true)
        {
            if (!element.IsVisible) return;
            
            if (IsRepaint)
            {
                DrawOutsideBox(position);
                
                using (LudiqGUI.color.Override(element.Color))
                {
                    Styles.background.normal.background = element.NodeBackground;
                    Styles.background.Draw(position, false, IsSelected, false, false);
                }

                if (useSelection) GraphDrawer.DrawSelectionBox(position, GetBorderThickness(), Color.cyan);

                if (node.ShowIcon)
                {
                    DrawIcon(offset);
                }
                
                DrawTitle(offset, element.NodeName);
                DrawLastExecutionIcon(offset);
            }
        }
    }
}