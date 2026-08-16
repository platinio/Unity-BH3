using System;
using System.Collections.Generic;
using System.Linq;
using ArcaneOnyx.VisualScriptingExtension;
using Unity.Pipeline.Commands;
using UnityEditor;
using UnityEngine;

namespace ArcaneOnyx.BehaviorTree.Authoring
{
    /// <summary>
    /// Authoring and inspection for Functions, following the same "ask the project" philosophy as the
    /// <c>bt_</c> and <c>tps_</c> families: the catalogue is derived from the assets themselves, so it cannot
    /// fall behind the way a hand-written table does.
    /// <para>
    /// These live in BH3's editor assembly rather than VisualScriptingExtension's because that is where the
    /// pipeline command surface already is — and because VSE's editor assembly is still misnamed
    /// <c>NewAssembly</c>, which is worth fixing on its own rather than as a side effect of this feature.
    /// </para>
    /// </summary>
    public static class FunctionGraphAuthoring
    {
        // ------------------------------------------------------------------ C# API

        /// <summary>Creates a Function asset with a runnable default graph.</summary>
        public static FunctionGraphAsset CreateFunction(string path)
        {
            if (string.IsNullOrWhiteSpace(path)) throw new ArgumentException("A Function needs an asset path.");

            var normalized = NormalizePath(path);
            var function = ScriptableObject.CreateInstance<FunctionGraphAsset>();
            function.graph = function.DefaultGraph();

            AssetDatabase.CreateAsset(function, normalized);
            EditorUtility.SetDirty(function);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            return function;
        }

        /// <summary>Every Function in the project, optionally under one folder.</summary>
        public static List<FunctionGraphAsset> FindFunctions(string folder = null)
        {
            var searchFolders = string.IsNullOrWhiteSpace(folder)
                ? null
                : new[] { folder.TrimEnd('/') };

            var guids = searchFolders == null
                ? AssetDatabase.FindAssets($"t:{nameof(FunctionGraphAsset)}")
                : AssetDatabase.FindAssets($"t:{nameof(FunctionGraphAsset)}", searchFolders);

            var functions = new List<FunctionGraphAsset>();
            foreach (var guid in guids)
            {
                var assetPath = AssetDatabase.GUIDToAssetPath(guid);
                var function = AssetDatabase.LoadAssetAtPath<FunctionGraphAsset>(assetPath);
                if (function != null) functions.Add(function);
            }

            return functions;
        }

        /// <summary>
        /// How a Function is classified for a library panel. Derived from the declared result type rather than
        /// from a subclass, so adding a flavor never means adding an asset type.
        /// </summary>
        public static string DescribeFlavor(FunctionGraphAsset function)
        {
            var resultType = function?.ResultType;

            if (resultType == null) return "no result";
            if (resultType == typeof(bool)) return "predicate";
            if (resultType.Name.Contains("Query")) return "query";

            return $"value ({resultType.Name})";
        }

        private static string NormalizePath(string path)
        {
            var normalized = path.Replace('\\', '/');
            if (!normalized.EndsWith(".asset", StringComparison.OrdinalIgnoreCase)) normalized += ".asset";
            return normalized;
        }

        /// <summary>
        /// Loads a Function by asset path, normalising the path and failing by name when there is none.
        /// Exposed for authoring code outside this class that takes a Function path — a guard's condition,
        /// for instance — so path handling stays in one place rather than being re-guessed per command.
        /// </summary>
        public static FunctionGraphAsset ResolveFunction(string path) => ResolveFunction(path, out _);

        private static FunctionGraphAsset ResolveFunction(string path, out string normalized)
        {
            normalized = NormalizePath(path);
            var function = AssetDatabase.LoadAssetAtPath<FunctionGraphAsset>(normalized);

            if (function == null)
            {
                throw new ArgumentException($"No Function asset at '{normalized}'.");
            }

            return function;
        }

        private static object DescribeContract(FunctionGraphAsset function)
        {
            var plan = FunctionBindingPlan.Resolve(function);

            return new
            {
                function = AssetDatabase.GetAssetPath(function),
                name = function.name,
                flavor = DescribeFlavor(function),
                result = function.ResultType?.Name ?? "(none)",
                pure = function.Pure,
                watchedKeys = function.WatchedKeys.ToArray(),
                description = function.Description ?? "",
                inputs = function.Inputs
                    .Select(i => new
                    {
                        key = i.key,
                        type = i.type?.Name ?? "?",
                        optional = i.hasDefaultValue,
                        defaultValue = i.hasDefaultValue ? $"{i.defaultValue}" : "(required)"
                    })
                    .ToArray(),
                outputs = function.Outputs
                    .Select(o => new { key = o.key, type = o.type?.Name ?? "?" })
                    .ToArray(),
                units = function.graph?.units.Count ?? 0,
                graphHash = function.ComputeGraphHash(),
                usable = plan.IsUsable,
                error = plan.Error
            };
        }

        // ------------------------------------------------------------------ CLI

        [CliCommand("fn_create",
            "Create a Function — a named, contracted, reusable Visual Scripting graph. The new asset ships " +
            "with Enter wired to Exit so it runs before you have declared anything.")]
        public static object CreateFunctionCommand(
            [CliArg("path", "Asset path for the new Function, e.g. Assets/AI/Functions/HasTarget.asset.", Required = true)] string path)
        {
            var function = CreateFunction(path);
            return DescribeContract(function);
        }

        [CliCommand("fn_list",
            "Every Function in the project with its flavor, contract size and purity. Flavor comes from the " +
            "declared result type, so the sections are derived rather than configured.")]
        public static object ListFunctionsCommand(
            [CliArg("folder", "Restrict to one folder, e.g. Assets/AI/Functions.")] string folder = null)
        {
            var functions = FindFunctions(folder);

            return new
            {
                count = functions.Count,
                functions = functions
                    .Select(f => new
                    {
                        path = AssetDatabase.GetAssetPath(f),
                        name = f.name,
                        flavor = DescribeFlavor(f),
                        inputs = f.Inputs.Count(),
                        pure = f.Pure,
                        watchedKeys = f.WatchedKeys.Count,
                        usable = FunctionBindingPlan.Resolve(f).IsUsable
                    })
                    .OrderBy(f => f.path)
                    .ToArray()
            };
        }

        [CliCommand("fn_describe",
            "A Function's full contract: declared inputs with types and optionality, outputs, purity, " +
            "watched keys, and whether it can actually be evaluated.")]
        public static object DescribeFunctionCommand(
            [CliArg("function", "Asset path of the Function.", Required = true)] string function)
        {
            var asset = ResolveFunction(function, out _);
            return DescribeContract(asset);
        }

        /// <summary>
        /// Promotes a node's embedded one-off graph into a shared project asset and re-points the node at it.
        /// <para>
        /// This is the bridge the whole design rests on: embedding stays legal for a three-unit read, and the
        /// moment one is worth sharing it becomes a Function without being rebuilt by hand. The graph is
        /// cloned rather than moved, because the embedded sub-asset is owned structurally by the tree and
        /// deleting it here would make this the second thing in the project that deletes graphs — the one
        /// situation the repository-removal sequence exists to avoid. The now-unreferenced sub-asset is left
        /// for <c>bt_verify</c> to report as an orphan.
        /// </para>
        /// </summary>
        public static FunctionGraphAsset ExtractToProjectAsset(
            BehaviorTreeGraphAsset tree,
            VisualScriptGraphVariable node,
            string path)
        {
            if (tree == null) throw new ArgumentException("No tree given.");
            if (node == null) throw new ArgumentException("No node given.");

            var embedded = node.EmbeddedScriptGraph;
            if (embedded == null)
            {
                throw new ArgumentException(
                    node.Function != null
                        ? $"Node '{node.NodeName}' already reads the Function '{node.Function.name}'."
                        : $"Node '{node.NodeName}' has no embedded graph to extract.");
            }

            var normalized = NormalizePath(path);
            if (AssetDatabase.LoadAssetAtPath<UnityEngine.Object>(normalized) != null)
            {
                throw new ArgumentException($"'{normalized}' already exists. Pick a path that is free.");
            }

            var function = ScriptableObject.CreateInstance<FunctionGraphAsset>();
            // Called as a plain static rather than as an extension: this file deliberately does not import
            // Unity.VisualScripting, because that namespace collides with several ArcaneOnyx.BehaviorTree
            // type names and importing it here would make the collisions resolve silently the wrong way.
            function.graph = Unity.VisualScripting.Cloning.CloneViaFakeSerialization(embedded.graph);

            AssetDatabase.CreateAsset(function, normalized);

            // The Function wins over an embedded graph at runtime, so leaving both assigned would leave an
            // editable-but-dead graph on the node — which bt_verify reports, and which nobody wants to see
            // reported by the command that supposedly did the migration.
            node.SetScriptGraph(null);
            node.SetFunction(function);

            // No seeding call here on purpose. The SaveAssets below runs GuardScheduleSeeder, which seeds
            // every guard whose condition now declares something -- one hook covering every path a Function
            // can arrive by, rather than a call each assignment site has to remember. A freshly extracted
            // Function declares nothing yet anyway, since extraction copies the graph and a graph carries no
            // asset-level metadata, so there is usually nothing to inherit until an author declares it.
            EditorUtility.SetDirty(function);
            EditorUtility.SetDirty(tree);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            FunctionEvaluator.Invalidate(function);

            return function;
        }

        private static BehaviorTreeGraphAsset ResolveTreeAsset(string path, out string normalized)
        {
            normalized = NormalizePath(path);
            var tree = AssetDatabase.LoadAssetAtPath<BehaviorTreeGraphAsset>(normalized);

            if (tree == null) throw new ArgumentException($"No behavior tree asset at '{normalized}'.");

            return tree;
        }

        [CliCommand("fn_extract",
            "Promote a node's embedded one-off graph into a shared Function asset and re-point the node at " +
            "it. The graph is copied, not moved: the tree still owns the original sub-asset, which bt_verify " +
            "then reports as an orphan rather than this command deleting it.")]
        public static object ExtractFunctionCommand(
            [CliArg("tree", "Asset path of the behavior tree holding the node.", Required = true)] string tree,
            [CliArg("node", "Guid of the Script Graph Variable node. Run bt_describe_tree to list them.", Required = true)] string node,
            [CliArg("path", "Asset path for the new Function, e.g. Assets/AI/Functions/HasTarget.asset.", Required = true)] string path)
        {
            var treeAsset = ResolveTreeAsset(tree, out var normalizedTree);

            if (!Guid.TryParse(node, out var parsed))
            {
                throw new ArgumentException($"'{node}' is not a guid. Run bt_describe_tree to list them.");
            }

            var target = treeAsset.graph.Nodes.FirstOrDefault(n => n.guid == parsed);
            if (target == null) throw new ArgumentException($"The tree has no node with guid '{node}'.");

            if (target is not VisualScriptGraphVariable variableNode)
            {
                throw new ArgumentException(
                    $"Node '{node}' is a {target.GetType().Name}, which holds no embedded graph to extract.");
            }

            var extractedFrom = variableNode.EmbeddedScriptGraph?.name;
            var function = ExtractToProjectAsset(treeAsset, variableNode, path);

            return new
            {
                tree = normalizedTree,
                node = node,
                extractedFrom = extractedFrom,
                function = AssetDatabase.GetAssetPath(function),
                contract = DescribeContract(function),
                note = "The original sub-asset is still stored in the tree and is now unreferenced; " +
                       "bt_verify reports it as an orphan."
            };
        }

        [CliCommand("fn_set_metadata",
            "Set the asset-level metadata a graph cannot express: purity, watched keys and description. " +
            "Watched keys are what a reactive guard whose condition is this Function inherits.")]
        public static object SetFunctionMetadataCommand(
            [CliArg("function", "Asset path of the Function.", Required = true)] string function,
            [CliArg("pure", "Whether this Function only reads. Default true.")] string pure = null,
            [CliArg("watched_keys", "Comma-separated agent variable keys this Function reads.")] string watchedKeys = null,
            [CliArg("description", "What this Function answers.")] string description = null)
        {
            var asset = ResolveFunction(function, out _);

            if (!string.IsNullOrWhiteSpace(pure))
            {
                if (!bool.TryParse(pure, out var isPure))
                {
                    throw new ArgumentException($"Could not read '{pure}' as true or false.");
                }

                asset.SetPure(isPure);
            }

            if (description != null) asset.SetDescription(description);

            if (watchedKeys != null)
            {
                asset.SetWatchedKeys(watchedKeys.Split(','));
            }

            EditorUtility.SetDirty(asset);
            AssetDatabase.SaveAssets();

            FunctionEvaluator.Invalidate(asset);

            return DescribeContract(asset);
        }

        [CliCommand("fn_refresh_ports",
            "Rebuild the input ports of every Script Graph Variable node in a tree from the contract of the " +
            "Function it reads. Reports the drift it repaired and any connection the repair cost.")]
        public static object RefreshFunctionPortsCommand(
            [CliArg("tree", "Asset path of the behavior tree.", Required = true)] string tree,
            [CliArg("node", "Guid of a single node. Omit to refresh every Function-backed node in the tree.")]
            string node = null)
        {
            var asset = BehaviorTreeAuthoring.LoadTree(tree);
            if (asset == null) throw new ArgumentException($"No behavior tree at '{tree}'.");

            var refreshed = new List<object>();

            foreach (var candidate in asset.graph.Nodes)
            {
                if (candidate is not VisualScriptGraphVariable variableNode) continue;
                if (node != null && variableNode.guid.ToString() != node) continue;
                if (variableNode.Function == null) continue;

                // Drift is read before the rebuild, because afterwards there is nothing left to differ --
                // the same order bt_refresh_sub_tree_ports uses, for the same reason.
                var drift = variableNode.DescribeContractDrift();
                var dropped = variableNode.RefreshParameters();

                // The contract just changed, so this is exactly when the node is allowed to be resized.
                var size = ContractPortLayout.ResizeToFitPorts(variableNode);

                refreshed.Add(new
                {
                    node = variableNode.guid.ToString(),
                    name = variableNode.NodeName,
                    function = variableNode.Function.name,
                    drift = drift.Count == 0 ? new List<string> { "none" } : drift,
                    droppedConnections = dropped,
                    ports = variableNode.Parameters.Select(parameter => parameter.ToString()).ToList(),
                    size = $"{size.width:0} x {size.height:0}"
                });
            }

            if (refreshed.Count == 0)
            {
                return new
                {
                    tree,
                    refreshed = 0,
                    note = node != null
                        ? $"No Function-backed Script Graph Variable node with guid '{node}' in this tree."
                        : "No Script Graph Variable node in this tree reads a Function."
                };
            }

            EditorUtility.SetDirty(asset);
            AssetDatabase.SaveAssets();

            return new { tree, refreshed = refreshed.Count, nodes = refreshed };
        }
    }
}
