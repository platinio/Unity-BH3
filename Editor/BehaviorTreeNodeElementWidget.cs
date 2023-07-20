using Platinio.GraphCore;
using Unity.VisualScripting;
using UnityEngine;

namespace Platinio.BehaviorTree
{
    [Widget(typeof(BehaviorTreeNode))]
    public class BehaviorTreeNodeElementWidget : GraphElementWidget<BehaviorTreeCanvas, BehaviorTreeNode>
    {
        public BehaviorTreeNodeElementWidget(BehaviorTreeCanvas canvas, BehaviorTreeNode element) : base(canvas, element)
        {
        }

        public Rect IconRect { get; private set; }
        public Rect TittleRect { get; private set; }

        public override Rect position
        {
            get => element.Position;
            set => element.Position = value;
        }

        public override bool canSelect => element.CanSelect;
        public override bool canDrag => element.CanDrag;
        public override bool canDelete => element.CanDelete;

        public bool IsSelected => selection.Contains(element);

        private readonly Vector2 ICON_POSITION_OFFSET = new Vector2(28.0f, 0.0f);
        private readonly Vector2 ICON_SIZE = new Vector2(65.0f, 65.0f);
        private readonly Vector2 TITLE_POSITION_OFFSET = new Vector2(-15.0f, -35.0f);

        private readonly float TITLE_HEIGHT = 20.0f;
        

        public override void DrawForeground()
        {
            DrawForeground(Vector2.zero, e.IsRepaint);
        }

        public void DrawForeground(Vector2 offset, bool IsRepaint, bool useSelection = true)
        {
            if (!element.IsVisible) return;

            base.DrawForeground();

            if (IsRepaint)
            {
                Rect p = position;
                p.position += offset;
                
                using (LudiqGUI.color.Override(element.Color))
                {
                    Styles.background.normal.background = element.NodeBackground;
                    Styles.background.Draw(p, false, IsSelected, false, false);
                }

                if (useSelection) GraphDrawer.DrawSelectionBox(p, GetBorderThickness(), Color.cyan);

                DrawIcon(offset);
                DrawTitle(offset);
            }
        }

        private int GetBorderThickness()
        {
            int borderThickness = 0;
           
            if (position.Contains(mousePosition)) borderThickness++;
            if (IsSelected) borderThickness++;

            return borderThickness;
        }

        private void DrawIcon(Vector2 offset)
        {
            GUIStyle style = new GUIStyle();
            style.normal.background = element.NodeIcon;

            Rect p = IconRect;
            p.position += offset;
            
            style.Draw(p, false, IsSelected, false, false);
        }

        private void DrawTitle(Vector2 offset)
        {
            Rect p = TittleRect;
            p.position += offset;
            
            Styles.title.Draw(p, element.NodeName, false, IsSelected, false, false);
        }

        public override void CachePosition()
        {
            base.CachePosition();
            
            if (!element.IsVisible) return;

            var edgeOrigin = element.Position.position;
            var innerOrigin = EdgeToInnerPosition(new Rect(edgeOrigin, Vector2.zero)).position;

            Vector2 iconPosition = innerOrigin + ICON_POSITION_OFFSET;
            Vector2 titlePosition = GetTitlePosition(innerOrigin);
            Vector2 titleSize = new Vector2(element.Width, TITLE_HEIGHT);
            
            using (LudiqGUIUtility.iconSize.Override(IconSize.Small))
            {
                IconRect = new Rect(iconPosition, ICON_SIZE);
                TittleRect = new Rect(titlePosition, titleSize);
            }
        }

        private Vector2 GetTitlePosition(Vector2 innerOrigin)
        {
            Vector2 titlePosition = innerOrigin + TITLE_POSITION_OFFSET;
            titlePosition.y += element.Position.height;
            return titlePosition;
        }

        public override void HandleInput()
        {
            if (element is PlaceHolderNode placeHolderNode)
            {
                placeHolderNode.IsSelected = isSelected;
            }

            if (e.IsMouseDrag(MouseButton.Left) &&
                e.ctrlOrCmd &&
                !canvas.isCreatingTransition)
            {
                canvas.StartTransition(element);

                e.Use();
            }
            else if (e.IsMouseDrag(MouseButton.Left) && canvas.isCreatingTransition)
            {
                e.Use();
            }
            else if (e.IsMouseUp(MouseButton.Left) && canvas.isCreatingTransition)
            {
                var source = canvas.TransitionSource;
                var destination = (canvas.hoveredWidget as BehaviorTreeNodeElementWidget).element;

                if (destination == null)
                {
                    canvas.CompleteTransitionToNewState();
                }
                
                else if (destination == source)
                {
                    canvas.CancelTransition();
                }
                else if (true)
                {
                    canvas.EndTransition(destination);
                }

                e.Use();
            }

            base.HandleInput();
        }

        protected Rect EdgeToInnerPosition(Rect edgeRect)
        {
            return GraphGUI.GetNodeEdgeToInnerPosition(edgeRect, NodeShape.Hex);
        }

        public override void BeforeFrame()
        {
            base.BeforeFrame();
            Reposition();
        }
        
        
        public static class Styles
        {
            static Styles()
            {
                background = new GUIStyle();

                title = new GUIStyle(BoltCore.Styles.nodeLabel);
                title.normal.textColor = new Color(1, 1, 1, 0.75f);
                title.alignment = TextAnchor.MiddleCenter;
                title.fontSize = 12;
                title.wordWrap = true;
            }

            public static readonly GUIStyle background;
            public static readonly GUIStyle title;
        }
    }
}