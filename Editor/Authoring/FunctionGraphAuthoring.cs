using System;
using System.Collections.Generic;
using System.Linq;
using ArcaneOnyx.VisualScriptingExtension;
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
    /// tree authoring API already is. The <c>fn_</c> commands over them are in <c>FunctionGraphCommands</c>,
    /// in the assembly that needs <c>com.unity.pipeline</c>; this class does not.
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

        /// <summary>
        /// Renames a declared output, carrying the wires that fed it across to the new name.
        ///
        /// <para>
        /// This exists for one repair: a Function whose author named the output something descriptive —
        /// <c>SelectedPosition</c> — not knowing that callers read the output named
        /// <see cref="FunctionGraphAsset.ResultKey"/> specifically. Such a Function evaluates fine and is
        /// offered nowhere, and until this helper the only fix was editing the definition and re-wiring by
        /// hand in the graph window.
        /// </para>
        ///
        /// <para>
        /// The wires are detached <em>before</em> the definition changes and reconnected after
        /// <c>PortDefinitionsChanged()</c> rebuilds the graph-output unit's ports — a connection into a port
        /// that no longer exists is exactly the stale-port state a definition edit leaves behind otherwise.
        /// </para>
        /// </summary>
        public static void RenameOutput(FunctionGraphAsset function, string fromKey, string toKey)
        {
            if (function == null || function.graph == null)
                throw new ArgumentException("A Function with a graph is required.");
            if (string.IsNullOrWhiteSpace(fromKey) || string.IsNullOrWhiteSpace(toKey))
                throw new ArgumentException("Both the current and the new output name are required.");
            if (fromKey == toKey) return;

            var graph = function.graph;
            var definition = graph.valueOutputDefinitions.FirstOrDefault(candidate => candidate.key == fromKey);

            if (definition == null)
                throw new ArgumentException($"'{function.name}' declares no output named '{fromKey}'.");
            if (graph.valueOutputDefinitions.Any(candidate => candidate.key == toKey))
                throw new ArgumentException($"'{function.name}' already declares an output named '{toKey}'.");

            var rewire = new List<(Unity.VisualScripting.ValueOutput source, ScriptGraphOutput unit)>();

            foreach (var connection in graph.valueConnections
                         .Where(c => c.destination.unit is ScriptGraphOutput && c.destination.key == fromKey)
                         .ToList())
            {
                rewire.Add((connection.source, (ScriptGraphOutput)connection.destination.unit));
                graph.valueConnections.Remove(connection);
            }

            definition.key = toKey;
            definition.label = toKey;
            graph.PortDefinitionsChanged();

            foreach (var (source, unit) in rewire)
            {
                source.ValidlyConnectTo(unit.valueInputs[toKey]);
            }

            EditorUtility.SetDirty(function);
            AssetDatabase.SaveAssets();

            // The contract just changed, so cached bindings and every canvas badge are stale.
            FunctionEvaluator.Invalidate(function);
            NodeProblemCache.Invalidate();
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

        public static FunctionGraphAsset ResolveFunction(string path, out string normalized)
        {
            normalized = NormalizePath(path);
            var function = AssetDatabase.LoadAssetAtPath<FunctionGraphAsset>(normalized);

            if (function == null)
            {
                throw new ArgumentException($"No Function asset at '{normalized}'.");
            }

            return function;
        }

        /// <summary>A Function's full contract as one plain object, the shape the <c>fn_</c> commands return.</summary>
        public static object DescribeContract(FunctionGraphAsset function)
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
    }
}
