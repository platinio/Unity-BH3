using System.Collections.Generic;
using Unity.VisualScripting;
using UnityEditor;

namespace ArcaneOnyx.BehaviorTree.Authoring
{
    /// <summary>
    /// The script graphs stored inside a tree that nothing in that tree references any more.
    ///
    /// <para>
    /// <b>Sub-asset containment is the whole source of truth.</b> A tree's embedded graphs are its own
    /// sub-assets, so "which graphs belong to this tree" is a question the asset file answers by itself.
    /// That is what replaced the project-wide ledger this feature deleted: a ledger existed only because
    /// nobody had noticed the file already knew.
    /// </para>
    ///
    /// <para>
    /// <b>One implementation, two consumers.</b> <see cref="BehaviorTreeVerification"/> names what this
    /// finds and <c>OrphanedScriptGraphCleanup</c> destroys it. Those must agree — a report that named a
    /// different set than the deleter acted on would be worse than no report at all — so the rule is
    /// written once here rather than twice in the two places that need it.
    /// </para>
    ///
    /// <para>
    /// <b>A standalone Function can never appear in the result.</b> Only sub-assets *of this tree's own
    /// asset path* are candidates, and <c>FunctionGraphAsset</c> derives from <c>Macro&lt;FlowGraph&gt;</c>
    /// rather than <c>ScriptGraphAsset</c> anyway. Both facts have to stay true: the cross-tree deletion
    /// bug class that motivated this feature came from a deleter whose candidate set was project-wide.
    /// </para>
    /// </summary>
    public static class OrphanedScriptGraphs
    {
        /// <summary>
        /// Every script-graph sub-asset at <paramref name="assetPath"/> that no element of
        /// <paramref name="tree"/> still references. Empty when the tree is clean, when it has no
        /// sub-assets, or when it could not be read.
        /// </summary>
        /// <param name="tree">The tree to ask. Its <c>graph</c> supplies the referenced set.</param>
        /// <param name="assetPath">
        /// The path to enumerate sub-assets at. Passed in rather than derived, because
        /// <see cref="BehaviorTreeVerification.Reload"/> hands back an <c>Instantiate</c> clone with no
        /// asset path — deriving it there returned empty and made a lint report nothing at all.
        /// </param>
        public static List<ScriptGraphAsset> Of(BehaviorTreeGraphAsset tree, string assetPath)
        {
            var orphans = new List<ScriptGraphAsset>();

            if (tree == null || string.IsNullOrEmpty(assetPath)) return orphans;

            // A tree whose graph failed to deserialize reports no elements, which would read as "every
            // sub-asset is an orphan" and take the lot. Refusing to answer is the only safe reading of a
            // tree we cannot see the elements of -- this is the timing bug class that made the old
            // per-frame sweep dangerous, and the one thing a deleter must never get wrong.
            var graph = tree.graph;
            if (graph == null) return orphans;

            var referenced = new HashSet<ScriptGraphAsset>();

            foreach (var node in graph.Nodes)
            {
                var graphs = node?.scriptGraphAssets;
                if (graphs == null) continue;

                foreach (var scriptGraph in graphs)
                {
                    if (scriptGraph != null) referenced.Add(scriptGraph);
                }
            }

            foreach (var representation in AssetDatabase.LoadAllAssetRepresentationsAtPath(assetPath))
            {
                if (representation is not ScriptGraphAsset subAsset) continue;
                if (referenced.Contains(subAsset)) continue;

                orphans.Add(subAsset);
            }

            return orphans;
        }
    }
}
