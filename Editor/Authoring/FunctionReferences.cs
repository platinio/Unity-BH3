using System.Collections.Generic;
using ArcaneOnyx.VisualScriptingExtension;
using UnityEditor;

namespace ArcaneOnyx.BehaviorTree.Authoring
{
    /// <summary>
    /// Who reads a Function.
    ///
    /// <para>
    /// A Function is shared on purpose — that is the whole point of it being an asset — which means every
    /// question about changing one is really a question about everything referencing it. Answering that is a
    /// project-wide load, so it belongs behind a click and never anywhere near a draw.
    /// </para>
    ///
    /// <para>
    /// Callers are found through <see cref="IDeclaresWatchedKeys.DeclarationOwner"/> rather than by testing
    /// node types, so a future node that reads a Function some other way is found without this being
    /// revisited.
    /// </para>
    /// </summary>
    public static class FunctionReferences
    {
        /// <summary>One tree that reads <paramref name="function"/>, and the nodes in it that do.</summary>
        public readonly struct Caller
        {
            public Caller(BehaviorTreeGraphAsset tree, List<BehaviorTreeNode> nodes)
            {
                Tree = tree;
                Nodes = nodes;
            }

            public BehaviorTreeGraphAsset Tree { get; }

            public List<BehaviorTreeNode> Nodes { get; }
        }

        /// <summary>
        /// Every tree with at least one node reading this Function. Loads every behavior tree in the project.
        /// </summary>
        public static List<Caller> Find(FunctionGraphAsset function)
        {
            var callers = new List<Caller>();
            if (function == null) return callers;

            foreach (var guid in AssetDatabase.FindAssets("t:BehaviorTreeGraphAsset"))
            {
                var path = AssetDatabase.GUIDToAssetPath(guid);
                var tree = AssetDatabase.LoadAssetAtPath<BehaviorTreeGraphAsset>(path);

                if (tree?.graph == null) continue;

                List<BehaviorTreeNode> reading = null;

                foreach (var node in tree.graph.Nodes)
                {
                    if (node is not IDeclaresWatchedKeys declarer) continue;
                    if (declarer.DeclarationOwner != function) continue;

                    reading ??= new List<BehaviorTreeNode>();
                    reading.Add(node);
                }

                if (reading != null) callers.Add(new Caller(tree, reading));
            }

            return callers;
        }

        /// <summary>How many callers hold a contract copy that no longer matches the Function.</summary>
        public static int CountDrifted(List<Caller> callers)
        {
            var drifted = 0;

            foreach (var caller in callers)
            {
                foreach (var node in caller.Nodes)
                {
                    if (node is VisualScriptGraphVariable variable && variable.DescribeContractDrift().Count > 0)
                    {
                        drifted++;
                    }
                }
            }

            return drifted;
        }
    }
}
