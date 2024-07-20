using System;
using System.Collections.Generic;
using System.Linq;
using Platinio.GraphCore;
using Unity.VisualScripting;
using UnityEditor;
using UnityEngine;

namespace Platinio.BehaviorTree
{
    [Widget(typeof(BehaviorTreeNode))]
    public class BehaviorTreeNodeElementWidget : GraphElementWidget<BehaviorTreeCanvas, BehaviorTreeNode>, IBehaviorTreeWidget
    {
        protected BehaviorTreeNode unit => element;
        
        private UnitDescription description;
        public Rect portsBackgroundPosition { get; private set; }

        protected NodeShape shape => NodeShape.Hex;
        
        public BehaviorTreeNodeElementWidget(BehaviorTreeCanvas canvas, BehaviorTreeNode element) : base(canvas, element)
        {
            unit.onPortsChanged += CacheDefinition;
            unit.onPortsChanged += SubWidgetsChanged;
        }
        
        public override void Dispose()
        {
            base.Dispose();

            unit.onPortsChanged -= CacheDefinition;
            unit.onPortsChanged -= SubWidgetsChanged;
        }
        
        protected readonly List<IPortWidget> ports = new List<IPortWidget>();

        protected readonly List<IPortWidget> inputs = new List<IPortWidget>();

        protected readonly List<IPortWidget> outputs = new List<IPortWidget>();

        private readonly List<string> settingNames = new List<string>();
        private float currentInnerWidth;
        
        protected override void CacheItemFirstTime()
        {
            base.CacheItemFirstTime();
            CacheDefinition();
        }
        
        protected virtual void CacheDefinition()
        {
            inputs.Clear();
            outputs.Clear();
            ports.Clear();
            inputs.AddRange(unit.inputs.Select(port => canvas.Widget<IPortWidget>(port)));
            outputs.AddRange(unit.outputs.Select(port => canvas.Widget<IPortWidget>(port)));
            ports.AddRange(inputs);
            ports.AddRange(outputs);

            Reposition();
        }
        
        protected override void CacheDescription()
        {
            //description = unit.Description<UnitDescription>();
            
            Reposition();
        }
        
        public virtual Inspector GetPortInspector(IUnitPort port, Metadata metadata)
        {
            return metadata.Inspector();
        }
        
        public Rect edgePosition
        {
            get
            {
                return position;
            }
            set
            {
                position = value;
            }
        }
        
        public Rect innerPosition
        {
            get
            {
                return EdgeToInnerPosition(edgePosition);
            }
            set
            {
                edgePosition = InnerToEdgePosition(value);
            }
        }
        
        public override void ExpandDragGroup(HashSet<IGraphElement> dragGroup)
        {
            if (BoltCore.Configuration.carryChildren)
            {
                foreach (var output in unit.outputs)
                {
                    foreach (var connection in output.connections)
                    {
                        if (dragGroup.Contains(connection.destination.behaviorTreeNode))
                        {
                            continue;
                        }

                        dragGroup.Add(connection.destination.behaviorTreeNode);

                        canvas.Widget(connection.destination.behaviorTreeNode).ExpandDragGroup(dragGroup);
                    }
                }
            }
        }
        
        public override IEnumerable<IWidget> positionDependers => ports.Cast<IWidget>();
        
        
        
        
        
        
        
        
        
        
        public override IEnumerable<IWidget> subWidgets => element.ports.Select(port => canvas.Widget(port));

        public Rect LastExecutionStateIconRect { get; private set; }
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
        private readonly Vector2 LAST_EXECUTION_STATE_ICON_OFFSET = new Vector2(-15.0f, -35.0f);

        private readonly float TITLE_HEIGHT = 20.0f;

        private bool showPorts = true;
        
        public override void DrawForeground()
        {
            if (showPorts)
            {
                DrawPortsBackground();
            }
            
            DrawForeground(Vector2.zero, e.IsRepaint);
        }

        protected void DrawPortsBackground()
        {
            return;
            
            //if (canvas.showRelations)
            {
                foreach (var relation in unit.relations)
                {
                    var start = ports.Single(pw => pw.port == relation.source).handlePosition.center;
                    var end = ports.Single(pw => pw.port == relation.destination).handlePosition.center;

                    var startTangent = start;
                    var endTangent = end;

                    if (relation.source is IUnitInputPort &&
                        relation.destination is IUnitInputPort)
                    {
                        //startTangent -= new Vector2(20, 0);
                        endTangent -= new Vector2(32, 0);
                    }
                    else
                    {
                        startTangent += new Vector2(innerPosition.width / 2, 0);
                        endTangent += new Vector2(-innerPosition.width / 2, 0);
                    }

                    Handles.DrawBezier
                    (
                        start,
                        end,
                        startTangent,
                        endTangent,
                        new Color(0.136f, 0.136f, 0.136f, 1.0f),
                        null,
                        3
                    );
                }
            }
            /*
            else
            {
                if (e.IsRepaint)
                {
                    Styles.portsBackground.Draw(portsBackgroundPosition, false, false, false, false);
                }
            }*/
        }
        
        public virtual void DrawForeground(Vector2 offset, bool IsRepaint, bool useSelection = true)
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
                DrawLastExecutionIcon(offset);
            }
        }

        private void DrawLastExecutionIcon(Vector2 offset)
        {
            if (element.LastExecutionStatus == ExecutionStatus.None) return;
            
            GUIStyle style = new GUIStyle();
            style.normal.background = Resources.Load<Texture2D>($"ExecutionStatus/{element.LastExecutionStatus.ToString()}");

            Rect p = LastExecutionStateIconRect;
            p.position += offset + new Vector2(-10, 20);
            
            style.Draw(p, false, IsSelected, false, false);
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
            var headerHeight = 0f;
            
            var edgeOrigin = element.Position.position;
            var innerOrigin = EdgeToInnerPosition(new Rect(edgeOrigin, Vector2.zero)).position;
            
            var edgeX = edgeOrigin.x;
            var edgeY = edgeOrigin.y;
            
            var innerY = innerOrigin.y;
            var y = innerY;
            
            var innerWidth = currentInnerWidth;
            var edgeWidth = InnerToEdgePosition(new Rect(0, 0, innerWidth, 0)).width;
            
            
            //ports code
           
            
            y = innerY + headerHeight;

            var innerHeight = 0f;

            innerHeight += headerHeight;
            
            //if (showPorts)
            {
                innerHeight += Styles.spaceBeforePorts;
                y += Styles.spaceBeforePorts;

                var portsBackgroundY = y;
                var portsBackgroundHeight = 0f;

                portsBackgroundHeight += Styles.portsBackground.padding.top;
                innerHeight += Styles.portsBackground.padding.top;
                y += Styles.portsBackground.padding.top;

                var portStartY = y;

                var inputsHeight = 0f;
                var outputsHeight = 0f;

                foreach (var input in inputs)
                {
                    input.y = y;

                    var inputHeight = input.GetHeight();

                    inputsHeight += inputHeight;
                    y += inputHeight;

                    inputsHeight += Styles.spaceBetweenPorts;
                    y += Styles.spaceBetweenPorts;
                }

                if (inputs.Count > 0)
                {
                    inputsHeight -= Styles.spaceBetweenPorts;
                    y -= Styles.spaceBetweenPorts;
                }

                y = portStartY;

                foreach (var output in outputs)
                {
                    output.y = y;

                    var outputHeight = output.GetHeight();

                    outputsHeight += outputHeight;
                    y += outputHeight;

                    outputsHeight += Styles.spaceBetweenPorts;
                    y += Styles.spaceBetweenPorts;
                }

                if (outputs.Count > 0)
                {
                    outputsHeight -= Styles.spaceBetweenPorts;
                    y -= Styles.spaceBetweenPorts;
                }

                var portsHeight = Math.Max(inputsHeight, outputsHeight);

                portsBackgroundHeight += portsHeight;
                innerHeight += portsHeight;
                y = portStartY + portsHeight;

                portsBackgroundHeight += Styles.portsBackground.padding.bottom;
                innerHeight += Styles.portsBackground.padding.bottom;
                y += Styles.portsBackground.padding.bottom;

                portsBackgroundPosition = new Rect
                    (
                    edgeX,
                    portsBackgroundY,
                    edgeWidth,
                    portsBackgroundHeight
                    );
            }
            
            
            
            
            base.CachePosition();
            
            if (!element.IsVisible) return;

           
           

            Vector2 iconPosition = innerOrigin + ICON_POSITION_OFFSET;
            Vector2 titlePosition = GetTitlePosition(innerOrigin);
            Vector2 titleSize = new Vector2(element.Width, TITLE_HEIGHT);
            Vector2 lastExecutionIconPosition = innerOrigin + LAST_EXECUTION_STATE_ICON_OFFSET;
            
            using (LudiqGUIUtility.iconSize.Override(IconSize.Small))
            {
                IconRect = new Rect(iconPosition, ICON_SIZE);
                TittleRect = new Rect(titlePosition, titleSize);
                LastExecutionStateIconRect = new Rect(lastExecutionIconPosition, new Vector2(25, 25));
            }
        }
        
        protected Rect InnerToEdgePosition(Rect position)
        {
            return GraphGUI.GetNodeInnerToEdgePosition(position, shape);
        }
        
        

        private Vector2 GetTitlePosition(Vector2 innerOrigin)
        {
            Vector2 titlePosition = innerOrigin + TITLE_POSITION_OFFSET;
            titlePosition.y += element.Position.height;
            return titlePosition;
        }

        private bool CanCreateTransition()
        {
            return canvas.graph.CountTransitionsFromNode(element) < element.MaxChildrenLimit;
        }

        public override void HandleInput()
        {
            
            if (canvas.IsCreatingConnection)
            {
                if (e.IsMouseDown(MouseButton.Left))
                {
                    var source = canvas.ConnectionSource;
                    var destination = source.CompatiblePort(unit);

                    if (destination != null)
                    {
                        UndoUtility.RecordEditedObject("Connect Nodes");
                        source.ValidlyConnectTo(destination);
                        canvas.ConnectionSource = null;
                        canvas.Widget(source.behaviorTreeNode).Reposition();
                        canvas.Widget(destination.behaviorTreeNode).Reposition();
                        GUI.changed = true;
                    }

                    e.Use();
                }
                else if (e.IsMouseDown(MouseButton.Right))
                {
                    canvas.CancelConnection();
                    e.Use();
                }
            }
            
            
            
            
            if (element is PlaceHolderNode placeHolderNode)
            {
                placeHolderNode.IsSelected = isSelected;
            }

            if (e.IsMouseDrag(MouseButton.Left) &&
                e.ctrlOrCmd &&
                !canvas.isCreatingTransition &&
                CanCreateTransition())
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
                var hoveredWidget = canvas.hoveredWidget as BehaviorTreeNodeElementWidget;
                var destination = hoveredWidget == null? null : hoveredWidget.element;

                if (destination == null)
                {
                    canvas.CompleteTransitionToNewState();
                }
                else if (destination == source || canvas.graph.TransitionExist(source, destination))
                {
                    canvas.CancelTransition();
                }
                else
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
            public static readonly float spaceAroundLineIcon = 5;

            public static readonly float spaceBeforePorts = 5;

            public static readonly float spaceBetweenInputsAndOutputs = 8;

            public static readonly float spaceBeforeSettings = 2;

            public static readonly float spaceBetweenSettings = 3;

            public static readonly float spaceBetweenPorts = 3;

            public static readonly float spaceAfterSettings = 0;

            public static readonly float maxSettingsWidth = 150;

            public static readonly GUIStyle portsBackground;

            public static readonly float iconSize = IconSize.Medium;

            public static readonly float iconsSize = IconSize.Small;

            public static readonly float iconsSpacing = 3;

            public static readonly int iconsPerColumn = 2;

            public static readonly float spaceAfterIcon = 6;

            public static readonly float spaceAfterSurtitle = 2;

            public static readonly float spaceBeforeSubtitle = 0;

            public static readonly float invokeFadeDuration = 0.5f;
            
            static Styles()
            {
                background = new GUIStyle();

                title = new GUIStyle(BoltCore.Styles.nodeLabel);
                title.normal.textColor = new Color(1, 1, 1, 0.75f);
                title.alignment = TextAnchor.MiddleCenter;
                title.fontSize = 11;
                title.wordWrap = true;
                
                
                if (EditorGUIUtility.isProSkin)
                {
                    portsBackground = new GUIStyle("In BigTitle")
                    {
                        padding = new RectOffset(0, 0, 6, 5)
                    };
                }
                else
                {
                    TextureResolution[] textureResolution = { 2 };
                    var createTextureOptions = CreateTextureOptions.Scalable;
                    EditorTexture normalTexture = BoltCore.Resources.LoadTexture($"NodePortsBackground.png", textureResolution, createTextureOptions);

                    portsBackground = new GUIStyle
                    {
                        normal = { background = normalTexture.Single() },
                        padding = new RectOffset(0, 0, 6, 5)
                    };
                }
                
            }

            public static readonly GUIStyle background;
            public static readonly GUIStyle title;
        }

        public IBehaviorTreeNode behaviorTreeNode { get; }
        public Inspector GetPortInspector(IPort port, Metadata metadata)
        {
            return null;
        }
    }
}