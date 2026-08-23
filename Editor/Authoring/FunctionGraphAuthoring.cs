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

        /// <summary>
        /// Promotes a node's embedded one-off graph into a shared project asset and re-points the node at it.
        /// <para>
        /// This is the bridge the whole design rests on: embedding stays legal for a three-unit read, and the
        /// moment one is worth sharing it becomes a Function without being rebuilt by hand. The graph is
        /// cloned rather than moved, because the embedded sub-asset is owned structurally by the tree and
        /// deleting it here would make this the second thing in the project that deletes graphs — the one
        /// situation the repository-removal sequence exists to avoid. Clearing the node's reference is all
        /// this does; the <c>SaveAssets</c> below then runs <c>OrphanedScriptGraphCleanup</c>, which removes
        /// the sub-asset nothing references any more. So extraction leaves no leftover, and still is not a
        /// deleter.
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

        /// <summary>
        /// Where a migrated one-off lands: <c>&lt;TreeFolder&gt;/Functions/&lt;Tree&gt;.&lt;Graph&gt;.asset</c>.
        ///
        /// <para>
        /// The convention had to be invented here — step 2c deferred it to the migration that needed it. Two
        /// things decided the shape. It sits <b>beside the tree</b> rather than in one project-wide folder,
        /// because a central folder is what the deleted repository was, in spirit: a place every tree writes
        /// to and nobody owns. And it is <b>named for where it came from</b>, because after this runs a
        /// designer meets a folder of small assets with no other clue about which tree once contained them.
        /// </para>
        ///
        /// <para>
        /// <b>Three sources for the name, in the order that yields a name worth reading.</b> The graph's own
        /// name comes first, because <c>CreateVariableReadGraph</c> named it after the variable it reads
        /// (<c>hpRead</c>). Graphs authored by hand in the canvas have <em>no</em> name at all, but their
        /// node usually does — BH3's own FPS sample is full of <c>Is Enemy On Range?</c> and
        /// <c>Safe Position Query</c>, which are the best names available anywhere. Only when both are
        /// missing is the variable the graph reads used to build one.
        /// </para>
        ///
        /// <para>
        /// The order matters more than it looks: getting it wrong turns a sample's eight meaningful graphs
        /// into <c>Soldier.Graph3</c> through <c>Soldier.Graph8</c>, which is a migration a designer cannot
        /// undo by reading it.
        /// </para>
        /// </summary>
        public static string MigrationPathFor(BehaviorTreeGraphAsset tree, VisualScriptGraphVariable node)
        {
            var treePath = AssetDatabase.GetAssetPath(tree);
            var folder = System.IO.Path.GetDirectoryName(treePath)?.Replace('\\', '/') ?? "Assets";
            var treeName = System.IO.Path.GetFileNameWithoutExtension(treePath);

            return $"{folder}/Functions/{treeName}.{Sanitize(NameFor(node))}.asset";
        }

        /// <summary>
        /// Where a migrated lifecycle graph lands. Same convention as
        /// <see cref="MigrationPathFor(BehaviorTreeGraphAsset, VisualScriptGraphVariable)"/>, but the slot
        /// carries no name of its own, so the node's name plus the slot's position is all there is —
        /// <c>Soldier.Shoot.OnEnter.asset</c>.
        /// </summary>
        public static string MigrationPathFor(
            BehaviorTreeGraphAsset tree,
            BehaviorTreeNode node,
            ScriptGraphVariable slot,
            string slotLabel)
        {
            var treePath = AssetDatabase.GetAssetPath(tree);
            var folder = System.IO.Path.GetDirectoryName(treePath)?.Replace('\\', '/') ?? "Assets";
            var treeName = System.IO.Path.GetFileNameWithoutExtension(treePath);

            var embedded = slot.ScriptGraphAsset;
            var name = embedded != null && !string.IsNullOrWhiteSpace(embedded.name)
                ? embedded.name
                : $"{node.NodeName}.{slotLabel}";

            return $"{folder}/Functions/{treeName}.{Sanitize(name)}.asset";
        }

        /// <summary>
        /// Promotes the embedded graph in one slot into a standalone Function and re-points the slot.
        ///
        /// <para>
        /// The general form of <see cref="ExtractToProjectAsset"/>. That one takes a
        /// <c>VisualScriptGraphVariable</c> because it also has to rebuild the node's declared ports from
        /// the Function's contract; a lifecycle slot has no ports, so re-pointing the slot is the whole job.
        /// Both share the clone, which is the part that must not be written twice.
        /// </para>
        /// </summary>
        public static FunctionGraphAsset ExtractSlotToProjectAsset(
            BehaviorTreeGraphAsset tree,
            ScriptGraphVariable slot,
            string path)
        {
            if (tree == null) throw new ArgumentException("No tree given.");
            if (slot == null) throw new ArgumentException("No graph slot given.");

            var embedded = slot.ScriptGraphAsset;
            if (embedded == null) throw new ArgumentException("That slot holds no embedded graph to extract.");

            var normalized = NormalizePath(path);
            if (AssetDatabase.LoadAssetAtPath<UnityEngine.Object>(normalized) != null)
            {
                throw new ArgumentException($"'{normalized}' already exists. Pick a path that is free.");
            }

            var function = ScriptableObject.CreateInstance<FunctionGraphAsset>();
            function.graph = Unity.VisualScripting.Cloning.CloneViaFakeSerialization(embedded.graph);

            AssetDatabase.CreateAsset(function, normalized);

            slot.SetScriptGraphAsset(null);
            slot.SetFunction(function);

            EditorUtility.SetDirty(function);
            EditorUtility.SetDirty(tree);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            FunctionEvaluator.Invalidate(function);

            return function;
        }

        /// <summary>The best available name for what a node's embedded graph does. See MigrationPathFor.</summary>
        private static string NameFor(VisualScriptGraphVariable node)
        {
            var embedded = node.EmbeddedScriptGraph;

            if (embedded != null && !string.IsNullOrWhiteSpace(embedded.name)) return embedded.name;

            // NodeName falls back to the type's display name when the node carries no comment, and every
            // such node in a tree would share it -- so that default is exactly what must not be used.
            var nodeName = node.NodeName;
            if (!string.IsNullOrWhiteSpace(nodeName) && nodeName != DefaultScriptGraphVariableNodeName)
            {
                return nodeName;
            }

            if (embedded?.graph != null)
            {
                foreach (var unit in embedded.graph.units)
                {
                    if (unit is not Unity.VisualScripting.GetVariable read) continue;
                    if (!read.name.unit.defaultValues.TryGetValue("name", out var key)) continue;

                    var variable = key as string;
                    if (!string.IsNullOrWhiteSpace(variable)) return variable + "Read";
                }
            }

            return "Graph";
        }

        /// <summary>What <c>VisualScriptGraphVariable.NodeName</c> answers when the node has no comment.</summary>
        private const string DefaultScriptGraphVariableNodeName = "Script Graph Variable";

        /// <summary>
        /// A node name turned into a file name. Punctuation is <em>dropped</em> rather than substituted:
        /// "Is Enemy On Range?" reads as <c>IsEnemyOnRange</c>, where mapping to underscores would leave the
        /// trailing <c>IsEnemyOnRange_</c> on every question a designer ever named a node with. The dot goes
        /// too, since it is this convention's own separator.
        /// </summary>
        private static string Sanitize(string name)
        {
            var kept = new System.Text.StringBuilder(name.Length);
            var invalid = System.IO.Path.GetInvalidFileNameChars();

            foreach (var character in name)
            {
                if (char.IsWhiteSpace(character) || character == '.') continue;
                if (Array.IndexOf(invalid, character) >= 0) continue;

                kept.Append(character);
            }

            return kept.Length == 0 ? "Graph" : kept.ToString();
        }

        /// <summary>
        /// Promotes every embedded graph still held by the given trees into a standalone Function.
        ///
        /// <para>
        /// The one-shot half of retiring embedding. It is a loop over <see cref="ExtractToProjectAsset"/>
        /// rather than a second implementation of it, so a migrated node is indistinguishable from one a
        /// designer extracted by hand, and any fix to extraction reaches the migration for free.
        /// </para>
        ///
        /// <para>
        /// <b>Collisions are resolved rather than refused.</b> A tree may read the same variable from two
        /// nodes, and both sub-assets are then called <c>hpRead</c>. Failing the whole migration over a name
        /// clash in the middle of it would leave the project half-converted, which is the worst of the
        /// available outcomes; a numeric suffix is the least surprising alternative.
        /// </para>
        /// </summary>
        /// <param name="dryRun">
        /// Report what would happen and change nothing. Worth having for its own sake: this rewrites every
        /// tree it touches, and the paths it picks are a convention worth reading before it is applied to
        /// dozens of assets at once.
        /// </param>
        public static List<object> MigrateEmbeddedGraphs(IEnumerable<string> treePaths, bool dryRun)
        {
            var results = new List<object>();
            var taken = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            foreach (var treePath in treePaths)
            {
                var tree = AssetDatabase.LoadAssetAtPath<BehaviorTreeGraphAsset>(treePath);
                if (tree == null) continue;

                // Collected before extracting: extraction re-points the node it is given, and enumerating a
                // graph while its nodes are being rewritten is the kind of thing that works until it does not.
                var pending = tree.graph.Nodes
                    .OfType<VisualScriptGraphVariable>()
                    .Where(node => node.EmbeddedScriptGraph != null)
                    .ToList();

                // Every other node type that holds graphs, by slot. VisualScriptGraphVariable is the one the
                // design talks about, but it is not the only holder: VisualScriptingNode carries four
                // lifecycle graphs, and BH3's own FPS sample runs its whole Shoot/Aim/Do Damage behaviour
                // out of them. Migrating only the value reads leaves the field still in use and the seam
                // undeletable, which is how this was discovered.
                var pendingSlots = new List<(BehaviorTreeNode node, ScriptGraphVariable slot, string label)>();

                foreach (var node in tree.graph.Nodes)
                {
                    if (node is VisualScriptGraphVariable) continue;
                    if (node is not BaseVisualScriptingNode holder) continue;

                    var slots = holder.GraphSlots;
                    for (var index = 0; index < slots.Count; index++)
                    {
                        if (slots[index]?.ScriptGraphAsset == null) continue;
                        pendingSlots.Add((node, slots[index], SlotLabel(holder, index)));
                    }
                }

                foreach (var node in pending)
                {
                    Migrate(
                        treePath,
                        node.guid.ToString(),
                        node.EmbeddedScriptGraph.name,
                        MigrationPathFor(tree, node),
                        candidate => ExtractToProjectAsset(tree, node, candidate));
                }

                foreach (var (node, slot, label) in pendingSlots)
                {
                    Migrate(
                        treePath,
                        node.guid.ToString(),
                        $"{node.NodeName}.{label}",
                        MigrationPathFor(tree, node, slot, label),
                        candidate => ExtractSlotToProjectAsset(tree, slot, candidate));
                }
            }

            return results;

            // Local, because the two loops above differ only in how they reach the graph -- and the part
            // they share, deciding what the migrated Function declares, is the part that must not be
            // written twice and then drift.
            void Migrate(string treePath, string nodeGuid, string graphName, string wantedPath,
                Func<string, FunctionGraphAsset> extract)
            {
                var candidate = wantedPath;
                var suffix = 2;
                while (taken.Contains(candidate) || AssetDatabase.LoadAssetAtPath<UnityEngine.Object>(candidate) != null)
                {
                    candidate = wantedPath.Substring(0, wantedPath.Length - ".asset".Length) + suffix + ".asset";
                    suffix++;
                }

                taken.Add(candidate);

                if (dryRun)
                {
                    results.Add(new { tree = treePath, node = nodeGuid, graph = graphName, function = candidate });
                    return;
                }

                EnsureFolder(System.IO.Path.GetDirectoryName(candidate)?.Replace('\\', '/'));

                try
                {
                    var function = extract(candidate);

                    // Purity defaults to true, which is right for a Function somebody wrote deliberately and
                    // wrong for one that arrived by migration: an embedded graph was never asked to declare
                    // anything, and BH3's own FPS sample has several that write. Declaring them pure would
                    // manufacture a batch of verify warnings the migration itself caused, and teach the
                    // first reader that the warning is noise.
                    var writes = function.DeriveWrites();
                    if (writes.Count > 0) function.SetPure(false);

                    // Same reasoning for watched keys: a guard reading this Function inherits them, and an
                    // empty declaration on a migrated condition is the silent-stale-guard failure the
                    // watched-key work exists to prevent.
                    var readKeys = function.DeriveReadKeys();
                    if (readKeys.Count > 0) function.SetWatchedKeys(readKeys);

                    EditorUtility.SetDirty(function);
                    AssetDatabase.SaveAssets();

                    results.Add(new
                    {
                        tree = treePath,
                        node = nodeGuid,
                        function = AssetDatabase.GetAssetPath(function),
                        pure = function.Pure,
                        watchedKeys = readKeys
                    });
                }
                catch (Exception exception)
                {
                    // Reported rather than thrown: one bad node must not abandon the migration partway
                    // through and leave the project in a state nobody chose.
                    results.Add(new { tree = treePath, node = nodeGuid, error = exception.Message });
                }
            }
        }

        /// <summary>
        /// What to call a graph slot that has no name of its own. <c>VisualScriptingNode</c>'s four are its
        /// lifecycle hooks, in declaration order, and naming them after the hook is the only thing that
        /// makes <c>Soldier.Shoot.OnEnter</c> readable rather than <c>Soldier.Shoot.2</c>.
        /// </summary>
        private static string SlotLabel(BaseVisualScriptingNode node, int index)
        {
            if (node is VisualScriptingNode)
            {
                switch (index)
                {
                    case 0: return "OnAwake";
                    case 1: return "OnEnter";
                    case 2: return "OnUpdate";
                    case 3: return "OnExit";
                }
            }

            return index.ToString();
        }

        /// <summary>Creates a folder and any missing parent, since <c>CreateAsset</c> will not.</summary>
        private static void EnsureFolder(string folder)
        {
            if (string.IsNullOrEmpty(folder) || AssetDatabase.IsValidFolder(folder)) return;

            var parent = System.IO.Path.GetDirectoryName(folder)?.Replace('\\', '/');
            EnsureFolder(parent);

            AssetDatabase.CreateFolder(parent, System.IO.Path.GetFileName(folder));
        }

        /// <summary>
        /// Every behavior tree in the project, or under one folder — except test fixtures.
        ///
        /// <para>
        /// <b>A migration must not rewrite a test fixture.</b> Some of them exist precisely to hold an old
        /// serialization shape, so re-saving one destroys the thing it was kept for while leaving a green
        /// suite that no longer tests anything. BH3 has exactly such a fixture, and a guard test that
        /// notices — it is what caught this. Passing an explicit <paramref name="folder"/> under a test
        /// directory still reaches them, so this is a default rather than a prohibition.
        /// </para>
        /// </summary>
        public static List<string> FindTrees(string folder)
        {
            var explicitlyScoped = !string.IsNullOrWhiteSpace(folder);

            var search = explicitlyScoped
                ? AssetDatabase.FindAssets($"t:{nameof(BehaviorTreeGraphAsset)}", new[] { NormalizePath(folder) })
                : AssetDatabase.FindAssets($"t:{nameof(BehaviorTreeGraphAsset)}");

            return search
                .Select(AssetDatabase.GUIDToAssetPath)
                .Where(path => explicitlyScoped || !IsTestAsset(path))
                .Distinct()
                .OrderBy(path => path)
                .ToList();
        }

        private static bool IsTestAsset(string path)
        {
            return path.Contains("/Test/", StringComparison.OrdinalIgnoreCase)
                || path.Contains("/Tests/", StringComparison.OrdinalIgnoreCase);
        }

        [CliCommand("fn_migrate_embedded",
            "Promote every embedded one-off graph still held by a tree into a standalone Function, at " +
            "<TreeFolder>/Functions/<Tree>.<Graph>.asset. Pass --dry_run true first: this rewrites every " +
            "tree it touches. Omit --folder to migrate the whole project.")]
        public static object MigrateEmbeddedGraphsCommand(
            [CliArg("folder", "Limit to trees under this folder, e.g. Assets/ArcaneOnyx/BH3Demos.")] string folder,
            [CliArg("dry_run", "Report what would happen and change nothing.")] bool dryRun = false)
        {
            var trees = FindTrees(folder);
            var migrated = MigrateEmbeddedGraphs(trees, dryRun);

            return new
            {
                dryRun,
                treesScanned = trees.Count,
                graphs = migrated.Count,
                migrated
            };
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
            "it. The graph is copied, not moved, and this command never deletes one: it clears the node's " +
            "reference and saves, and the save-time cleanup removes the sub-asset nothing references any " +
            "more. Nothing is left behind for bt_verify to report.")]
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
                note = "The original sub-asset was left unreferenced and removed by the save this command " +
                       "performed. Extraction does not delete graphs; the save-time cleanup does."
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
