using ArcaneOnyx.VisualScriptingExtension;
using Unity.VisualScripting;
using UnityObject = UnityEngine.Object;

namespace ArcaneOnyx.BehaviorTree
{
    /// <summary>
    /// Points a node at a Function, recorded against the object that actually owns it.
    ///
    /// <para>
    /// Separated from the inspector for one reason: <b>which object gets recorded is the part that was
    /// wrong twice</b>, and it is not testable through an IMGUI callback. First
    /// <c>UndoUtility.RecordEditedObject</c> recorded nothing at all, because the dropdown callback runs
    /// outside the <c>GraphContext.BeginEdit/EndEdit</c> bracket that populates
    /// <c>LudiqEditorUtility.editedObject</c>. Then resolving the tree asset instead fixed that for macro
    /// trees and reproduced it exactly for machine-embedded ones, where
    /// <c>BehaviorTreeCanvas.GetBehaviorTreeGraphAsset()</c> reads <c>machine.GraphAsset</c> —
    /// <c>nest.macro</c>, which is null when the nest source is Embed.
    /// </para>
    ///
    /// <para>
    /// So the owner is passed in rather than discovered here, and the caller captures it where Visual
    /// Scripting itself keeps it: <c>reference.serializedObject</c>, which is the asset for a macro graph
    /// and the <c>BehaviorTreeMachine</c> component for an embedded one.
    /// </para>
    /// </summary>
    public static class FunctionAssignment
    {
        /// <summary>
        /// Assigns <paramref name="function"/> (or null to clear) to <paramref name="node"/>, recording an
        /// undo step against <paramref name="owner"/>.
        ///
        /// <para>
        /// The recording runs through Visual Scripting's own <c>UndoUtility</c> rather than a local
        /// reimplementation, by pushing <paramref name="owner"/> onto the same override stack
        /// <c>GraphContext</c> uses. That matters because <c>UndoUtility</c> does three things this code
        /// would otherwise have to copy and keep in step: <c>RegisterCompleteObjectUndo</c> rather than
        /// <c>RecordObject</c> (the serialized object-reference list must be recorded whole), <c>SetDirty</c>
        /// only for objects that are <em>not</em> scene-bound, and a deferred
        /// <c>RecordPrefabInstancePropertyModifications</c> for prefab instances.
        /// </para>
        ///
        /// <para>
        /// Returns false when there is nothing to record against, which is a real state — a node inspected
        /// with no graph context — and one the caller must report rather than swallow.
        /// </para>
        /// </summary>
        public static bool Apply(VisualScriptGraphVariable node, FunctionGraphAsset function, UnityObject owner)
        {
            if (node == null) return false;

            if (owner == null)
            {
                // Deliberately refuses to mutate. Assigning without recording is the silent-loss failure
                // this whole type exists to stop: the node would change, look assigned, and be gone on the
                // next domain reload with nothing marked unsaved.
                return false;
            }

            using (LudiqEditorUtility.editedObject.Override(owner))
            {
                UndoUtility.RecordEditedObject(function == null ? "Clear Function" : "Assign Function");

                // SetFunction refreshes the contract copy and re-declares the ports, which is what makes the
                // assignment usable without a second action.
                node.SetFunction(function);
            }

            Authoring.ContractPortLayout.ResizeToFitPorts(node);
            Authoring.NodeProblemCache.Invalidate();

            return true;
        }
    }
}
