using System;
using UnityEditor;
using UnityEngine;

namespace ArcaneOnyx.BehaviorTree.Authoring
{
    /// <summary>
    /// Gives a guard the schedule its condition declares, whenever a tree is saved.
    ///
    /// <para>
    /// <b>One hook rather than one per assignment path.</b> A Function can reach a node through
    /// <c>bt_guard_on_function</c>, through <c>fn_extract</c>, through a hand edit, or through an inspector
    /// field that does not exist yet — and every one of those ends in a save. Hooking the save covers all of
    /// them, including the ones nobody has written, where hooking each assignment site covers only the ones
    /// somebody remembered.
    /// </para>
    ///
    /// <para>
    /// <b>Save, not change.</b> The only thing that observes every graph edit today is the canvas's per-frame
    /// sweep, and that sweep is scheduled for deletion (spec 10, locked decision 6) precisely because
    /// per-frame work on every edit is what made tree editing expensive. Save is where spec 10 is already
    /// moving structural work, and it has the better semantics anyway: it fires when an author has committed
    /// an edit rather than while they are still dragging a wire.
    /// </para>
    ///
    /// <para>
    /// <c>OnWillSaveAssets</c> runs <em>before</em> the write, so a trigger seeded here is serialized by the
    /// same save. The alternative, an <c>AssetPostprocessor</c>, would seed after the file was written and
    /// leave the asset dirty again immediately.
    /// </para>
    /// </summary>
    /// <remarks>
    /// This never removes or replaces a schedule — see <c>BehaviorTreeAuthoring.SeedMissingGuardTriggers</c>,
    /// which only touches guards that have none at all. A save that silently rewrote a schedule an author
    /// chose would be a far worse trade than the every-tick default it is trying to fix.
    /// </remarks>
    internal sealed class GuardScheduleSeeder : UnityEditor.AssetModificationProcessor
    {
        private static string[] OnWillSaveAssets(string[] paths)
        {
            if (paths == null) return paths;

            foreach (var path in paths)
            {
                if (string.IsNullOrEmpty(path)) continue;
                if (!path.EndsWith(".asset", StringComparison.OrdinalIgnoreCase)) continue;

                try
                {
                    var tree = AssetDatabase.LoadAssetAtPath<BehaviorTreeGraphAsset>(path);
                    if (tree == null) continue;

                    var seeded = BehaviorTreeAuthoring.SeedMissingGuardTriggers(tree);
                    if (seeded.Count == 0) continue;

                    // Said out loud, because an asset that quietly gains content during a save is the kind of
                    // thing a reviewer finds in a diff and cannot explain. Each guard is named so the author
                    // can go and look at what it now watches.
                    foreach (var guard in seeded)
                    {
                        Debug.Log(
                            $"[BehaviorTree] Seeded a key trigger on guard '{guard.NodeName}' in " +
                            $"'{System.IO.Path.GetFileNameWithoutExtension(path)}' from what its condition " +
                            "declares. It was re-checking every tick.");
                    }
                }
                catch (Exception exception)
                {
                    // A save must complete. Failing to seed a schedule costs the every-tick default, which is
                    // what the guard was doing anyway; failing to save costs an author their work.
                    Debug.LogWarning($"[BehaviorTree] Could not seed guard schedules in '{path}': {exception.Message}");
                }
            }

            return paths;
        }
    }
}
