using System;
using UnityEditor;
using UnityEngine;

namespace ArcaneOnyx.BehaviorTree.Authoring
{
    /// <summary>
    /// Destroys a tree's orphaned script-graph sub-assets when that tree is saved.
    ///
    /// <para>
    /// <b>This is the only thing in the project that deletes a graph.</b> That is the invariant the whole
    /// repository-removal sequencing exists to protect (spec 10, locked decision 6): a lint may name an
    /// orphan, and only this may destroy one. The project-wide ledger, its <c>Instance</c> lookup and the
    /// canvas's per-GUI-event sweep were removed in the same change that added this file, so there is never
    /// a moment where two mechanisms are both deciding what to delete.
    /// </para>
    ///
    /// <para>
    /// <b>Save, not change.</b> The sweep this replaces ran on every GUI event -- layout, repaint, every
    /// mouse-move -- and destroyed whatever was unreferenced at that instant, un-undoably. Its real defect
    /// was not the cost but the timing: an element that transiently reports no graphs, mid-undo or
    /// mid-deserialize, had its graph taken by a pass that happened to run first. A save is a moment an
    /// author has committed to, which is the same reason <see cref="GuardScheduleSeeder"/> hooks it.
    /// </para>
    ///
    /// <para>
    /// <c>OnWillSaveAssets</c> runs <em>before</em> the write, so a sub-asset destroyed here is absent from
    /// the file the same save produces. An <c>AssetPostprocessor</c> would delete it after the write and
    /// leave the tree dirty again immediately.
    /// </para>
    ///
    /// <para>
    /// <b>Deleting is not undoable, and that is the accepted trade.</b> <c>DestroyImmediate</c> on a
    /// sub-asset cannot be recorded for undo. What replaces undo is <em>when</em> this runs: nothing is
    /// destroyed while an author is mid-edit, so an orphan that reappears before the next save -- an undo
    /// that puts the node back -- is never a candidate in the first place.
    /// </para>
    /// </summary>
    /// <remarks>
    /// <c>bt_verify</c> reaches this indirectly: <see cref="BehaviorTreeVerification.Reload"/> begins with
    /// <c>AssetDatabase.SaveAssets()</c>, so verifying a tree with unsaved changes cleans it up before the
    /// orphan lint looks. That is the invariant working rather than a surprise -- the lint's remaining job
    /// is the tree that arrives already carrying an orphan on disk, from an older version of this tool or
    /// from another project, and which nothing in this session has dirtied.
    /// </remarks>
    internal sealed class OrphanedScriptGraphCleanup : UnityEditor.AssetModificationProcessor
    {
        private static string[] OnWillSaveAssets(string[] paths)
        {
            if (paths == null) return paths;

            // Play mode inherits the old sweep's one sensible guard. A running agent holds live references
            // into the graphs of the tree it is executing, and a save during play is not an authoring act.
            if (EditorApplication.isPlaying) return paths;

            foreach (var path in paths)
            {
                if (string.IsNullOrEmpty(path)) continue;
                if (!path.EndsWith(".asset", StringComparison.OrdinalIgnoreCase)) continue;

                try
                {
                    var tree = AssetDatabase.LoadAssetAtPath<BehaviorTreeGraphAsset>(path);
                    if (tree == null) continue;

                    foreach (var orphan in OrphanedScriptGraphs.Of(tree, path))
                    {
                        // Said out loud, for the same reason the guard seeder says what it seeds: an asset
                        // that quietly loses content during a save is what a reviewer finds in a diff and
                        // cannot explain. The graph is named, so an author who wanted to keep it knows
                        // which one to recover from version control.
                        Debug.Log(
                            $"[BehaviorTree] Removed orphaned script graph '{orphan.name}' from " +
                            $"'{System.IO.Path.GetFileNameWithoutExtension(path)}'. Nothing in the tree " +
                            "referenced it any more.");

                        UnityEngine.Object.DestroyImmediate(orphan, true);
                    }
                }
                catch (Exception exception)
                {
                    // A save must complete. Failing to clean up costs a stale sub-asset, which the orphan
                    // lint still names; failing to save costs an author their work.
                    Debug.LogWarning(
                        $"[BehaviorTree] Could not clean up orphaned script graphs in '{path}': {exception.Message}");
                }
            }

            return paths;
        }
    }
}
