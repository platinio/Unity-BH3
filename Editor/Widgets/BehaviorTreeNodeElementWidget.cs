using System;
using System.Collections.Generic;
using System.Linq;
using ArcaneOnyx.GraphCore;
using Unity.VisualScripting;
using UnityEditor;
using UnityEngine;
using GraphGUI = ArcaneOnyx.GraphCore.GraphGUI;
using MouseButton = Unity.VisualScripting.MouseButton;

namespace ArcaneOnyx.BehaviorTree
{
    [Widget(typeof(BehaviorTreeNode))]
    public class BehaviorTreeNodeElementWidget : GraphCore.GraphElementWidget<BehaviorTreeCanvas, BehaviorTreeNode>, IBehaviorTreeWidget
    {
        public IBehaviorTreeNode behaviorTreeNode => node;
        protected BehaviorTreeNode node => element;
        
        private UnitDescription description;
        public Rect portsBackgroundPosition { get; private set; }

        protected NodeShape shape => NodeShape.Hex;
        
        public override IEnumerable<GraphCore.IWidget> positionDependers => ports.Cast<GraphCore.IWidget>();
        
        protected readonly List<IPortWidget> ports = new List<IPortWidget>();

        protected readonly List<IPortWidget> inputs = new List<IPortWidget>();

        protected readonly List<IPortWidget> outputs = new List<IPortWidget>();

        private readonly List<string> settingNames = new List<string>();
        private float currentInnerWidth;
        
        public override IEnumerable<GraphCore.IWidget> subWidgets => element.ports.Select(port => canvas.Widget(port));

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

        protected const float TITLE_HEIGHT = 25.0f;

        /// <summary>How far the dark outline behind a node extends past it, in total across each axis.</summary>
        private const float OUTSIDE_BOX_BORDER = 10.0f;

        /// <summary>
        /// What a node reserves above and below its port rows: the icon or title band at the top, and the
        /// name drawn across the bottom of the box.
        /// <para>
        /// Public because <see cref="Authoring.ContractPortLayout"/> sizes a node to its contract outside the
        /// draw path and has to arrive at the same number. It used to carry its own copy with a comment
        /// saying this one could not be referenced.
        /// </para>
        /// </summary>
        public const float HEADER_AND_FOOTER_HEIGHT = 70.0f;

        /// <summary>
        /// Vertical space reserved above the ports, for a node that draws something there. Zero here, where
        /// the title is drawn across the box and the ports share the space with it — which is legible only
        /// while every port is a bare label. A port showing an inline field draws into the title, so a node
        /// kind that can have one reserves a band and lays its ports out underneath.
        /// </summary>
        protected virtual float HeaderHeight => 0.0f;

        /// <summary>
        /// Whether this widget lets the base size its height to the ports. False for a node that computes its
        /// own rect — <see cref="ConditionalExecutionWidget"/> is anchored to its owner and sizes itself, so
        /// the base writing a height would be overwritten a moment later anyway.
        /// </summary>
        protected virtual bool SizesHeightToPorts => !node.ShowIcon;

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
        
        public override void ExpandDragGroup(HashSet<GraphCore.IGraphElement> dragGroup)
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
        
        /// <summary>
        /// Stand-in port markers for the sub-tree preview, at each port's real handle position rather than
        /// evenly spread along the node's edge — the preview draws the wires between previewed nodes, and a
        /// wire ends at the handle, so a marker anywhere else has a wire pointing beside it.
        /// </summary>
        protected virtual void DrawPortPreviews(Vector2 offset)
        {
            const float size = 8.0f;

            DrawPortHandlePreviews(inputs, offset, size);
            DrawPortHandlePreviews(outputs, offset, size);
        }

        private static void DrawPortHandlePreviews(List<IPortWidget> portWidgets, Vector2 offset, float size)
        {
            foreach (var portWidget in portWidgets)
            {
                var center = portWidget.handlePosition.center + offset;
                var dot = new Rect(center.x - (size * 0.5f), center.y - (size * 0.5f), size, size);
                Styles.background.Draw(dot, false, false, false, false);
            }
        }

        /// <summary>Set while this widget is being drawn inside another tree's sub-tree preview.</summary>
        protected bool previewPorts;

        /// <summary>
        /// Draws this node as part of a parent tree's sub-tree preview: offset into the parent's box, not
        /// selectable, and with stand-in port markers. Kept as its own entry point so the overrides of
        /// <see cref="DrawForeground(Vector2, bool, bool)"/> do not all need a new parameter.
        /// </summary>
        public void DrawSubTreePreview(Vector2 offset)
        {
            previewPorts = true;

            try
            {
                DrawForeground(offset, true, false);
            }
            finally
            {
                previewPorts = false;
            }
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
                
               
                using (LudiqGUI.color.Override( element.Color))
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

                    if (element.CanBeUsedAsTransitionDestination)
                    {
                        var topConnection = new Rect();
                        topConnection.x = p.x + (p.width / 2.0f) - (connectorSize / 2.0f);
                        topConnection.y = p.y - connectorSize + horizontalOffset;
                        topConnection.width = connectorSize;
                        topConnection.height = connectorSize;
                    
                        Styles.background.Draw(topConnection, false, IsSelected, false, false);
                    }

                    if (previewPorts) DrawPortPreviews(offset);
                }

                if (useSelection) GraphDrawer.DrawSelectionBox(p, GetBorderThickness(), Color.cyan);

                if (node.ShowIcon)
                {
                    DrawIcon(offset);
                }
                
                DrawTitle(offset, element.NodeName);
                DrawLastExecutionIcon(offset);
                DrawProblemBadge(p);
            }
        }

        /// <summary>#FF4747 — the red the badge and its border use for an error.</summary>
        private static readonly Color ErrorRed = new Color(1.0f, 0.28f, 0.28f);

        /// <summary>#FFC226 — the amber the badge and its border use for a warning.</summary>
        private static readonly Color WarningAmber = new Color(1.0f, 0.76f, 0.15f);

        /// <summary>
        /// Unity's built-in console icons, so a problem here reads as the same kind of thing as a problem
        /// anywhere else in the editor. Names, not art: <c>EditorGUIUtility.IconContent</c> resolves them
        /// against the running skin, which is also why a missing one is tolerated at the call site.
        /// </summary>
        private const string ERROR_ICON = "console.erroricon.sml";

        private const string WARNING_ICON = "console.warnicon.sml";

        /// <summary>Border weight of the problem outline, in pixels.</summary>
        private const int PROBLEM_BORDER_THICKNESS = 2;

        /// <summary>
        /// Marks a node that is wrong before anyone runs it.
        ///
        /// <para>
        /// Contract drift, an unfed required port and a missing reference were all previously invisible until
        /// Play threw — which meant the canvas showed a healthy node for a tree that could not work. The
        /// badge is drawn from <see cref="Authoring.NodeProblemCache"/>, which computes rarely and is read
        /// per frame; see that class for why the freshness is tied to the evaluator's own invalidation
        /// counter rather than to a timer.
        /// </para>
        ///
        /// <para>
        /// Unity's own console icons are used rather than new art, so an error here reads as the same kind of
        /// thing as an error anywhere else in the editor.
        /// </para>
        /// </summary>
        /// <param name="nodeRect">
        /// The node's box <em>already shifted</em> by whatever offset the caller draws at — a sub-tree
        /// preview passes a large one. Everything here is measured from this rect and nothing else, which is
        /// why there is no separate offset parameter to forget to add.
        /// </param>
        protected void DrawProblemBadge(Rect nodeRect)
        {
            if (!Authoring.NodeProblemCache.TryGetWorst(element, out var severity, out var count)) return;

            var isError = severity == NodeProblemSeverity.Error;
            var tint = isError ? ErrorRed : WarningAmber;

            // A border rather than a fill: the node's own colour still has to read, and a tinted node looks
            // like a node type rather than a node in trouble.
            GraphDrawer.DrawSelectionBox(nodeRect, PROBLEM_BORDER_THICKNESS, tint);

            var icon = EditorGUIUtility.IconContent(isError ? ERROR_ICON : WARNING_ICON);
            if (icon?.image == null) return;

            // Top-left. The execution-status icon owns the opposite corner, and on a narrow node -- a guard,
            // or anything at the default 150 width -- the two corners are close enough that a badge on the
            // right sat on top of it.
            var badge = new Rect(nodeRect.x + 2.0f, nodeRect.y - 6.0f, 18.0f, 18.0f);
            GUI.DrawTexture(badge, icon.image, ScaleMode.ScaleToFit);

            if (count > 1)
            {
                // Reads outward from the icon, away from the node, so the number never lands on the title.
                var countRect = new Rect(badge.xMax - 3.0f, badge.y - 2.0f, 18.0f, 14.0f);
                GUI.Label(countRect, count.ToString(), Styles.problemCount);
            }

            // Hovering is how the reader gets from "something is wrong" to "this is wrong and here is the
            // fix" without leaving the canvas or opening a console.
            GUI.Label(badge, new GUIContent(string.Empty, Authoring.NodeProblemCache.DescriptionOf(element)));
        }

        /// <summary>
        /// The breakpoint dot and the stopped-on ring.
        ///
        /// <para>
        /// In the overlay pass rather than in <see cref="DrawForeground(Vector2,bool,bool)"/>, which is what
        /// makes it correct on every node instead of on most of them. Two things were wrong with drawing it in
        /// the foreground. A guard's widget takes its owner's <c>zIndex</c> plus one, so a dot drawn in the
        /// owner's foreground landed <em>underneath</em> the conditional executions stacked above it — and
        /// those sit exactly where the dot goes. And both <see cref="ConditionalExecutionWidget"/> and
        /// <see cref="RunBehaviorTreeNodeElementWidget"/> replace the foreground body without calling base, so
        /// a sub-tree node never drew a dot at all.
        /// </para>
        ///
        /// <para>
        /// The canvas runs <c>DrawWidgetsOverlay</c> after <em>every</em> widget's foreground, so one override
        /// here covers all three paths and nothing has to be repeated in a subclass. Any future canvas marker
        /// belongs here for the same reason.
        /// </para>
        /// </summary>
        public override void DrawOverlay()
        {
            base.DrawOverlay();

            if (!e.IsRepaint || !element.IsVisible) return;

            BehaviorTreeBreakpointGizmos.Draw(position, element);
            BehaviorTreePriorityBadge.Draw(position, element);
        }

        /// <summary>The near-black an outside box is tinted with, so the node reads as sitting on top of it.</summary>
        private static readonly Color OutsideBoxTint = new Color(0.1f, 0.1f, 0.1f, 1.0f);

        /// <summary>
        /// The dark outline behind a node: the node's own sprite, drawn <see cref="OUTSIDE_BOX_BORDER"/> px
        /// larger on each axis and tinted near-black.
        ///
        /// <para>
        /// The background is assigned here rather than relied on, because <c>Styles.background</c> is shared
        /// by every node — left to itself it holds whichever sprite the previous node drew with.
        /// </para>
        /// </summary>
        protected virtual void DrawOutsideBox(Rect p)
        {
            Rect outsideBox = p;
            outsideBox.position += new Vector2(-OUTSIDE_BOX_BORDER / 2.0f, -OUTSIDE_BOX_BORDER / 2.0f);
            outsideBox.width += OUTSIDE_BOX_BORDER;
            outsideBox.height += OUTSIDE_BOX_BORDER;

            Styles.background.normal.background = element.NodeBackground;

            using (LudiqGUI.color.Override(OutsideBoxTint))
            {
                Styles.background.Draw(outsideBox, false, IsSelected, false, false);
            }
        }

        /// <summary>
        /// One style per <see cref="ExecutionStatus"/>, built the first time that status is drawn.
        ///
        /// <para>
        /// This is drawn for every node on every repaint, and it used to allocate a <see cref="GUIStyle"/>
        /// and build a resource path by string interpolation each time — roughly 3,600 of each per second on
        /// a 60-node tree. Both are constant per status, so both belong outside the draw call.
        /// </para>
        ///
        /// <para>
        /// Entries are kept even when the texture resolves to null: <c>Inactive</c> and <c>Running</c> have no
        /// art, and re-asking <see cref="Resources"/> for a file that is not there every repaint is the cost
        /// this removes. The styles hold their textures alive, so an unused-asset sweep cannot pull one out
        /// from under a cached entry, and a domain reload clears the whole array anyway.
        /// </para>
        /// </summary>
        private static readonly GUIStyle[] executionStatusStyles =
            new GUIStyle[Enum.GetValues(typeof(ExecutionStatus)).Length];

        private static GUIStyle ExecutionStatusStyle(ExecutionStatus status)
        {
            var index = (int)status;

            if (index < 0 || index >= executionStatusStyles.Length) return null;

            if (executionStatusStyles[index] == null)
            {
                executionStatusStyles[index] = new GUIStyle
                {
                    normal = { background = Resources.Load<Texture2D>($"ExecutionStatus/{status}") }
                };
            }

            return executionStatusStyles[index];
        }

        protected void DrawLastExecutionIcon(Vector2 offset)
        {
            // While the timeline scrubber is parked on a tick this is the node's status *then*, not now. The
            // override falls back to the live value when nothing is scrubbing, so there is one code path.
            var status = BehaviorTreeScrubOverride.StatusOf(element, element.LastExecutionStatus);

            if (status == ExecutionStatus.None) return;

            var style = ExecutionStatusStyle(status);

            if (style == null) return;

            Rect p = LastExecutionStateIconRect;
            p.position += offset + new Vector2(-10, 20);

            using (BehaviorTreeScrubOverride.Ghost())
            {
                style.Draw(p, false, IsSelected, false, false);
            }
        }

        protected int GetBorderThickness()
        {
            int borderThickness = 0;
           
            if (position.Contains(mousePosition)) borderThickness++;
            if (IsSelected) borderThickness++;

            return borderThickness;
        }

        /// <summary>
        /// One shared style whose background is swapped per node, the way <c>Styles.background</c> already
        /// works — a node's icon varies, so there is nothing to cache per node, but the style around it does
        /// not and used to be allocated for every node on every repaint.
        ///
        /// <para>
        /// Assigned immediately before the draw and read nowhere else. A shared style read after some other
        /// node wrote it is exactly how the outside-box border came to be drawn from whatever sprite the
        /// previous node left behind, so the write and the use stay in the same two lines.
        /// </para>
        /// </summary>
        private static readonly GUIStyle iconStyle = new GUIStyle();

        protected void DrawIcon(Vector2 offset)
        {
            iconStyle.normal.background = element.NodeIcon;

            Rect p = IconRect;
            p.position += offset;

            iconStyle.Draw(p, false, IsSelected, false, false);
        }

        protected virtual void DrawTitle(Vector2 offset, string title)
        {
            Rect p = TittleRect;
            p.position += offset;
            
            Styles.title.Draw(p, title, false, IsSelected, false, false);
        }

        public override void CachePosition()
        {
            var headerHeight = HeaderHeight;
            var edgeOrigin = element.Position.position;
            var innerOrigin = EdgeToInnerPosition(new Rect(edgeOrigin, Vector2.zero)).position;
            var edgeX = edgeOrigin.x;
            var innerY = innerOrigin.y;
            var y = innerY;
            var innerWidth = currentInnerWidth;
            var edgeWidth = InnerToEdgePosition(new Rect(0, 0, innerWidth, 0)).width;
            y = innerY + headerHeight;

            if (SizesHeightToPorts)
            {
                Rect newPosition = position;
                newPosition.height = HEADER_AND_FOOTER_HEIGHT + GetPortSectionHeight();
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

        /// <summary>
        /// Lays this node'''s ports out and draws them, for a subclass that replaces the whole node body and so
        /// never reaches the base drawing that would normally do it. <see cref="RunBehaviorTreeNodeElementWidget"/>
        /// draws a sub-tree as a group frame rather than a box, and still needs its parameter ports.
        /// </summary>
        protected void CachePortPositions(float y, float edgeX, float edgeWidth) => CachePortPosition(y, edgeX, edgeWidth);

        /// <summary>Positions the child widgets — the ports among them — without the node box layout.</summary>
        protected void CacheChildWidgetPositions() => base.CachePosition();

        /// <summary>
        /// The full layout pass a sub-tree preview needs before this node can be drawn or wired into.
        /// The canvas gives every widget it runs this same sequence; a preview drives widgets by hand,
        /// and skipping any step shows: without the item caches the port list is empty and measuring a
        /// port dereferences a description it does not have, and without the port position pass the
        /// handle rects the wires and port markers read stay at the origin.
        /// </summary>
        public void CacheForPreview()
        {
            CacheItem();

            foreach (var port in ports) port.CacheItem();

            CachePosition();

            foreach (var port in ports) port.CachePosition();
        }

        /// <summary>Draws the child widgets, which is what puts ports on the canvas.</summary>
        protected void DrawChildWidgets() => base.DrawForeground();

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

        /// <summary>
        /// How tall one column of ports draws, matching what <see cref="CachePortPosition"/> actually lays
        /// out: every row's height, plus the spacing <em>between</em> rows.
        ///
        /// <para>
        /// This used to sum the rows and then subtract one <c>spaceBetweenPorts</c> it had never added, while
        /// asking <c>inputs.Count</c> whichever column it was handed — so the outputs column was measured
        /// against the number of inputs. Both directions under-reported, which is why a node's port
        /// background drew past its own bottom edge: measurably 4px on a one-port node, growing with the
        /// port count.
        /// </para>
        /// </summary>
        private float GetPortsHeight(IReadOnlyCollection<IPortWidget> portWidgets)
        {
            if (portWidgets.Count == 0) return 0.0f;

            float height = 0;

            foreach (var port in portWidgets)
            {
                height += port.GetHeight();
            }

            return height + ((portWidgets.Count - 1) * Styles.spaceBetweenPorts);
        }

        /// <summary>The taller of the two port columns, since both are drawn from the same top edge.</summary>
        protected float GetPortSectionHeight()
        {
            float inputHeight = GetPortsHeight(inputs);
            float ouputHeight = GetPortsHeight(outputs);

            return inputHeight > ouputHeight ? inputHeight : ouputHeight;
        }

        /// <summary>
        /// The whole vertical space the ports occupy below the header: the gap before them, the background's
        /// own padding, and the rows. What a widget sizing itself to its ports has to reserve, as opposed to
        /// <see cref="GetPortSectionHeight"/>, which is the rows alone.
        /// </summary>
        protected float PortBlockHeight =>
            Styles.spaceBeforePorts
            + Styles.portsBackground.padding.top
            + GetPortSectionHeight()
            + Styles.portsBackground.padding.bottom;

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
                else if (destination.CanBeUsedAsTransitionDestination)
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

                foreach (var breakpointOption in BreakpointOptions())
                {
                    yield return breakpointOption;
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
                                UndoUtility.RecordEditedObject("Create Node");
                                BehaviorTreeNode selectedNode = selection.First() as BehaviorTreeNode;

                                var conditionalExecution = Activator.CreateInstance(nodeType) as ConditionalExecution;
                                conditionalExecution.UpdateOwner(selectedNode);
                                conditionalExecution.Position = new Rect(element.position, conditionalExecution.StartingSize);

                                graph.elements.Add(conditionalExecution);
                                selection.Select(conditionalExecution);
                                GUI.changed = true;
                           
                            }), NodeUtil.GetNodeGraphCreateMenu(nodeType));
                        }
                    }
                }
            }
        }

        /// <summary>
        /// The Breakpoint submenu for this node.
        ///
        /// <para>
        /// A guard gets a different set from an ordinary node, because the two have different lives: a node
        /// enters, exits, is aborted or is refused entry, while a guard only ever changes its mind. Offering a
        /// node's four moments on a <see cref="ConditionalExecution"/> would list three that can never fire —
        /// guards are pulled by their owner rather than entered, so they emit no lifecycle events at all.
        /// </para>
        ///
        /// <para>
        /// Ticks are drawn into the labels rather than by the dropdown, which has no notion of a checked item.
        /// </para>
        /// </summary>
        private IEnumerable<DropdownOption> BreakpointOptions()
        {
            if (element == null || !element.IsVisible) yield break;

            if (element is ConditionalExecution guard)
            {
                var armed = Debugging.BehaviorTreeBreakpoints.ForGuard(guard.guid);

                foreach (var option in GuardOptions(guard, armed)) yield return option;

                if (armed != null)
                {
                    foreach (var option in SharedOptions(armed)) yield return option;
                }

                yield break;
            }

            var onNode = Debugging.BehaviorTreeBreakpoints.ForNode(element.guid);

            yield return NodeMomentOption(onNode, Debugging.BehaviorTreeNodeBreakEvents.Enter, "Break on Enter");
            yield return NodeMomentOption(onNode, Debugging.BehaviorTreeNodeBreakEvents.Exit, "Break on Exit");
            yield return NodeMomentOption(onNode, Debugging.BehaviorTreeNodeBreakEvents.Aborted, "Break on Abort");
            yield return NodeMomentOption(onNode, Debugging.BehaviorTreeNodeBreakEvents.Skipped, "Break on Skip");

            if (onNode != null)
            {
                foreach (var option in SharedOptions(onNode)) yield return option;
            }
        }

        private DropdownOption NodeMomentOption(
            Debugging.BehaviorTreeBreakpoint armed, Debugging.BehaviorTreeNodeBreakEvents moment, string label)
        {
            var node = element;
            var isOn = armed != null && (armed.Events & moment) != 0;

            return new DropdownOption(
                (Action)(() => BehaviorTreeBreakpointStore.ToggleNodeEvent(node, moment, context)),
                $"Breakpoint/{Tick(isOn)}{label}");
        }

        private IEnumerable<DropdownOption> GuardOptions(
            ConditionalExecution guard, Debugging.BehaviorTreeBreakpoint armed)
        {
            yield return GuardOption(guard, armed, Debugging.BehaviorTreeGuardBreakOn.EitherWay, "Break when it changes");
            yield return GuardOption(guard, armed, Debugging.BehaviorTreeGuardBreakOn.BecameTrue, "Break when it becomes true");
            yield return GuardOption(guard, armed, Debugging.BehaviorTreeGuardBreakOn.BecameFalse, "Break when it becomes false");
        }

        private DropdownOption GuardOption(
            ConditionalExecution guard,
            Debugging.BehaviorTreeBreakpoint armed,
            Debugging.BehaviorTreeGuardBreakOn breakOn,
            string label)
        {
            // The three directions are one setting rather than a mask, so choosing the one already chosen
            // disarms it — otherwise the only way off a guard breakpoint would be Remove.
            var isOn = armed != null && armed.GuardBreakOn == breakOn;

            return new DropdownOption(
                (Action)(() =>
                {
                    if (isOn) BehaviorTreeBreakpointStore.Remove(armed);
                    else BehaviorTreeBreakpointStore.SetGuard(guard, breakOn, context);
                }),
                $"Breakpoint/{Tick(isOn)}{label}");
        }

        private static IEnumerable<DropdownOption> SharedOptions(Debugging.BehaviorTreeBreakpoint armed)
        {
            yield return new DropdownOption(
                (Action)(() => BehaviorTreeBreakpointStore.SetEnabled(armed, !armed.Enabled)),
                $"Breakpoint/{Tick(armed.Enabled)}Enabled");

            yield return new DropdownOption(
                (Action)(() => BehaviorTreeBreakpointStore.Remove(armed)),
                "Breakpoint/Remove Breakpoint");
        }

        private static string Tick(bool on) => on ? "✔ " : "     ";

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

            /// <summary>The "3" on a node carrying more than one problem.</summary>
            public static readonly GUIStyle problemCount = new GUIStyle
            {
                fontSize = 10,
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleLeft,
                normal = { textColor = new Color(1.0f, 0.85f, 0.85f) }
            };
            
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
            return metadata?.Inspector();
        }
    }
}