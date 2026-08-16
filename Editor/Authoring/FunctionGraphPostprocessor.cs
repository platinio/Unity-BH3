using ArcaneOnyx.VisualScriptingExtension;
using UnityEditor;

namespace ArcaneOnyx.BehaviorTree.Authoring
{
    /// <summary>
    /// Drops cached binding plans when a Function is imported.
    /// <para>
    /// Without this the invalidation path is theoretical: editing a Function's ports while the editor is
    /// running would leave every live binding evaluating against the port references it resolved before the
    /// edit. Import is the one moment that reliably signals the change, and it is an editor-only concern —
    /// assets cannot change in a player build, which is why nothing re-checks per evaluation.
    /// </para>
    /// </summary>
    internal sealed class FunctionGraphPostprocessor : AssetPostprocessor
    {
        private static void OnPostprocessAllAssets(
            string[] importedAssets,
            string[] deletedAssets,
            string[] movedAssets,
            string[] movedFromAssetPaths)
        {
            var treeChanged = false;

            foreach (var path in importedAssets)
            {
                if (!path.EndsWith(".asset", System.StringComparison.OrdinalIgnoreCase)) continue;

                if (AssetDatabase.LoadAssetAtPath<FunctionGraphAsset>(path) != null)
                {
                    // One Function changing is enough to make every plan suspect, and plans are cheap to
                    // rebuild. Bumping the evaluator's version is also what re-checks the canvas problem
                    // badges, which invalidate off the same counter so they cannot disagree with it.
                    FunctionEvaluator.InvalidateAll();
                    return;
                }

                // A sub-tree's contract lives on a behavior tree asset, which the evaluator has no opinion
                // about -- so a caller's drift against it needs its own signal.
                if (AssetDatabase.LoadAssetAtPath<BehaviorTreeGraphAsset>(path) != null) treeChanged = true;
            }

            if (treeChanged) NodeProblemCache.Invalidate();
        }
    }
}
