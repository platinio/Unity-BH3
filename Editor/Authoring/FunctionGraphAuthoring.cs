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
        public static FunctionGraphAsset CreateFunction(string path, Type resultType = null)
        {
            if (string.IsNullOrWhiteSpace(path)) throw new ArgumentException("A Function needs an asset path.");

            var normalized = NormalizePath(path);
            var function = ScriptableObject.CreateInstance<FunctionGraphAsset>();

            // DefaultGraph() gives Enter wired to Exit and no result, because it cannot know what the
            // Function is for. A caller that does know says so here rather than building the graph itself:
            // this is the one place that decides what a new Function contains, and it had grown a second
            // copy in the inspector's picker before this parameter existed.
            function.graph = function.DefaultGraph();

            if (resultType != null)
            {
                function.graph.valueOutputDefinitions.Add(new Unity.VisualScripting.ValueOutputDefinition
                {
                    key = FunctionGraphAsset.ResultKey,
                    label = FunctionGraphAsset.ResultKey,
                    type = resultType
                });

                function.graph.PortDefinitionsChanged();
            }

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
            [CliArg("path", "Asset path for the new Function, e.g. Assets/AI/Functions/HasTarget.asset.", Required = true)] string path,
            [CliArg("result", "Type name of the Result this Function returns, e.g. System.Boolean or " +
                              "UnityEngine.Vector3. Omit for a Function that declares no result yet.")] string result = null)
        {
            Type resultType = null;

            if (!string.IsNullOrWhiteSpace(result))
            {
                resultType = Unity.VisualScripting.RuntimeCodebase.TryDeserializeType(result, out var deserialized)
                    ? deserialized
                    : Type.GetType(result);

                if (resultType == null)
                {
                    throw new ArgumentException(
                        $"'{result}' is not a type this project can resolve. Use an assembly-qualified or " +
                        "full type name, e.g. System.Boolean or UnityEngine.Vector3.");
                }
            }

            var function = CreateFunction(path, resultType);
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
