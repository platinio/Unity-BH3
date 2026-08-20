using System.Collections.Generic;
using Unity.VisualScripting;
using UnityEditor;
using UnityEngine;

namespace ArcaneOnyx.BehaviorTree
{
    public abstract class PortConnectionWidget<TConnection> : GraphCore.GraphElementWidget<BehaviorTreeCanvas, TConnection>, IPortConnectionWidget
        where TConnection : class, IPortConnection
    {
        protected PortConnectionWidget(BehaviorTreeCanvas canvas, TConnection connection) : base(canvas, connection) { }


        #region Model
        protected TConnection connection => element;
        #endregion

        #region Positioning

        public override IEnumerable<GraphCore.IWidget> positionDependencies
        {
            get
            {
                yield return GraphCore.XCanvasProvider.Widget(canvas, connection.source);
                yield return GraphCore.XCanvasProvider.Widget(canvas, connection.source);
            }
        }

        protected override bool snapToGrid => false;

        public Rect sourceHandlePosition { get; private set; }

        public Rect destinationHandlePosition { get; private set; }

        public Vector2 sourceHandleEdgeCenter { get; private set; }

        public Vector2 destinationHandleEdgeCenter { get; private set; }

        public Vector2 middlePosition;

        private Rect _position;

        private Rect _clippingPosition;

        public override Rect position
        {
            get => _position;
            set { }
        }

        public override Rect clippingPosition => _clippingPosition;

        public override void CachePosition()
        {
            base.CachePosition();
            
            sourceHandlePosition = GraphCore.XCanvasProvider.Widget<IPortWidget>(canvas, connection.source).handlePosition;
            destinationHandlePosition = GraphCore.XCanvasProvider.Widget<IPortWidget>(canvas, connection.destination).handlePosition;

            sourceHandleEdgeCenter = sourceHandlePosition.GetEdgeCenter(Edge.Right);
            destinationHandleEdgeCenter = destinationHandlePosition.GetEdgeCenter(Edge.Left);

            middlePosition = (sourceHandlePosition.center + destinationHandlePosition.center) / 2;

            _position = new Rect
                (
                middlePosition.x,
                middlePosition.y,
                0,
                0
                );

            _clippingPosition = _position.Encompass(sourceHandleEdgeCenter).Encompass(destinationHandleEdgeCenter);
        }

        #endregion


        #region Drawing

        protected virtual bool colorIfActive => true;

        public abstract Color color { get; }

        protected override bool dim => default;

        public override void DrawBackground()
        {
            base.DrawBackground();

            BeginDim();

            DrawConnection();

            if (showDroplets)
            {
                DrawDroplets();
            }

            EndDim();
        }

        protected virtual void DrawConnection()
        {
            var color = this.color;
          
            var sourceWidget = GraphCore.XCanvasProvider.Widget<IPortWidget>(canvas, connection.source);
            var destinationWidget = GraphCore.XCanvasProvider.Widget<IPortWidget>(canvas, connection.destination);

            var highlight = !canvas.IsCreatingConnection && (sourceWidget.isMouseOver || destinationWidget.isMouseOver);

            var willDisconnect = sourceWidget.willDisconnect || destinationWidget.willDisconnect;

            if (willDisconnect)
            {
                color = UnitConnectionStyles.disconnectColor;
            }
            else if (highlight)
            {
                color = UnitConnectionStyles.highlightColor;
            }
            else if (colorIfActive)
            {
                if (EditorApplication.isPaused)
                {
                    /*
                    if (EditorTimeBinding.frame == ConnectionDebugData.lastInvokeFrame)
                    {
                        color = UnitConnectionStyles.activeColor;
                    }*/
                }
                else
                {
                    //color = Color.Lerp(UnitConnectionStyles.activeColor, color, (EditorTimeBinding.time - ConnectionDebugData.lastInvokeTime) / UnitWidget<IUnit>.Styles.invokeFadeDuration);
                }
            }

            var thickness = 3;

            GraphGUI.DrawConnection(color, sourceHandleEdgeCenter, destinationHandleEdgeCenter, Edge.Right, Edge.Left, null, Vector2.zero, UnitConnectionStyles.relativeBend, UnitConnectionStyles.minBend, thickness);
        }

        /// <summary>
        /// The same wire shifted into a parent tree's sub-tree preview. Hover, disconnect and droplet
        /// state are skipped: a preview is not interactive, so none of them can occur there.
        /// </summary>
        public void DrawConnection(Vector2 offset)
        {
            GraphGUI.DrawConnection(color, sourceHandleEdgeCenter + offset, destinationHandleEdgeCenter + offset, Edge.Right, Edge.Left, null, Vector2.zero, UnitConnectionStyles.relativeBend, UnitConnectionStyles.minBend, 3);
        }

        #endregion


        #region Selecting

        public override bool canSelect => false;

        #endregion


        #region Dragging

        public override bool canDrag => false;

        #endregion


        #region Deleting

        public override bool canDelete => true;

        #endregion


        #region Droplets

        private readonly List<float> droplets = new List<float>();

        private float dropTime;

        private float lastInvokeTime;

        private const float handleAlignmentMargin = 0.1f;

        protected virtual bool showDroplets => true;

        protected abstract Vector2 GetDropletSize();

        protected abstract void DrawDroplet(Rect position);

        protected virtual void DrawDroplets()
        {
            foreach (var droplet in droplets)
            {
                Vector2 position;

                if (droplet < handleAlignmentMargin)
                {
                    var t = droplet / handleAlignmentMargin;
                    position = Vector2.Lerp(sourceHandlePosition.center, sourceHandleEdgeCenter, t);
                }
                else if (droplet > 1 - handleAlignmentMargin)
                {
                    var t = (droplet - (1 - handleAlignmentMargin)) / handleAlignmentMargin;
                    position = Vector2.Lerp(destinationHandleEdgeCenter, destinationHandlePosition.center, t);
                }
                else
                {
                    var t = (droplet - handleAlignmentMargin) / (1 - 2 * handleAlignmentMargin);
                    position = GraphGUI.GetPointOnConnection(t, sourceHandleEdgeCenter, destinationHandleEdgeCenter, Edge.Right, Edge.Left, UnitConnectionStyles.relativeBend, UnitConnectionStyles.minBend);
                }

                var size = GetDropletSize();

                using (LudiqGUI.color.Override(GUI.color * color))
                {
                    DrawDroplet(new Rect(position.x - size.x / 2, position.y - size.y / 2, size.x, size.y));
                }
            }
        }

        #endregion
    }
}
