using System.Collections.Generic;
using ArcaneOnyx.GraphCore;
using Unity.VisualScripting;
using UnityEditor;
using UnityEngine;

namespace ArcaneOnyx.BehaviorTree
{
    /// <summary>
    /// Gives a Function-backed Script Graph Variable the same right-click refresh a sub-tree node has.
    ///
    /// <para>
    /// The two nodes declare ports from a contract copy for identical reasons, drift for identical reasons,
    /// and are repaired by the same two verbs — so one of them offering the repair on the canvas while the
    /// other could only be fixed from the CLI was an asymmetry, not a design. It mattered most for the path
    /// that has no CLI in it at all: a Function assigned through the inspector runs no authoring code, so
    /// before this the only in-editor signal was <c>bt_verify</c>.
    /// </para>
    ///
    /// <para>
    /// The node otherwise draws through the generic <c>BehaviorTreeNodeElementWidget</c>, and still does —
    /// this subclass adds a menu entry and nothing else.
    /// </para>
    /// </summary>
    [Widget(typeof(VisualScriptGraphVariable))]
    public class VisualScriptGraphVariableWidget : BehaviorTreeNodeElementWidget
    {
        public VisualScriptGraphVariableWidget(BehaviorTreeCanvas canvas, BehaviorTreeNode element)
            : base(canvas, element)
        {
        }

        protected override IEnumerable<DropdownOption> contextOptions
        {
            get
            {
                foreach (var dropdownOption in base.contextOptions)
                {
                    yield return dropdownOption;
                }

                if (element is not VisualScriptGraphVariable variableNode || variableNode.Function == null)
                {
                    yield break;
                }

                var drift = variableNode.DescribeContractDrift();

                var label = drift.Count == 0
                    ? "Refresh Ports (up to date)"
                    : $"Refresh Ports ({drift.Count} change(s) in {variableNode.Function.name})";

                yield return new DropdownOption((System.Action)(() =>
                {
                    UndoUtility.RecordEditedObject("Refresh Function Ports");

                    if (drift.Count > 0)
                    {
                        Debug.Log($"[{variableNode.NodeName}] refreshed ports:{System.Environment.NewLine}  " +
                                  string.Join(System.Environment.NewLine + "  ", drift));
                    }

                    // Losing a wire is a warning rather than a log: it is the one part of a refresh the
                    // author did not ask for and cannot undo.
                    foreach (var line in variableNode.RefreshParameters())
                    {
                        Debug.LogWarning($"[BehaviorTree] {line}");
                    }

                    Authoring.ContractPortLayout.ResizeToFitPorts(variableNode);

                    // The refresh is the fix for whatever the badge was reporting, so it has to stop
                    // reporting it now rather than at the next import.
                    Authoring.NodeProblemCache.Invalidate();

                    GUI.changed = true;
                }), label);
            }
        }
    }
}
