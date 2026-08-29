using Unity.VisualScripting;
using UnityEngine;

namespace ArcaneOnyx.BehaviorTree.Authoring
{
    /// <summary>
    /// Performs the repair a <see cref="NodeProblem"/> carries — the editor half of the split
    /// <see cref="NodeProblemRepair"/> documents. The problem says what would fix it; this is the one place
    /// that knows how each kind is performed: what gets an undo entry, what is logged, what is resized, and
    /// which caches stop being true.
    /// </summary>
    public static class NodeProblemRepairs
    {
        /// <summary>
        /// Applies <paramref name="repair"/> to <paramref name="node"/>. Returns false when nothing was
        /// changed — the node cannot perform this kind, or the author cancelled a confirmation.
        ///
        /// <para>
        /// <paramref name="owner"/> is the object the undo entry is recorded against, resolved the way
        /// <see cref="FunctionAssignment"/> resolves it: capture <c>LudiqEditorUtility.editedObject.value</c>
        /// while drawing and pass it in. Null means "trust the ambient edit bracket", which is only correct
        /// for a caller already running inside the canvas's own event loop — a context-menu verb. From
        /// anywhere else a null owner records nothing, and the edit is silently gone on the next domain
        /// reload.
        /// </para>
        /// </summary>
        public static bool Run(BehaviorTreeNode node, NodeProblemRepair repair, Object owner = null)
        {
            if (node == null || repair == null) return false;

            switch (repair)
            {
                case RefreshContractPortsRepair ports: return RefreshContractPorts(node, ports, owner);
                case RefreshWatchedKeysRepair: return RefreshWatchedKeys(node, owner);
                case DeclareWatchedKeyRepair declare:
                    // Confirms, records against the Function itself, saves and invalidates — the shared-asset
                    // loudness lives there so every surface that declares a key is equally loud.
                    return WatchedKeyRepair.DeclareOnFunction(declare.Function, declare.Key);
                default: return false;
            }
        }

        private static bool RefreshContractPorts(BehaviorTreeNode node, RefreshContractPortsRepair repair, Object owner)
        {
            if (node is not IRefreshesContractPorts holder) return false;

            Recorded(owner, repair.Label, () =>
            {
                // Say what it did. A refresh that removes a port silently removes whatever fed it, and the
                // canvas alone will not make that obvious on a large tree.
                var drift = holder.DescribeContractDrift();

                if (drift.Count > 0)
                {
                    Debug.Log($"[{node.NodeName}] {repair.Label}:{System.Environment.NewLine}  "
                              + string.Join(System.Environment.NewLine + "  ", drift));
                }

                // A lost wire is a warning rather than a log: it is the one part of a refresh the author
                // did not ask for and cannot see happen.
                foreach (var line in holder.RefreshParameters()) Debug.LogWarning($"[BehaviorTree] {line}");

                // The contract just changed, which is the one moment this node is resized. Anywhere near the
                // draw path instead would overwrite an author's own drag.
                ContractPortLayout.ResizeToFitPorts(node);
            });

            NodeProblemCache.Invalidate();

            return true;
        }

        private static bool RefreshWatchedKeys(BehaviorTreeNode node, Object owner)
        {
            if (node is not ReactiveGuard guard) return false;

            Recorded(owner, "Refresh Watched Keys", () =>
            {
                foreach (var line in guard.RefreshWatchedKeys()) Debug.Log($"[BehaviorTree] {line}");
            });

            NodeProblemCache.Invalidate();

            return true;
        }

        /// <summary>
        /// Runs <paramref name="edit"/> under an undo entry named <paramref name="label"/>. With an owner,
        /// it is pushed onto the same override stack <c>GraphContext</c> populates, so
        /// <c>UndoUtility</c> makes its usual three decisions (complete-object undo, dirty only what is not
        /// scene-bound, prefab modifications) against the right object even outside a draw.
        /// </summary>
        private static void Recorded(Object owner, string label, System.Action edit)
        {
            if (owner != null)
            {
                using (LudiqEditorUtility.editedObject.Override(owner))
                {
                    UndoUtility.RecordEditedObject(label);
                    edit();
                }

                return;
            }

            UndoUtility.RecordEditedObject(label);
            edit();
        }
    }
}
