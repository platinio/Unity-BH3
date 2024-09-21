using System;
using System.Collections.Generic;
using System.Linq;
using ArcaneOnyx.GraphCore;
using Unity.VisualScripting;
using UnityEditor;
using UnityEngine;
using MouseButton = Unity.VisualScripting.MouseButton;

namespace ArcaneOnyx.BehaviorTree
{
    [Widget(typeof(BehaviorTreeNode))]
    public class BehaviorTreeNodeElementWidget : GraphElementWidget<BehaviorTreeCanvas, BehaviorTreeNode>, IBehaviorTreeWidget
    {
        public IBehaviorTreeNode behaviorTreeNode => node;
        protected BehaviorTreeNode node => element;
        
        private UnitDescription description;
        public Rect portsBackgroundPosition { get; private set; }

        protected NodeShape shape => NodeShape.Hex;
        
        public override IEnumerable<IWidget> positionDependers => ports.Cast<IWidget>();
        
        protected readonly List<IPortWidget> ports = new List<IPortWidget>();

        protected readonly List<IPortWidget> inputs = new List<IPortWidget>();

        protected readonly List<IPortWidget> outputs = new List<IPortWidget>();

        private readonly List<string> settingNames = new List<string>();
        private float currentInnerWidth;
        
        public override IEnumerable<IWidget> subWidgets => element.ports.Select(port => canvas.Widget(port));

        public Rect LastExecutionStateIconRect { get; private set; }
        public Rect IconRect { get; private set; }
        public Rect TittleRect { get; protected set; }

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
        private readonly Vector2 TITLE_POSITION_OFFSET = new Vector2(-15.0f, -40.0f);
        private readonly Vector2 LAST_EXECUTION_STATE_ICON_OFFSET = new Vector2(-15.0f, -35.0f);

        private readonly float TITLE_HEIGHT = 25.0f;

        protected override bool snapToGrid => true;

        public BehaviorTreeNodeElementWidget(BehaviorTreeCanvas canvas, BehaviorTreeNode element) : base(canvas, element)
        {
            node.onPortsChanged += CacheDefinition;
            node.onPortsChanged += SubWidgetsChanged;
        }
        
        public override void Dispose()
        {
            base.Dispose();

            node.onPortsChanged -= CacheDefinition;
            node.onPortsChanged -= SubWidgetsChanged;
        }
        
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
            inputs.AddRange(node.inputs.Select(port => canvas.Widget<IPortWidget>(port)));
            outputs.AddRange(node.outputs.Select(port => canvas.Widget<IPortWidget>(port)));
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
            get => position;
            set => position = value;
        }
        
        public Rect innerPosition
        {
            get => EdgeToInnerPosition(edgePosition);
            set => edgePosition = InnerToEdgePosition(value);
        }
        
        public override void ExpandDragGroup(HashSet<IGraphElement> dragGroup)
        {
            if (BoltCore.Configuration.carryChildren)
            {
                foreach (var output in node.outputs)
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
        
        public override void DrawForeground()
        {
            DrawForeground(Vector2.zero, e.IsRepaint);
        }
        
        public virtual void DrawForeground(Vector2 offset, bool IsRepaint, bool useSelection = true)
        {
            if (!element.IsVisible) return;

            base.DrawForeground();

            if (IsRepaint)
            {
                Rect p = position;
                p.position += offset;
                
                DrawOutsideBox(p);
                
                using (LudiqGUI.color.Override(new Color(0.2f, 0.2f, 0.2f, 1)))
                {
                    
                    Styles.background.normal.background = element.NodeBackground;
                    Styles.background.Draw(p, false, IsSelected, false, false);

                    float connectorSize = 12;
                    float horizontalOffset = 3.0f;
                    
                    if (element.MaxChildrenLimit > 0)
                    {
                        var bottomConnection = new Rect();
                        bottomConnection.x = p.x + (p.width / 2.0f) - (connectorSize / 2.0f);
                        bottomConnection.y = p.y + (p.height) - horizontalOffset;
                        bottomConnection.width = connectorSize;
                        bottomConnection.height = connectorSize;
                        Styles.background.Draw(bottomConnection, false, IsSelected, false, false);
                    }

                    if (element.CanBeUseAsTransitionDestination)
                    {
                        var topConnection = new Rect();
                        topConnection.x = p.x + (p.width / 2.0f) - (connectorSize / 2.0f);
                        topConnection.y = p.y - connectorSize + horizontalOffset;
                        topConnection.width = connectorSize;
                        topConnection.height = connectorSize;
                    
                        Styles.background.Draw(topConnection, false, IsSelected, false, false);
                    }
                }
               
                if (useSelection) GraphDrawer.DrawSelectionBox(p, GetBorderThickness(), Color.cyan);

                if (node.ShowIcon)
                {
                    DrawIcon(offset);
                }
                
                DrawTitle(offset);
                DrawLastExecutionIcon(offset);
            }
        }

        public static Texture2D outsideTexture;
        
        protected virtual void DrawOutsideBox(Rect p)
        {
            float borderSize = 10;
                    
            Rect outsideBox = p;
            outsideBox.position += new Vector2(-borderSize / 2.0f, -borderSize / 2.0f);
            outsideBox.width += borderSize;
            outsideBox.height += borderSize;
                    
            int conditionalExecutionCount = node.ConditionalExecutions.Count;
            float conditionalExecutionHeight = 45.0f * conditionalExecutionCount;

            outsideBox.height += conditionalExecutionHeight;
            outsideBox.position -= new Vector2(0, conditionalExecutionHeight);

            outsideTexture = null;
            if (outsideTexture == null)
            {
                outsideTexture = new Texture2D(1, 1);
                outsideTexture.wrapMode = TextureWrapMode.Repeat;
                outsideTexture.SetPixel(0, 0, new Color(0.1f, 0.1f, 0.1f, 1));
                outsideTexture.Apply();
            }

            using (LudiqGUI.color.Override(new Color(0.1f, 0.1f, 0.1f, 1)))
            {
                Styles.background.Draw(outsideBox, false, IsSelected, false, false);
            }
        }

        protected void DrawLastExecutionIcon(Vector2 offset)
        {
            if (element.LastExecutionStatus == ExecutionStatus.None) return;
            
            GUIStyle style = new GUIStyle();
            style.normal.background = Resources.Load<Texture2D>($"ExecutionStatus/{element.LastExecutionStatus.ToString()}");

            Rect p = LastExecutionStateIconRect;
            p.position += offset + new Vector2(-10, 20);
            
            style.Draw(p, false, IsSelected, false, false);
        }

        protected int GetBorderThickness()
        {
            int borderThickness = 0;
           
            if (position.Contains(mousePosition)) borderThickness++;
            if (IsSelected) borderThickness++;

            return borderThickness;
        }

        protected void DrawIcon(Vector2 offset)
        {
            GUIStyle style = new GUIStyle();
            style.normal.background = element.NodeIcon;

            Rect p = IconRect;
            p.position += offset;
            
            style.Draw(p, false, IsSelected, false, false);
        }

        protected void DrawTitle(Vector2 offset)
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
            var innerY = innerOrigin.y;
            var y = innerY;
            var innerWidth = currentInnerWidth;
            var edgeWidth = InnerToEdgePosition(new Rect(0, 0, innerWidth, 0)).width;
            y = innerY + headerHeight;

            if (!node.ShowIcon)
            {
                Rect newPosition = position;
                newPosition.height = 60 + (GetPortSectionHeight());
                position = newPosition;
            }
            
            CachePortPosition(y, edgeX, edgeWidth);

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

        private void CachePortPosition(float y, float edgeX, float edgeWidth)
        {
            y += Styles.spaceBeforePorts;

            var portsBackgroundY = y;
            var portsBackgroundHeight = 0f;

            portsBackgroundHeight += Styles.portsBackground.padding.top;
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
            y = portStartY + portsHeight;

            portsBackgroundHeight += Styles.portsBackground.padding.bottom;
            y += Styles.portsBackground.padding.bottom;

            portsBackgroundPosition = new Rect
            (
                edgeX,
                portsBackgroundY,
                edgeWidth,
                portsBackgroundHeight
            );
        }

        private float GetPortsHeight(IEnumerable<IPortWidget> portWidgets)
        {
            float height = 0;
            
            foreach (var input in portWidgets)
            {
                height += input.GetHeight();
            }

            if (inputs.Count > 0)
            {
                height -= Styles.spaceBetweenPorts;
            }

            return height;
        }

        private float GetPortSectionHeight()
        {
            float inputHeight = GetPortsHeight(inputs);
            float ouputHeight = GetPortsHeight(outputs);

            return inputHeight > ouputHeight ? inputHeight : ouputHeight;
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
                    var destination = source.CompatiblePort(node);

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
                else if (destination.CanBeUseAsTransitionDestination)
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
        
        protected override IEnumerable<DropdownOption> contextOptions
        {
            get
            {
                foreach (var dropdownOption in base.contextOptions)
                {
                    yield return dropdownOption;
                }

                if (selection.Count == 1)
                {
                    var bNode = selection.First() as BehaviorTreeNode;
                    
                    if (bNode != null && bNode.CanUseConditionalExecutions)
                    {
                        var typeEnumerable = GetEnumerableOfType(typeof(ConditionalExecution));
                        foreach (var nodeType in typeEnumerable)
                        {
                            yield return new DropdownOption((Action)( () =>
                            {
                                BehaviorTreeNode selectedNode = selection.First() as BehaviorTreeNode;

                                var conditionalExecution = Activator.CreateInstance(nodeType) as ConditionalExecution;
                                conditionalExecution.UpdateOwner(selectedNode);
                                conditionalExecution.Position = new Rect(element.position, conditionalExecution.StartingSize);

                                graph.elements.Add(conditionalExecution);
                                selection.Select(conditionalExecution);
                                GUI.changed = true;
                           
                                node.AddConditionalExecution(conditionalExecution);
                                
                            }), NodeUtil.GetNodeGraphCreateMenu(nodeType));
                        }
                    }
                }
            }
        }

        public IEnumerable<Type> GetEnumerableOfType(Type t)
        {
            List<Type> types = new List<Type>();
            var assemblies = AppDomain.CurrentDomain.GetAssemblies();

            foreach (var assembly in assemblies)
            {
                foreach (var type in assembly.GetTypes().Where(myType => myType.IsClass && !myType.IsAbstract && t.IsAssignableFrom(myType)))
                {
                    types.Add(type);
                }
            }

            return types;
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
                title.fontSize = 12;
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
       
        public Inspector GetPortInspector(IPort port, Metadata metadata)
        {
            return null;
        }
    }
}