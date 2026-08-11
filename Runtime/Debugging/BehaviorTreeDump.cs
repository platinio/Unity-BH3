using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using ArcaneOnyx.UnityExtensions;
using Unity.VisualScripting;
using UnityEngine;

namespace ArcaneOnyx.BehaviorTree.Debugging
{
    /// <summary>
    /// Renders a behavior tree as JSON: the execution hierarchy, what each port is wired to, and which
    /// nodes nothing can reach.
    ///
    /// A graph asset is an opaque serialization blob, so a tree is otherwise only inspectable by opening
    /// the canvas. That makes trees impossible to review in a diff and awkward to verify when something is
    /// building them in code. This walks the same relationships the runtime walks and writes them out flat.
    ///
    /// Children are ordered by <see cref="BehaviorTreeGraph.ChildrenInPriorityOrder"/>, the same rule the
    /// runtime uses. Anything else would misreport execution order, which is the mistake this is here to catch.
    /// </summary>
    public static class BehaviorTreeDump
    {
        /// <summary>
        /// Hard stop on nesting depth. Cycle detection already handles a tree that reaches itself; this only
        /// catches a genuinely pathological chain of distinct assets.
        /// </summary>
        private const int MaxSubTreeDepth = 12;

        /// <summary>
        /// Dumps an asset. Prefer this over the graph overload: seeding the path with the asset lets a tree
        /// that runs itself be reported as recursion straight away instead of being expanded once first.
        /// </summary>
        public static string ToJson(BehaviorTreeGraphAsset asset)
        {
            if (asset == null) return "{ \"asset\": \"(null)\" }";

            var path = new List<BehaviorTreeGraphAsset> { asset };

            return WriteDocument(asset.graph, asset.name, path);
        }

        public static string ToJson(BehaviorTreeGraph graph, string assetName = null)
        {
            return WriteDocument(graph, assetName, new List<BehaviorTreeGraphAsset>());
        }

        private static string WriteDocument(BehaviorTreeGraph graph, string assetName, List<BehaviorTreeGraphAsset> path)
        {
            var json = new JsonWriter();
            WriteGraph(json, graph, assetName, path);

            return json.ToString();
        }

        private static void WriteGraph(JsonWriter json, BehaviorTreeGraph graph, string assetName, List<BehaviorTreeGraphAsset> path)
        {
            json.OpenObject();

            if (!string.IsNullOrEmpty(assetName)) json.Property("asset", assetName);

            if (graph == null)
            {
                json.Property("root", "(graph is null)");
                json.CloseObject();
                return;
            }

            json.Property("nodeCount", graph.Nodes.Count());

            // node identity is per graph, so each nested tree gets its own set
            var visited = new HashSet<BehaviorTreeNode>();

            json.PropertyName("root");
            WriteNode(json, graph, graph.EntryNode, visited, path);

            WriteOrphans(json, graph, visited, path);

            json.CloseObject();
        }

        private static void WriteNode(JsonWriter json, BehaviorTreeGraph graph, BehaviorTreeNode node, HashSet<BehaviorTreeNode> visited, List<BehaviorTreeGraphAsset> path)
        {
            if (node == null)
            {
                json.RawValue("null");
                return;
            }

            json.OpenObject();
            json.Property("name", node.NodeName);
            json.Property("type", node.GetType().Name);
            json.Property("guid", node.guid.ToString());
            json.Property("pos", string.Format(CultureInfo.InvariantCulture, "{0:0},{1:0}", node.Position.x, node.Position.y));

            // a cycle would otherwise recurse forever; report it instead of hanging
            if (!visited.Add(node))
            {
                json.Property("repeat", "already shown above (cycle)");
                json.CloseObject();
                return;
            }

            // One malformed node used to abort the entire asset, which left nothing to diagnose it with —
            // and a malformed node is precisely what a dump is being read to find. Report it in place and
            // keep going, so the rest of the tree still comes out.
            try
            {
                WriteGuards(json, graph, node, visited);
                WriteInputs(json, node);
                WriteScriptGraphs(json, node);
                WriteSubTree(json, node, path);
            }
            catch (System.Exception exception)
            {
                json.Property("error", exception.GetType().Name + ": " + exception.Message);
            }

            WriteChildren(json, graph, node, visited, path);

            json.CloseObject();
        }

        /// <summary>Conditional Executions are attached to an owner rather than parented, so they are listed apart.</summary>
        private static void WriteGuards(JsonWriter json, BehaviorTreeGraph graph, BehaviorTreeNode node, HashSet<BehaviorTreeNode> visited)
        {
            var guards = graph.Nodes
                .OfType<ConditionalExecution>()
                .Where(c => c.Owner == node)
                .ToList();

            if (guards.Count == 0) return;

            json.PropertyName("guards");
            json.OpenArray();

            foreach (var guard in guards)
            {
                visited.Add(guard);

                json.OpenObject();
                json.Property("name", guard.NodeName);
                json.Property("type", guard.GetType().Name);
                json.Property("guid", guard.guid.ToString());
                WriteInputs(json, guard);
                json.CloseObject();
            }

            json.CloseArray();
        }

        private static void WriteInputs(JsonWriter json, BehaviorTreeNode node)
        {
            if (node.valueInputs == null)
            {
                json.Property("ports", "(node was never defined)");
                return;
            }

            var inputs = node.valueInputs.ToList();
            if (inputs.Count == 0) return;

            json.PropertyName("inputs");
            json.OpenObject();

            foreach (var input in inputs)
            {
                var connection = input.connection;

                if (connection != null)
                {
                    var source = connection.source;

                    // ports only know their owner as IBehaviorTreeNode, which carries no display name
                    string sourceName = source.behaviorTreeNode is BehaviorTreeNode sourceNode ? sourceNode.NodeName : "?";

                    json.Property(input.key, "<- " + sourceName + "." + source.key);
                    continue;
                }

                json.Property(input.key,
                    node.defaultValues.TryGetValue(input.key, out var value) ? Describe(value) : "(unset)");
            }

            json.CloseObject();
        }

        private static void WriteChildren(JsonWriter json, BehaviorTreeGraph graph, BehaviorTreeNode node, HashSet<BehaviorTreeNode> visited, List<BehaviorTreeGraphAsset> path)
        {
            var children = graph.ChildrenInPriorityOrder(node);

            if (children.Count == 0) return;

            json.PropertyName("children");
            json.OpenArray();

            foreach (var child in children)
            {
                WriteNode(json, graph, child, visited, path);
            }

            json.CloseArray();
        }

        /// <summary>
        /// Expands a sub-behavior tree in place, to any depth.
        ///
        /// Reads the serialized asset, never <c>BehaviorTreeGraphAssetInstance</c> or
        /// <c>BehaviorTreeGraphInstance</c> — those call <c>Object.Instantiate</c>, so touching them would
        /// have inspecting a tree quietly clone assets.
        ///
        /// The guard is the asset path rather than a global visited set, because the same sub-tree used by
        /// two different branches is normal reuse, not recursion. Only an asset already on the current path
        /// is a cycle, which is the same rule <c>GraphWillCauseRecursion</c> applies at load time.
        /// </summary>
        private static void WriteSubTree(JsonWriter json, BehaviorTreeNode node, List<BehaviorTreeGraphAsset> path)
        {
            if (!(node is RunBehaviorTreeGraphNode runNode)) return;

            json.PropertyName("subTree");

            var asset = runNode.BehaviorTreeGraphAsset;

            if (asset == null)
            {
                json.OpenObject();
                json.Property("asset", "(none assigned)");
                json.CloseObject();
                return;
            }

            if (path.Contains(asset))
            {
                json.OpenObject();
                json.Property("asset", asset.name);
                json.Property("recursion", DescribePath(path, asset));
                json.CloseObject();
                return;
            }

            if (path.Count >= MaxSubTreeDepth)
            {
                json.OpenObject();
                json.Property("asset", asset.name);
                json.Property("truncated", "nesting deeper than " + MaxSubTreeDepth);
                json.CloseObject();
                return;
            }

            path.Add(asset);
            WriteGraph(json, asset.graph, asset.name, path);
            path.RemoveAt(path.Count - 1);
        }

        private static string DescribePath(List<BehaviorTreeGraphAsset> path, BehaviorTreeGraphAsset repeated)
        {
            return "already on this path: "
                   + string.Join(" -> ", path.Select(a => a == null ? "?" : a.name))
                   + " -> " + repeated.name;
        }

        /// <summary>
        /// Nodes the Entry can never reach. Value nodes legitimately live here because they are pulled
        /// through ports rather than parented, so each one reports whether anything actually reads it.
        /// </summary>
        private static void WriteOrphans(JsonWriter json, BehaviorTreeGraph graph, HashSet<BehaviorTreeNode> visited, List<BehaviorTreeGraphAsset> path)
        {
            // Invisible nodes are canvas plumbing, not authored structure: every transition owns a
            // PlaceHolderNode so the transition line can be selected. They have no ports and are never
            // defined, so they are both noise here and, before this filter, the reason a tree with any
            // editor-created transition dumped nothing at all.
            var orphans = graph.Nodes.Where(n => !visited.Contains(n) && n.IsVisible).ToList();
            if (orphans.Count == 0) return;

            json.PropertyName("unreachable");
            json.OpenArray();

            foreach (var orphan in orphans)
            {
                // a node that was deserialized but never defined has null port collections; that must not
                // take the whole report down with it, since a broken node is exactly what you are looking for
                bool feedsSomething = orphan.valueOutputs != null &&
                                      orphan.valueOutputs.Any(output => output.hasValidConnection);

                json.OpenObject();
                json.Property("name", orphan.NodeName);
                json.Property("type", orphan.GetType().Name);
                json.Property("guid", orphan.guid.ToString());
                json.Property("note", feedsSomething
                    ? "value node, read through a port"
                    : "nothing reaches or reads this node");

                // a dangling subgraph is what you are usually hunting here, so show its wiring too
                WriteInputs(json, orphan);
                WriteScriptGraphs(json, orphan);
                WriteSubTree(json, orphan, path);

                json.CloseObject();
            }

            json.CloseArray();
        }

        /// <summary>
        /// Recurses into any Visual Scripting graphs the node owns. Without this a node whose entire
        /// behaviour is authored in Visual Scripting dumps as an empty box, which is exactly the node type
        /// the package encourages non-programmers to build.
        /// </summary>
        private static void WriteScriptGraphs(JsonWriter json, BehaviorTreeNode node)
        {
            // GraphElement returns null rather than an empty sequence when a node owns no graphs
            var assets = node.scriptGraphAssets;
            if (assets == null) return;

            var owned = assets.Where(asset => asset != null).ToList();
            if (owned.Count == 0) return;

            json.PropertyName("scriptGraphs");
            json.OpenArray();

            foreach (var asset in owned)
            {
                FlowGraphDump.WriteGraph(json, asset.graph, asset.name);
            }

            json.CloseArray();
        }

        private static string Describe(object value)
        {
            switch (value)
            {
                case null: return "null";
                case string text: return "\"" + text + "\"";
                case float number: return number.ToString("0.###", CultureInfo.InvariantCulture);
                case Vector3 vector: return string.Format(CultureInfo.InvariantCulture, "({0:0.##}, {1:0.##}, {2:0.##})", vector.x, vector.y, vector.z);
                case Object unityObject: return unityObject == null ? "null" : unityObject.name;
                default: return value.ToString();
            }
        }

    }
}
