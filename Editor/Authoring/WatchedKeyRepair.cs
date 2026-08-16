using System.Collections.Generic;
using ArcaneOnyx.VisualScriptingExtension;
using UnityEditor;
using UnityEngine;

namespace ArcaneOnyx.BehaviorTree.Authoring
{
    /// <summary>
    /// Declares a watched key on a Function, from wherever the missing declaration was noticed.
    ///
    /// <para>
    /// The repair for a Function whose graph reads a fact it never declared. That gap does not merely make
    /// an asset misleading — it stops every reactive guard reading the Function from waking on that fact, so
    /// the branch quietly stops firing. Refreshing a guard cannot fix it, because a refresh copies the
    /// declaration that is missing the key.
    /// </para>
    ///
    /// <para>
    /// <b>Confirmed before it writes, because the Function is shared.</b> Declaring a key changes behaviour
    /// for every tree reading it, not just the one in front of you, so the dialog counts them and names
    /// them. The change itself is safe in the sense that matters — the graph provably reads the key, so this
    /// makes the declaration true rather than expressing a preference, and it only ever makes guards wake
    /// more often. Silently editing a shared asset from another asset's context menu would still be the
    /// wrong habit to build.
    /// </para>
    /// </summary>
    public static class WatchedKeyRepair
    {
        /// <summary>
        /// Adds <paramref name="key"/> to the Function's declared watched keys after confirming with the
        /// author. Returns true when the declaration was written.
        /// </summary>
        public static bool DeclareOnFunction(FunctionGraphAsset function, string key)
        {
            if (function == null || string.IsNullOrWhiteSpace(key)) return false;

            foreach (var declared in function.WatchedKeys)
            {
                if (declared == key) return false;
            }

            var readers = TreesReading(function);
            var others = Mathf.Max(0, readers.Count - 1);

            var scope = readers.Count <= 1
                ? "No other tree references it."
                : $"{others} other tree{(others == 1 ? "" : "s")} reference{(others == 1 ? "s" : "")} it:"
                  + System.Environment.NewLine + "  " + string.Join(System.Environment.NewLine + "  ", Names(readers, 6));

            var proceed = EditorUtility.DisplayDialog(
                $"Declare '{key}' on {function.name}?",
                $"{function.name}'s graph reads '{key}' but does not declare it, so no reactive guard reading "
                + $"this Function wakes when '{key}' changes."
                + System.Environment.NewLine + System.Environment.NewLine
                + "Declaring it fixes that everywhere this Function is used."
                + System.Environment.NewLine + System.Environment.NewLine
                + scope,
                $"Declare '{key}'",
                "Cancel");

            if (!proceed) return false;

            Undo.RecordObject(function, $"Declare watched key '{key}'");

            var keys = new List<string>(function.WatchedKeys) { key };
            function.SetWatchedKeys(keys.ToArray());

            EditorUtility.SetDirty(function);
            AssetDatabase.SaveAssets();

            // The declaration is what schedules guards, so both the evaluator's caches and every canvas badge
            // are now answering from stale information.
            FunctionEvaluator.Invalidate(function);
            NodeProblemCache.Invalidate();

            Debug.Log($"[BehaviorTree] {function.name} now declares '{key}'. Guards reading it wake on that "
                      + "fact from the next run; any whose own key trigger still omits it will offer a refresh.");

            return true;
        }

        /// <summary>
        /// Behavior trees with a node reading this Function. Loads every tree in the project, which is why it
        /// runs on a click rather than anywhere near a draw.
        /// </summary>
        private static List<BehaviorTreeGraphAsset> TreesReading(FunctionGraphAsset function)
        {
            var readers = new List<BehaviorTreeGraphAsset>();

            foreach (var guid in AssetDatabase.FindAssets("t:BehaviorTreeGraphAsset"))
            {
                var tree = AssetDatabase.LoadAssetAtPath<BehaviorTreeGraphAsset>(
                    AssetDatabase.GUIDToAssetPath(guid));

                if (tree?.graph == null) continue;

                foreach (var node in tree.graph.Nodes)
                {
                    if (node is not IDeclaresWatchedKeys declarer) continue;
                    if (declarer.DeclarationOwner != function) continue;

                    readers.Add(tree);
                    break;
                }
            }

            return readers;
        }

        private static IEnumerable<string> Names(List<BehaviorTreeGraphAsset> trees, int limit)
        {
            for (var i = 0; i < trees.Count && i < limit; i++) yield return trees[i].name;

            if (trees.Count > limit) yield return $"…and {trees.Count - limit} more";
        }
    }
}
