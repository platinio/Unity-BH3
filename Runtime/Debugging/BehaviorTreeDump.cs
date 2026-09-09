using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using ArcaneOnyx.VisualScriptingExtension;
using Unity.VisualScripting.FullSerializer;
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

            return DescribeGraph(asset.graph, asset.name, path).Pretty();
        }

        public static string ToJson(BehaviorTreeGraph graph, string assetName = null)
        {
            return DescribeGraph(graph, assetName, new List<BehaviorTreeGraphAsset>()).Pretty();
        }

        private static fsData DescribeGraph(BehaviorTreeGraph graph, string assetName, List<BehaviorTreeGraphAsset> path)
        {
            var json = FsJson.Object();

            if (!string.IsNullOrEmpty(assetName)) json.Set("asset", assetName);

            if (graph == null) return json.Set("root", "(graph is null)");

            json.Set("nodeCount", graph.Nodes.Count());

            // node identity is per graph, so each nested tree gets its own set
            var visited = new HashSet<BehaviorTreeNode>();

            json.Set("root", DescribeNode(graph, graph.EntryNode, visited, path));

            AddOrphans(json, graph, visited, path);

            return json;
        }

        private static fsData DescribeNode(BehaviorTreeGraph graph, BehaviorTreeNode node, HashSet<BehaviorTreeNode> visited, List<BehaviorTreeGraphAsset> path)
        {
            if (node == null) return fsData.Null;

            var json = FsJson.Object();
            json.Set("name", node.NodeName);
            json.Set("type", node.GetType().Name);
            json.Set("guid", node.guid.ToString());
            json.Set("pos", string.Format(CultureInfo.InvariantCulture, "{0:0},{1:0}", node.Position.x, node.Position.y));

            // a cycle would otherwise recurse forever; report it instead of hanging
            if (!visited.Add(node)) return json.Set("repeat", "already shown above (cycle)");

            // One malformed node used to abort the entire asset, which left nothing to diagnose it with —
            // and a malformed node is precisely what a dump is being read to find. Report it in place and
            // keep going, so the rest of the tree still comes out.
            try
            {
                AddGuards(json, graph, node, visited);
                AddInputs(json, node);
                AddScriptGraphs(json, node);
                AddSubTree(json, node, path);
            }
            catch (System.Exception exception)
            {
                json.Set("error", exception.GetType().Name + ": " + exception.Message);
            }

            AddChildren(json, graph, node, visited, path);

            return json;
        }

        /// <summary>Conditional Executions are attached to an owner rather than parented, so they are listed apart.</summary>
        private static void AddGuards(fsData json, BehaviorTreeGraph graph, BehaviorTreeNode node, HashSet<BehaviorTreeNode> visited)
        {
            var guards = graph.Nodes
                .OfType<ConditionalExecution>()
                .Where(c => c.Owner == node)
                .ToList();

            if (guards.Count == 0) return;

            var described = FsJson.List();

            foreach (var guard in guards)
            {
                visited.Add(guard);

                var entry = FsJson.Object();
                entry.Set("name", guard.NodeName);
                entry.Set("type", guard.GetType().Name);
                entry.Set("guid", guard.guid.ToString());

                // What a guard is allowed to do is not readable from its type name alone -- a ReactiveGuard
                // with StopsItsOwnBranch off is a very different thing from one with it on, and telling them apart
                // is most of what a reader wants from a dump of a reactive tree.
                entry.Set("stopsItsOwnBranch", guard.StopsItsOwnBranch);
                entry.Set("takesOverLowerPriority", guard.TakesOverLowerPriority);

                AddTriggers(entry, guard);

                AddInputs(entry, guard);
                described.Add(entry);
            }

            json.Set("guards", described);
        }

        /// <summary>
        /// When a reactive guard is allowed to recompute.
        /// <para>
        /// An empty list is written rather than omitted, because "no triggers" is not the absence of
        /// information — it means the guard re-checks every tick, which is the single most expensive thing a
        /// guard can do and the thing a reader most needs to see.
        /// </para>
        /// </summary>
        private static void AddTriggers(fsData json, ConditionalExecution guard)
        {
            // A doorman has no schedule at all, which is different from a watchman that re-checks every
            // tick -- so it gets no "triggers" key rather than an empty one.
            if (!guard.HasRecomputeSchedule) return;

            var described = FsJson.List();

            var triggers = guard.Triggers;

            if (triggers != null)
            {
                foreach (var trigger in triggers)
                {
                    if (trigger == null) continue;

                    var entry = FsJson.Object();
                    entry.Set("type", trigger.Kind.ToString());
                    entry.Set("when", trigger.Describe());

                    // The keys are what decides whether this guard ever wakes, so they are worth reading
                    // in a diff even though nothing derives them yet.
                    if (trigger.Kind == GuardTriggerKind.OnKeyChanged)
                    {
                        var keys = FsJson.List();

                        if (trigger.Keys != null)
                        {
                            foreach (var key in trigger.Keys)
                            {
                                keys.Add(key);
                            }
                        }

                        entry.Set("keys", keys);
                    }

                    described.Add(entry);
                }
            }

            json.Set("triggers", described);
        }

        private static void AddInputs(fsData json, BehaviorTreeNode node)
        {
            if (node.valueInputs == null)
            {
                json.Set("ports", "(node was never defined)");
                return;
            }

            var inputs = node.valueInputs.ToList();
            if (inputs.Count == 0) return;

            var described = FsJson.Object();

            foreach (var input in inputs)
            {
                var connection = input.connection;

                if (connection != null)
                {
                    var source = connection.source;

                    // ports only know their owner as IBehaviorTreeNode, which carries no display name
                    string sourceName = source.behaviorTreeNode is BehaviorTreeNode sourceNode ? sourceNode.NodeName : "?";

                    described.Set(input.key, "<- " + sourceName + "." + source.key);
                    continue;
                }

                described.Set(input.key,
                    node.defaultValues.TryGetValue(input.key, out var value) ? Describe(value) : "(unset)");
            }

            json.Set("inputs", described);
        }

        private static void AddChildren(fsData json, BehaviorTreeGraph graph, BehaviorTreeNode node, HashSet<BehaviorTreeNode> visited, List<BehaviorTreeGraphAsset> path)
        {
            var children = graph.ChildrenInPriorityOrder(node);

            if (children.Count == 0) return;

            var described = FsJson.List();

            foreach (var child in children)
            {
                described.Add(DescribeNode(graph, child, visited, path));
            }

            json.Set("children", described);
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
        private static void AddSubTree(fsData json, BehaviorTreeNode node, List<BehaviorTreeGraphAsset> path)
        {
            if (!(node is RunBehaviorTreeGraphNode runNode)) return;

            var asset = runNode.BehaviorTreeGraphAsset;

            if (asset == null)
            {
                json.Set("subTree", FsJson.Object().Set("asset", "(none assigned)"));
                return;
            }

            if (path.Contains(asset))
            {
                json.Set("subTree", FsJson.Object()
                    .Set("asset", asset.name)
                    .Set("recursion", DescribePath(path, asset)));
                return;
            }

            if (path.Count >= MaxSubTreeDepth)
            {
                json.Set("subTree", FsJson.Object()
                    .Set("asset", asset.name)
                    .Set("truncated", "nesting deeper than " + MaxSubTreeDepth));
                return;
            }

            path.Add(asset);
            json.Set("subTree", DescribeGraph(asset.graph, asset.name, path));
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
        private static void AddOrphans(fsData json, BehaviorTreeGraph graph, HashSet<BehaviorTreeNode> visited, List<BehaviorTreeGraphAsset> path)
        {
            // Invisible nodes are canvas plumbing, not authored structure: every transition owns a
            // PlaceHolderNode so the transition line can be selected. They have no ports and are never
            // defined, so they are both noise here and, before this filter, the reason a tree with any
            // editor-created transition dumped nothing at all.
            var orphans = graph.Nodes.Where(n => !visited.Contains(n) && n.IsVisible).ToList();
            if (orphans.Count == 0) return;

            var described = FsJson.List();

            foreach (var orphan in orphans)
            {
                // a node that was deserialized but never defined has null port collections; that must not
                // take the whole report down with it, since a broken node is exactly what you are looking for
                bool feedsSomething = orphan.valueOutputs != null &&
                                      orphan.valueOutputs.Any(output => output.hasValidConnection);

                var entry = FsJson.Object();
                entry.Set("name", orphan.NodeName);
                entry.Set("type", orphan.GetType().Name);
                entry.Set("guid", orphan.guid.ToString());
                entry.Set("note", feedsSomething
                    ? "value node, read through a port"
                    : "nothing reaches or reads this node");

                // a dangling subgraph is what you are usually hunting here, so show its wiring too
                AddInputs(entry, orphan);
                AddScriptGraphs(entry, orphan);
                AddSubTree(entry, orphan, path);

                described.Add(entry);
            }

            json.Set("unreachable", described);
        }

        /// <summary>
        /// Recurses into any Visual Scripting graphs the node owns. Without this a node whose entire
        /// behaviour is authored in Visual Scripting dumps as an empty box, which is exactly the node type
        /// the package encourages non-programmers to build.
        /// </summary>
        private static void AddScriptGraphs(fsData json, BehaviorTreeNode node)
        {
            if (node is not BaseVisualScriptingNode holder) return;

            var owned = holder.Functions.ToList();
            if (owned.Count == 0) return;

            var described = FsJson.List();

            foreach (var asset in owned)
            {
                described.Add(FlowGraphDump.Describe(asset.graph, asset.name));
            }

            json.Set("scriptGraphs", described);
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
