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
                if (node is ConditionalExecution conditionalExecution)
                {
                    var w = canvas.Widget(conditionalExecution.Owner);
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
                
                DrawTitle(offset);
                DrawLastExecutionIcon(offset);
            }
        }
    }
}