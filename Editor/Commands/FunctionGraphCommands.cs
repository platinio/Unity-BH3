using System;
using System.Collections.Generic;
using System.Linq;
using ArcaneOnyx.VisualScriptingExtension;
using Unity.Pipeline.Commands;
using UnityEditor;
using UnityEngine;
using static ArcaneOnyx.BehaviorTree.Authoring.FunctionGraphAuthoring;

namespace ArcaneOnyx.BehaviorTree.Authoring
{
    /// <summary>
    /// The <c>fn_</c> command surface over <see cref="FunctionGraphAuthoring"/>. Compiles only when
    /// <c>com.unity.pipeline</c> is installed; the authoring API it wraps does not need the package.
    /// </summary>
    public static class FunctionGraphCommands
    {

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

        [CliCommand("fn_rename_output",
            "Rename a declared output of a Function, carrying the wires that fed it to the new name. The " +
            "main use is repairing a Function whose result is not named 'Result' — it evaluates fine but " +
            "callers read nothing and no picker offers it.")]
        public static object RenameOutputCommand(
            [CliArg("function", "Asset path of the Function.", Required = true)] string function,
            [CliArg("from", "Current name of the output.", Required = true)] string from,
            [CliArg("to", "New name. Omit for 'Result', the name callers read.")] string to = null)
        {
            var asset = ResolveFunction(function, out _);
            RenameOutput(asset, from, string.IsNullOrWhiteSpace(to) ? FunctionGraphAsset.ResultKey : to);

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
