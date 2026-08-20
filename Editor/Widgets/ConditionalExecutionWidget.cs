using ArcaneOnyx.GraphCore;
using Unity.VisualScripting;
using UnityEditor;
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
        
        /// <summary>
        /// The shortest a guard is ever drawn, and what it stays at when its ports fit inside the band its
        /// name occupies. No longer the height of every guard: see <see cref="MeasuredHeight"/>.
        /// </summary>
        public const float Height = 40.0f;

        public const float Separation = 5.0f;

        /// <summary>
        /// The guard's name gets a band of its own at the top, and the ports lay out underneath it.
        ///
        /// <para>
        /// Before this, <c>TittleRect</c> was the entire box and the ports were drawn inside it. That reads
        /// correctly only while every port is a bare label sitting at the left edge, clear of the centred
        /// name — which is exactly the case a guard whose condition is connected presents. Leave the
        /// condition unconnected and the port declares a default, so the widget draws an inline field
        /// stretching right, across the name.
        /// </para>
        /// </summary>
        protected override float HeaderHeight => TITLE_HEIGHT;

        /// <summary>
        /// A guard is anchored to its owner and computes its own rect, so the base must not write a height
        /// that this widget overwrites a moment later.
        /// </summary>
        protected override bool SizesHeightToPorts => false;

        /// <summary>
        /// How tall this guard needs to be: its name band plus whatever its ports occupy, never less than
        /// <see cref="Height"/>.
        ///
        /// <para>
        /// Derived on demand from the port widgets rather than read back from a cached rect, because the
        /// guards in one stack measure each other and nothing orders their layout. A guard asking a sibling
        /// how tall it is must get the same answer whether or not that sibling has been positioned yet.
        /// </para>
        /// </summary>
        public float MeasuredHeight => Mathf.Max(
            Height,
            // Converted from inner space to edge space rather than adding a border by hand: the ports are
            // laid out from the inner origin, and the box being measured here is an edge rect.
            InnerToEdgePosition(new Rect(0.0f, 0.0f, 0.0f, HeaderHeight + PortBlockHeight)).height);

        public ConditionalExecutionWidget(BehaviorTreeCanvas canvas, BehaviorTreeNode element) : base(canvas, element)
        {

        }

        public override void DrawForeground()
        {
            DrawForeground(Vector2.zero, e.IsRepaint);
        }

        public override void CachePosition()
        {
            var guard = node as ConditionalExecution;
            var owner = guard?.Owner;

            if (owner == null || !node.graph.elements.Contains(owner))
            {
                base.CachePosition();
                return;
            }

            var widget = canvas.Widget(owner);

            Rect p = widget.position;
            p.height = MeasuredHeight;

            // Guards stack upwards from the owner's top edge. Every guard below this one contributes its own
            // measured height rather than a shared constant, because they are no longer all the same height.
            p.y -= StackHeightBelow(guard, owner) + Separation + p.height;

            if (owner is RunBehaviorTreeGraphNode) p.y -= Height;

            // Written before the base runs, not after: the base lays the ports out from whatever rect the
            // element currently has, so assigning afterwards left them positioned against the previous
            // frame's box. Harmless while every guard was a fixed 40 tall and never moved; visibly wrong as
            // soon as the height answers to the ports inside it.
            position = p;

            base.CachePosition();

            TittleRect = new Rect(p.x, p.y, p.width, HeaderHeight);

            zIndex = widget.zIndex + 1;
        }

        /// <summary>
        /// The vertical space taken by the guards stacked between <paramref name="guard"/> and its owner —
        /// each one's height plus the gap after it.
        /// </summary>
        private float StackHeightBelow(ConditionalExecution guard, BehaviorTreeNode owner)
        {
            var height = 0.0f;

            foreach (var other in GuardsOf(owner))
            {
                if (other == guard) break;

                height += MeasuredHeightOf(canvas, other) + Separation;
            }

            return height;
        }

        /// <summary>
        /// How much room the whole guard stack above <paramref name="owner"/> occupies, for anything that has
        /// to route around it. <see cref="BehaviorTreeGraphDrawer"/> draws transition lines to the top of the
        /// stack and needs the same answer this widget positions itself with, or the lines land on nothing.
        /// </summary>
        public static float StackHeightAbove(GraphCore.ICanvas canvas, BehaviorTreeNode owner)
        {
            var height = 0.0f;

            foreach (var guard in GuardsOf(owner))
            {
                height += MeasuredHeightOf(canvas, guard) + Separation;
            }

            return height;
        }

        /// <summary>
        /// Falls back to <see cref="Height"/> when a guard has no widget yet, which is the height it would
        /// have been given anyway before any of this was measured.
        /// </summary>
        private static float MeasuredHeightOf(GraphCore.ICanvas canvas, ConditionalExecution guard)
        {
            return canvas.Widget(guard) is ConditionalExecutionWidget widget ? widget.MeasuredHeight : Height;
        }

        /// <summary>
        /// The owner's guards, in the order <see cref="BehaviorTreeNode.GetConditionalIndex"/> numbers them —
        /// graph order, so a guard's place in the stack matches the index everything else quotes.
        /// </summary>
        private static System.Collections.Generic.IEnumerable<ConditionalExecution> GuardsOf(BehaviorTreeNode owner)
        {
            foreach (var graphElement in owner.graph.elements)
            {
                if (graphElement is ConditionalExecution guard && guard.Owner == owner) yield return guard;
            }
        }

        public override void DrawForeground(Vector2 offset, bool IsRepaint, bool useSelection = true)
        {
            if (!element.IsVisible) return;
            
            if (IsRepaint)
            {
                // Shifted like every other rect here: a sub-tree preview passes the translation into its
                // frame, and a box drawn from the bare position lands at the guard's raw sub-graph
                // coordinates — outside the frame — while its own title follows the offset.
                Rect p = position;
                p.position += offset;

                DrawOutsideBox(p);

                using (LudiqGUI.color.Override(element.Color))
                {
                    Styles.background.normal.background = element.NodeBackground;
                    Styles.background.Draw(p, false, IsSelected, false, false);
                }

                if (useSelection) GraphDrawer.DrawSelectionBox(p, GetBorderThickness(), Color.cyan);

                if (node.ShowIcon)
                {
                    DrawIcon(offset);
                }
                
                DrawTitle(offset, element.NodeName);
                DrawLastExecutionIcon(offset);

                // This override does not chain to the base, so the badge has to be asked for explicitly --
                // which is why guards were the one node kind it never appeared on.
                DrawProblemBadge(offset, p);
            }
        }

        /// <summary>
        /// The repair for a guard whose written-down keys have fallen behind its condition (Unity-BH3#22).
        ///
        /// <para>
        /// Offered on the guard rather than applied on save, for the same reason
        /// <c>SeedMissingGuardTriggers</c> refuses to touch a trigger that already exists: the list may hold
        /// hand-typed keys naming a fact no walk can see, and a save that rewrote them would discard a
        /// schedule somebody chose. The entry reports how many keys it would add, so the decision is informed.
        /// </para>
        /// </summary>
        protected override System.Collections.Generic.IEnumerable<DropdownOption> contextOptions
        {
            get
            {
                foreach (var dropdownOption in base.contextOptions)
                {
                    yield return dropdownOption;
                }

                if (element is not ReactiveGuard guard) yield break;

                var declared = InheritedWatchedKeys.Resolve(guard);
                var undeclared = InheritedWatchedKeys.ResolveUndeclaredReads(guard);

                if (declared.Length == 0 && undeclared.Length == 0) yield break;

                var missing = 0;

                foreach (var trigger in guard.Triggers)
                {
                    if (trigger == null || trigger.Kind != GuardTriggerKind.OnKeyChanged) continue;

                    foreach (var key in declared)
                    {
                        if (!string.IsNullOrWhiteSpace(key) && !trigger.Keys.Contains(key)) missing++;
                    }
                }

                // Offered only when it would actually do something. It used to appear unconditionally and
                // read "up to date" whenever the trigger matched the declaration -- including when the badge
                // was warning about an undeclared read, which this cannot fix. A menu entry contradicting
                // the badge beside it is worse than no entry.
                if (missing > 0)
                {
                    yield return new DropdownOption((System.Action)(() =>
                    {
                        UndoUtility.RecordEditedObject("Refresh Watched Keys");

                        foreach (var line in guard.RefreshWatchedKeys()) Debug.Log($"[BehaviorTree] {line}");

                        Authoring.NodeProblemCache.Invalidate();

                        GUI.changed = true;
                    }), $"Refresh Watched Keys ({missing} declared by the condition, not listed here)");
                }

                // The case a refresh cannot reach, offered as the repair it actually needs rather than as an
                // explanation of where the repair lives. The fix is on the Function, so this edits the
                // Function -- named in the label, and confirmed first, because that asset is shared.
                foreach (var declarer in InheritedWatchedKeys.ResolveIncompleteDeclarers(guard))
                {
                    if (declarer.DeclarationOwner is not
                        ArcaneOnyx.VisualScriptingExtension.FunctionGraphAsset function)
                    {
                        // Nowhere to write -- keys hand-declared in C#, or an embedded graph. Say so rather
                        // than offering a button that cannot work.
                        yield return new DropdownOption((System.Action)(() => { }),
                            $"Reads {string.Join(", ", declarer.UndeclaredReadKeys)} undeclared "
                            + "(no asset to fix it on)");
                        continue;
                    }

                    foreach (var key in declarer.UndeclaredReadKeys)
                    {
                        var captured = key;

                        yield return new DropdownOption((System.Action)(() =>
                            Authoring.WatchedKeyRepair.DeclareOnFunction(function, captured)),
                            $"Declare '{key}' on {function.name}");
                    }
                }

                if (undeclared.Length == 0 && missing == 0)
                {
                    yield return new DropdownOption((System.Action)(() => { }),
                        "Refresh Watched Keys (up to date)");
                }
            }
        }
    }
}