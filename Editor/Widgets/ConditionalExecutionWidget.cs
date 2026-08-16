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
        
        public const float Height = 40.0f;
        public const float Separation = 5.0f;

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
           
            var owner = (node as ConditionalExecution)?.Owner;
            if (owner == null || !node.graph.elements.Contains(owner)) return;
            
            int index = owner.GetConditionalIndex(node as ConditionalExecution) + 1;
            Vector2 offset = Vector2.zero;

            if (owner is RunBehaviorTreeGraphNode) offset += new Vector2(0.0f, -Height);

            var widget = canvas.Widget(owner);
            Rect p = widget.position;
            p.height = Height;
            p.position -= new Vector2(0, (p.height / 2.0f) * index);
            p.position -= new Vector2(0, ((Height / 2.0f) + Separation) * index);
            p.position += offset;
            
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

                // This override does not chain to the base, so the badge has to be asked for explicitly --
                // which is why guards were the one node kind it never appeared on.
                DrawProblemBadge(offset, position);
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

                // The case a refresh cannot reach: the condition reads a fact nothing declares, so there is
                // nothing for this guard to copy. Named here rather than left to the badge alone, because
                // this menu is where someone comes looking for the fix.
                if (undeclared.Length > 0)
                {
                    var keys = string.Join(", ", undeclared);

                    yield return new DropdownOption((System.Action)(() => Debug.LogWarning(
                            $"[BehaviorTree] '{guard.NodeName}' cannot be repaired from here. Its condition "
                            + $"reads {keys} without declaring it, so this guard never wakes on it. Declare "
                            + "it on the Function the condition reads (fn_set_metadata --watched_keys); "
                            + "refreshing this guard would only copy the declaration that is missing it.")),
                        $"Cannot refresh: condition reads {keys} without declaring it");
                }
                else if (missing == 0)
                {
                    yield return new DropdownOption((System.Action)(() => { }),
                        "Refresh Watched Keys (up to date)");
                }
            }
        }
    }
}