using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using ArcaneOnyx.VisualScriptingExtension;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Unity.Pipeline.Commands;
using Unity.Pipeline.Editor.Authoring;
using Unity.Pipeline.Models;
using Unity.VisualScripting;
using UnityEditor;
using UnityEngine;
using static ArcaneOnyx.BehaviorTree.Authoring.BehaviorTreeAuthoring;

// StickyNote exists in both ArcaneOnyx.GraphCore and Unity.VisualScripting; the graph uses the GraphCore one
using StickyNote = ArcaneOnyx.GraphCore.StickyNote;

namespace ArcaneOnyx.BehaviorTree.Authoring
{
    /// <summary>
    /// The <c>bt_</c> command surface over <see cref="BehaviorTreeAuthoring"/>: one command per authoring
    /// method, plus the read-only dumps and the catalogue. Nodes are addressed by their graph element
    /// <c>guid</c>, which <c>bt_describe_tree</c> lists and every node-creating command returns, because
    /// a CLI call cannot be handed a live <see cref="BehaviorTreeNode"/> the way a C# caller is.
    /// <para>
    /// This assembly compiles only when <c>com.unity.pipeline</c> is installed. Everything it does is
    /// available without the package through <see cref="BehaviorTreeAuthoring"/> and
    /// <see cref="BehaviorTreeVerification"/>; what the package adds is reaching them from a shell or an
    /// agent.
    /// </para>
    /// </summary>
    public static class BehaviorTreeCommands
    {
        [CliCommand("bt_create_tree",
            "Create an empty behavior tree asset, containing only its Entry node. NOTE: this replaces the " +
            "file, taking any hand-arranged layout with it, and asset creation is not undoable via Ctrl+Z.")]
        public static AuthoringResult CreateTreeCommand(
            [CliArg("path", "Asset path relative to the authoring root; the Assets/ prefix and the .asset extension are optional. e.g. Trees/Draugr", Required = true)] string path,
            [CliArg("overwrite", "Replace an existing tree at this path. Without it an existing asset is left alone — edit it with bt_add_node instead.")] bool overwrite = false)
        {
            var normalized = ResolveTreePath(path);

            if (AssetDatabase.LoadAssetAtPath<UnityEngine.Object>(normalized) != null && !overwrite)
            {
                throw new ArgumentException(
                    $"'{normalized}' already exists. Pass overwrite=true to replace it, or add to it in " +
                    "place — every bt_ command works the same on an existing tree.");
            }

            var asset = CreateTree(normalized);
            Save(asset);

            var result = ObjectResolver.Describe(asset) ?? new AuthoringResult { Type = nameof(BehaviorTreeGraphAsset) };
            result.AssetPath = normalized;

            return result;
        }

        [CliCommand("bt_describe_tree",
            "Dump a tree as JSON: the execution hierarchy in true runtime order, every node's guid, each " +
            "port's source or inline value, guards, embedded script graphs, sub-trees expanded in place, and " +
            "unreachable nodes. This is where the node guids the other bt_ commands take come from, and it is " +
            "how you read a tree you did not author. Read-only.")]
        public static object DescribeTree(
            [CliArg("tree", "Asset path of the behavior tree.", Required = true)] string tree,
            [CliArg("reload", "Re-import the asset first so the dump reflects what survives serialization rather than what is still in memory. Leave on unless you know the tree is untouched since load.")] bool reload = true)
        {
            var asset = ResolveTree(tree, out var normalized);

            // A dump of the in-memory asset reports inline values on bare ports that are gone by the time the
            // asset reloads — the exact defect the dump exists to catch. Round-trip it first.
            if (reload)
            {
                asset = BehaviorTreeVerification.Reload(normalized);
                if (asset == null) throw new InvalidOperationException($"'{normalized}' did not survive a reload.");
            }

            return AsJson(Debugging.BehaviorTreeDump.ToJson(asset));
        }

        [CliCommand("bt_describe_script_graph",
            "Dump any Visual Scripting graph asset as JSON: its own port definitions (where a wrong output key " +
            "or type shows up, which no amount of unit detail reveals), then every unit flat with its guid, " +
            "inline port values and outgoing control edges. Works on the graphs a tree embeds and on a " +
            "Tactical Position Selection query graph. Read-only.")]
        public static object DescribeScriptGraph(
            [CliArg("graph", "Asset path of the ScriptGraphAsset.", Required = true)] string graph)
        {
            var normalized = ResolveTreePath(graph);

            var asset = AssetDatabase.LoadAssetAtPath<ScriptGraphAsset>(normalized);
            if (asset == null) throw new ArgumentException($"No ScriptGraphAsset at '{normalized}'.");

            return AsJson(FlowGraphDump.ToJson(asset.graph, asset.name));
        }

        [CliCommand("bt_verify",
            "Reload one or more trees and report what would break at runtime: ports that read as unset and " +
            "will throw, orphaned nodes, sub-trees with no asset assigned, and recursion. Run this after " +
            "authoring — it forces the serialize/deserialize round trip, so it sees the trees as the machine " +
            "will rather than as they are still held in memory. Reports rather than repairs, but it is not " +
            "read-only: the round trip saves every dirty asset in the project, so anything hooked to a save " +
            "runs, project-wide, not just for the trees named here.")]
        public static object VerifyTrees(
            [CliArg("trees", "Comma-separated asset paths, e.g. Trees/Draugr,Trees/Death", Required = true)] string trees)
        {
            var paths = trees
                .Split(',')
                .Select(t => t.Trim())
                .Where(t => t.Length > 0)
                .Select(ResolveTreePath)
                .ToArray();

            if (paths.Length == 0) throw new ArgumentException("No tree paths given.");

            var findings = BehaviorTreeVerification.Verify(paths);

            return new { verified = paths, clean = findings.Count == 0, findings = findings.ToArray() };
        }

        [CliCommand("bt_retarget_missing",
            "Replace nodes whose C# type no longer exists with a type that does, keeping every value, object " +
            "reference and connection the replacement still has a home for. This is the headless form of the " +
            "inspector's picker; use it when a node type was renamed, moved between namespaces or " +
            "assemblies, or replaced. Note that the durable fix for code you own is [RenamedFrom(\"old.name\")] " +
            "on the new class -- trees restore themselves on load from that, including trees nobody has " +
            "opened. Use this for types you cannot annotate, or to clean up assets now.")]
        public static object RetargetMissingCommand(
            [CliArg("former", "Full name of the type that went missing, e.g. ArcaneOnyx.BehaviorTree.OldNode", Required = true)] string former,
            [CliArg("to", "Full name of the replacement node type, e.g. ArcaneOnyx.BehaviorTree.NewNode", Required = true)] string to,
            [CliArg("tree", "One tree to convert. Omit to convert every tree in the project.")] string tree = null)
        {
            if (!RuntimeCodebase.TryDeserializeType(to, out var target))
            {
                throw new ArgumentException($"No type named '{to}' exists.");
            }

            if (!typeof(BehaviorTreeNode).IsAssignableFrom(target) || target.IsAbstract)
            {
                throw new ArgumentException($"'{to}' is not a concrete {nameof(BehaviorTreeNode)}.");
            }

            if (tree == null)
            {
                var result = MissingTypeRetarget.ApplyToProject(former, target);

                // The paths, not just the count. This writes to disk across the whole project, so the first
                // thing anyone reviewing the result needs is which files it touched.
                return new
                {
                    former, to = target.FullName, scope = "project",
                    nodes = result.Nodes, trees = result.Trees.Count, paths = result.Trees.ToArray(),
                };
            }

            string path = ResolveTreePath(tree);
            var asset = LoadTree(path);
            if (asset == null) throw new ArgumentException($"No behavior tree at '{path}'.");

            int converted = MissingTypeRetarget.ApplyToTree(asset, former, target);

            // Only this tree. SaveAssets() would flush every dirty asset in the project, committing unrelated
            // half-finished edits as a side effect of a command that named one tree.
            if (converted > 0) AssetDatabase.SaveAssetIfDirty(asset);

            return new
            {
                former, to = target.FullName, scope = path,
                nodes = converted, trees = converted > 0 ? 1 : 0,
                paths = converted > 0 ? new[] { path } : Array.Empty<string>(),
            };
        }

        [CliCommand("bt_list_nodes",
            "Catalogue of the node types a tree can be built from: category, what each one does, how many " +
            "children it takes, and which of its ports MUST be connected because they declare no default and " +
            "throw on the first tick. Call this before bt_add_node rather than reading source. Read-only. " +
            "Pass --type for one node's full port list, or --category to narrow the listing.")]
        public static object ListNodes(
            [CliArg("type", "Show full port detail for one node type, e.g. WaitTime.")] string type = null,
            [CliArg("category", "Filter by create-menu category, e.g. Flow, Navigation, Composite.")] string category = null)
        {
            // ports only exist once Define() has run, which happens when a node joins a graph
            var scratch = ScriptableObject.CreateInstance<BehaviorTreeGraphAsset>();

            try
            {
                if (type != null) return DescribeNodeType(scratch, ResolveNodeType(type), detail: true);

                var types = TypeCache.GetTypesDerivedFrom<BehaviorTreeNode>()
                    .Where(t => !t.IsAbstract && !string.IsNullOrEmpty(GraphCore.NodeUtil.GetNodeGraphCreateMenu(t)))
                    .OrderBy(GraphCore.NodeUtil.GetNodeGraphCreateMenu)
                    .ToList();

                if (category != null)
                {
                    types = types
                        .Where(t => GraphCore.NodeUtil.GetNodeGraphCreateMenu(t)
                            .IndexOf(category, StringComparison.OrdinalIgnoreCase) >= 0)
                        .ToList();
                }

                return new
                {
                    note = "'required' ports declare no default: fill them with bt_set_value, which picks a " +
                           "mechanism that survives reload, or the node throws on its first tick. Exception: a " +
                           "Target port read only through GetComponent falls back to the agent's own " +
                           "GameObject and is safe to leave alone. Pass --type <name> for full ports.",
                    nodes = types.Select(t => DescribeNodeType(scratch, t, detail: false)).ToArray()
                };
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(scratch);
            }
        }

        private static object DescribeNodeType(BehaviorTreeGraphAsset scratch, System.Type nodeType, bool detail)
        {
            string menu = GraphCore.NodeUtil.GetNodeGraphCreateMenu(nodeType);
            string description;
            BehaviorTreeNode node;

            try
            {
                node = AddNode(scratch, nodeType, 0f, 0f);
                description = node.Description;
            }
            catch (Exception exception)
            {
                return new { type = nodeType.Name, category = menu, error = exception.Message };
            }

            var inputs = (node.valueInputs ?? Enumerable.Empty<ValueInput>()).ToArray();

            if (!detail)
            {
                return new
                {
                    type = nodeType.Name,
                    name = node.NodeName,
                    category = menu,
                    description = string.IsNullOrEmpty(description) ? null : description,
                    maxChildren = node.MaxChildrenLimit,
                    required = inputs.Where(p => !node.defaultValues.ContainsKey(p.key))
                        .Select(p => $"{p.key}:{p.Type?.Name}").ToArray()
                };
            }

            return new
            {
                type = nodeType.Name,
                name = node.NodeName,
                category = menu,
                description = string.IsNullOrEmpty(description) ? null : description,
                maxChildren = node.MaxChildrenLimit,
                inputs = inputs.Select(port => new
                {
                    key = port.key,
                    type = port.Type?.Name,
                    required = !node.defaultValues.ContainsKey(port.key),
                    inlineCapable = ValueInput.SupportsDefaultValue(port.Type),
                    @default = node.defaultValues.TryGetValue(port.key, out var value) ? Stringify(value) : null
                }).ToArray(),
                outputs = (node.valueOutputs ?? Enumerable.Empty<ValueOutput>())
                    .Select(port => new { key = port.key, type = port.Type?.Name }).ToArray()
            };
        }

        [CliCommand("bt_add_node",
            "Add a node to a tree at a canvas position, sized the way the editor sizes it. Returns the node's " +
            "guid. Child execution order comes from the transition indices bt_connect assigns, whenever those " +
            "form a genuine order (0..n-1, each used once); canvas X decides only when they do not, which is " +
            "what a removal or a hand-edit leaves behind. Lay siblings out left to right in priority order " +
            "regardless — the canvas is read that way, and it is the fallback the runtime uses.")]
        public static object AddNodeCommand(
            [CliArg("tree", "Asset path of the behavior tree.", Required = true)] string tree,
            [CliArg("type", "Node type name, e.g. Sequence, Selector, WaitTime, PlayAnimationAndWait.", Required = true)] string type,
            [CliArg("x", "Canvas X position. For a child, this is its priority among its siblings.")] float x = 0f,
            [CliArg("y", "Canvas Y position.")] float y = 0f)
        {
            var asset = ResolveTree(tree, out _);
            var node = AddNode(asset, ResolveNodeType(type), x, y);
            Save(asset);

            return DescribeNode(node);
        }

        [CliCommand("bt_connect",
            "Parent one node under another. Execution order comes from canvas X, not from this call order or " +
            "the index. Refuses to exceed a container's child limit — Entry and every Decorator take exactly " +
            "one child, and extra transitions would be silent dead weight. Refuses a guard as the child: " +
            "guards attach to their owner and are never children, so connect to the owner instead.")]
        public static object ConnectCommand(
            [CliArg("tree", "Asset path of the behavior tree.", Required = true)] string tree,
            [CliArg("parent", "Guid of the parent node.", Required = true)] string parent,
            [CliArg("child", "Guid of the child node.", Required = true)] string child,
            [CliArg("index", "Transition index on the parent.")] int index = 0)
        {
            var asset = ResolveTree(tree, out _);

            var parentNode = ResolveNode(asset, parent);
            var childNode = ResolveNode(asset, child);

            // the API does not enforce MaxChildrenLimit and the extra children are never entered, so a
            // decorator quietly given two children is a bug nothing reports. Composites report int.MaxValue,
            // leaves report 0.
            int limit = parentNode.MaxChildrenLimit;
            int existing = asset.graph.Transitions.Count(t => t.source == parentNode);

            if (limit == 0)
            {
                throw new InvalidOperationException(
                    $"{parentNode.GetType().Name} takes no children — it is a leaf. Parent this child under a " +
                    "composite or decorator instead.");
            }

            if (existing >= limit)
            {
                throw new InvalidOperationException(
                    $"{parentNode.GetType().Name} accepts {limit} child(ren) and already has {existing}. " +
                    "Only child 0 is ever entered, so the extra would never run.");
            }

            Connect(asset, parentNode, childNode, index);
            Save(asset);

            return DescribeNode(childNode);
        }

        [CliCommand("bt_declare",
            "Declare a variable on the tree. Every read resolves against the root tree, so on a sub-tree an " +
            "'instance' declaration supplies nothing — use 'required' to state what the agent must declare, " +
            "or 'optional' to give the branch a default it carries on its own.")]
        public static object DeclareCommand(
            [CliArg("tree", "Asset path of the behavior tree.", Required = true)] string tree,
            [CliArg("name", "Variable name.", Required = true)] string name,
            [CliArg("value", "Value as text, parsed as 'type'. For 'required' this only carries the type.", Required = true)] string value,
            [CliArg("type", "How to read 'value': string, float, int, bool, vector2 or vector3.")] string type = "string",
            [CliArg("scope", "Which list to write: instance, required or optional.")] string scope = "instance")
        {
            var asset = ResolveTree(tree, out var normalized);

            if (!Enum.TryParse<DeclarationScope>(scope, ignoreCase: true, out var declarationScope))
            {
                throw new ArgumentException(
                    $"Unknown scope '{scope}'. Expected instance, required or optional.");
            }

            Declare(asset, name, ParseNamedType(value, type, name), declarationScope);
            Save(asset);

            return new { tree = normalized, declared = name, value, scope = declarationScope.ToString() };
        }

        [CliCommand("bt_refresh_sub_tree_ports",
            "Rebuild a Run Behavior Tree node's parameter ports from the sub-tree's required and optional " +
            "declarations. Run it after changing a branch's contract; bt_verify reports which nodes need it. " +
            "Omit --node to refresh every sub-tree node in the tree.")]
        public static object RefreshSubTreePortsCommand(
            [CliArg("tree", "Asset path of the behavior tree that contains the sub-tree node(s).", Required = true)] string tree,
            [CliArg("node", "Guid of one Run Behavior Tree node. Omit to refresh all of them.")] string node = null)
        {
            var asset = ResolveTree(tree, out var normalized);

            var targets = asset.graph.Nodes.OfType<RunBehaviorTreeGraphNode>().ToList();

            if (!string.IsNullOrEmpty(node))
            {
                targets = targets.Where(candidate => candidate.guid.ToString() == node).ToList();

                if (targets.Count == 0)
                {
                    throw new ArgumentException($"No Run Behavior Tree node with guid '{node}' in {normalized}.");
                }
            }

            var refreshed = new List<object>();

            foreach (var target in targets)
            {
                // Report the drift before it is resolved — after the refresh there is nothing left to see, and
                // a dropped connection is exactly the thing the caller should know happened.
                var drift = target.DescribeContractDrift();

                var dropped = target.RefreshParameters();

                // The contract just changed, so this is exactly when the node is allowed to be resized.
                var size = ContractPortLayout.ResizeToFitPorts(target);

                refreshed.Add(new
                {
                    node = target.guid.ToString(),
                    subTree = target.BehaviorTreeGraphAsset != null ? target.BehaviorTreeGraphAsset.name : null,
                    ports = target.Parameters.Select(parameter => parameter.ToString()).ToArray(),
                    resolved = drift.ToArray(),
                    droppedConnections = dropped.ToArray(),
                    size = $"{size.width:0} x {size.height:0}"
                });
            }

            Save(asset);

            return new { tree = normalized, refreshed };
        }

        [CliCommand("bt_refresh_guard_keys",
            "Add to each reactive guard's key trigger whatever its condition declares and the trigger does " +
            "not already list. Adds only -- a key the trigger lists that nothing declares may be a " +
            "deliberate hand-typed one, and is reported rather than removed.")]
        public static object RefreshGuardKeysCommand(
            [CliArg("tree", "Asset path of the behavior tree.", Required = true)] string tree,
            [CliArg("node", "Guid of one guard. Omit to refresh every reactive guard in the tree.")] string node = null)
        {
            var asset = ResolveTree(tree, out var normalized);

            var refreshed = new List<object>();

            foreach (var candidate in asset.graph.Nodes.OfType<ReactiveGuard>())
            {
                if (node != null && candidate.guid.ToString() != node) continue;

                var added = candidate.RefreshWatchedKeys();
                if (added.Count == 0) continue;

                refreshed.Add(new
                {
                    node = candidate.guid.ToString(),
                    name = candidate.NodeName,
                    added = added.ToArray(),
                    keys = candidate.Triggers
                        .Where(trigger => trigger != null && trigger.Kind == GuardTriggerKind.OnKeyChanged)
                        .SelectMany(trigger => trigger.Keys)
                        .ToArray()
                });
            }

            if (refreshed.Count == 0)
            {
                return new { tree = normalized, refreshed = 0, note = "every guard already lists what its condition declares." };
            }

            Save(asset);

            return new { tree = normalized, refreshed = refreshed.Count, guards = refreshed };
        }

        [CliCommand("bt_set_value",
            "Give a node's value input a value, by whichever mechanism survives serialization for that port — " +
            "an inline value where the port declares one, a connected literal node where it does not. Prefer " +
            "this over every other way of filling a port.")]
        public static object SetValueCommand(
            [CliArg("tree", "Asset path of the behavior tree.", Required = true)] string tree,
            [CliArg("node", "Guid of the node whose port is being set.", Required = true)] string node,
            [CliArg("port", "Value input key, e.g. Time, TriggerName, Duration.", Required = true)] string port,
            [CliArg("value", "Value as text, parsed against the port's declared type: numbers, bools and strings plainly, Vector2/3 as 'x,y[,z]'.", Required = true)] string value,
            [CliArg("x", "Canvas X for the literal node, if one is needed.")] float x = 0f,
            [CliArg("y", "Canvas Y for the literal node, if one is needed.")] float y = 0f)
        {
            var asset = ResolveTree(tree, out _);
            var target = ResolveNode(asset, node);
            var input = ResolveValueInput(target, port);

            SetValue(asset, input, ParseValue(value, input.Type, port), x, y);
            Save(asset);

            return DescribeNode(target);
        }

        [CliCommand("bt_feed_float",
            "Feed a float port from a literal node, whether or not the port declares a default. bt_set_value " +
            "picks the right mechanism on its own; use this only to force the literal.")]
        public static object FeedFloatCommand(
            [CliArg("tree", "Asset path of the behavior tree.", Required = true)] string tree,
            [CliArg("node", "Guid of the node whose port is being fed.", Required = true)] string node,
            [CliArg("port", "Value input key.", Required = true)] string port,
            [CliArg("value", "Float value.", Required = true)] float value,
            [CliArg("x", "Canvas X for the literal node.")] float x = 0f,
            [CliArg("y", "Canvas Y for the literal node.")] float y = 0f)
        {
            var asset = ResolveTree(tree, out _);
            var target = ResolveNode(asset, node);

            FeedFloat(asset, ResolveValueInput(target, port), value, x, y);
            Save(asset);

            return DescribeNode(target);
        }

        [CliCommand("bt_feed_vector3", "Feed a Vector3 port from a literal node. See bt_feed_float.")]
        public static object FeedVector3Command(
            [CliArg("tree", "Asset path of the behavior tree.", Required = true)] string tree,
            [CliArg("node", "Guid of the node whose port is being fed.", Required = true)] string node,
            [CliArg("port", "Value input key.", Required = true)] string port,
            [CliArg("value", "Vector3 as 'x,y,z'.", Required = true)] string value,
            [CliArg("x", "Canvas X for the literal node.")] float x = 0f,
            [CliArg("y", "Canvas Y for the literal node.")] float y = 0f)
        {
            var asset = ResolveTree(tree, out _);
            var target = ResolveNode(asset, node);

            FeedVector3(asset, ResolveValueInput(target, port), (Vector3)ParseValue(value, typeof(Vector3), port), x, y);
            Save(asset);

            return DescribeNode(target);
        }

        [CliCommand("bt_guard_on_variable",
            "Guard a node on a boolean agent variable. Guards are re-evaluated every tick while the owner " +
            "runs and several may name the same owner — they are ANDed. This is the mechanism for " +
            "interrupting a running branch; prefer it over a Condition node.")]
        public static object GuardOnVariableCommand(
            [CliArg("tree", "Asset path of the behavior tree.", Required = true)] string tree,
            [CliArg("owner", "Guid of the node the guard protects.", Required = true)] string owner,
            [CliArg("variable", "Agent variable name to read.", Required = true)] string variable,
            [CliArg("expected", "Value the variable must hold for the owner to run. false inserts a Not node.")] bool expected = true,
            [CliArg("fallback", "Value used when the agent does not declare the variable at all, so a reused branch does not throw.")] bool fallback = false,
            [CliArg("x", "Canvas X position for the guard.")] float x = 0f,
            [CliArg("y", "Canvas Y position for the guard.")] float y = 0f)
        {
            var asset = ResolveTree(tree, out _);
            var ownerNode = ResolveNode(asset, owner);

            var guard = GuardOnVariable(asset, ownerNode, variable, expected, fallback, x, y);
            Save(asset);

            return DescribeNode(guard);
        }

        [CliCommand("bt_guard_on_function",
            "Guard a node on a Function returning bool — a named, shared predicate one fix updates everywhere. " +
            "The guard's recompute schedule is seeded from the Function's declared watched keys, so it wakes " +
            "when those facts change instead of re-running every tick. Prefer this over bt_guard_on_variable " +
            "when the condition is more than a single variable read.")]
        public static object GuardOnFunctionCommand(
            [CliArg("tree", "Asset path of the behavior tree.", Required = true)] string tree,
            [CliArg("owner", "Guid of the node the guard protects.", Required = true)] string owner,
            [CliArg("function", "Asset path of the Function to use as the condition.", Required = true)] string function,
            [CliArg("expected", "Value the Function must return for the owner to run. false inserts a Not node.")] bool expected = true,
            [CliArg("x", "Canvas X position for the guard.")] float x = 0f,
            [CliArg("y", "Canvas Y position for the guard.")] float y = 0f)
        {
            var asset = ResolveTree(tree, out _);
            var ownerNode = ResolveNode(asset, owner);

            var guard = GuardOnFunction(
                asset, ownerNode, FunctionGraphAuthoring.ResolveFunction(function), expected, x, y);

            Save(asset);

            return DescribeNode(guard);
        }

        [CliCommand("bt_add_variable_read",
            "Add a node that reads an agent variable through a Visual Scripting graph with a fallback, for " +
            "feeding a port. bt_guard_on_variable already does this for guards — use this for everything else.")]
        public static object AddVariableReadCommand(
            [CliArg("tree", "Asset path of the behavior tree.", Required = true)] string tree,
            [CliArg("variable", "Agent variable name to read.", Required = true)] string variable,
            [CliArg("fallback", "Value used when the agent does not declare the variable, as text.", Required = true)] string fallback,
            [CliArg("fallback_type", "How to read 'fallback': bool, string, float, int, vector2 or vector3.")] string fallbackType = "bool",
            [CliArg("x", "Canvas X position.")] float x = 0f,
            [CliArg("y", "Canvas Y position.")] float y = 0f)
        {
            var asset = ResolveTree(tree, out _);

            var read = AddNode<VisualScriptGraphVariable>(asset, x, y);
            read.SetFunction(CreateVariableReadFunction(asset, variable, ParseNamedType(fallback, fallbackType, "fallback")));
            SetComment(read, variable);

            Save(asset);

            return DescribeNode(read);
        }

        [CliCommand("bt_add_sub_tree",
            "Run another tree as a child. Guards do not cross the boundary — to let a caller interrupt a " +
            "sub-tree, guard this node in the parent. A tree must not reach itself.")]
        public static object AddSubTreeCommand(
            [CliArg("tree", "Asset path of the behavior tree being edited.", Required = true)] string tree,
            [CliArg("sub_tree", "Asset path of the tree to run.", Required = true)] string subTree,
            [CliArg("x", "Canvas X position.")] float x = 0f,
            [CliArg("y", "Canvas Y position.")] float y = 0f)
        {
            var asset = ResolveTree(tree, out var normalized);
            var subTreeAsset = ResolveTree(subTree, out var subNormalized);

            if (subNormalized == normalized)
                throw new ArgumentException("A tree cannot run itself — that is a cycle and throws at load.");

            var node = AddSubTree(asset, subTreeAsset, x, y);
            Save(asset);

            return DescribeNode(node);
        }

        [CliCommand("bt_add_sticky",
            "Add a canvas sticky note. Notes live outside the node collection, so they never affect " +
            "execution. Re-using a title replaces that note rather than stacking a duplicate.")]
        public static object AddStickyCommand(
            [CliArg("tree", "Asset path of the behavior tree.", Required = true)] string tree,
            [CliArg("title", "Note title; also the key that makes this call repeatable.", Required = true)] string title,
            [CliArg("body", "Note body.", Required = true)] string body,
            [CliArg("x", "Canvas X position.")] float x = 0f,
            [CliArg("y", "Canvas Y position.")] float y = 0f,
            [CliArg("width", "Note width.")] float width = 300f,
            [CliArg("height", "Note height.")] float height = 150f,
            [CliArg("color", "Colour theme, e.g. Classic, Teal, Orange.")] string color = "Classic")
        {
            var asset = ResolveTree(tree, out var normalized);

            if (!Enum.TryParse<StickyNote.ColorEnum>(color, true, out var theme))
            {
                throw new ArgumentException(
                    $"'{color}' is not a sticky colour. Options: {string.Join(", ", Enum.GetNames(typeof(StickyNote.ColorEnum)))}.");
            }

            AddSticky(asset, title, body, x, y, width, height, theme);
            Save(asset);

            return new { tree = normalized, sticky = title };
        }

        [CliCommand("bt_set_comment",
            "Set a node's comment, which is what the canvas shows as its title — it is how a generated node " +
            "stays readable.")]
        public static object SetCommentCommand(
            [CliArg("tree", "Asset path of the behavior tree.", Required = true)] string tree,
            [CliArg("node", "Guid of the node.", Required = true)] string node,
            [CliArg("comment", "Comment text.", Required = true)] string comment)
        {
            var asset = ResolveTree(tree, out _);
            var target = ResolveNode(asset, node);

            SetComment(target, comment);
            Save(asset);

            return DescribeNode(target);
        }

        [CliCommand("bt_save", "Flush in-memory edits of a tree to disk. NOTE: not undoable via Ctrl+Z.")]
        public static AuthoringResult SaveCommand(
            [CliArg("tree", "Asset path of the behavior tree.", Required = true)] string tree)
        {
            var asset = ResolveTree(tree, out var normalized);
            Save(asset);

            var result = ObjectResolver.Describe(asset) ?? new AuthoringResult { Type = nameof(BehaviorTreeGraphAsset) };
            result.AssetPath = normalized;

            return result;
        }

        // --- CLI plumbing ---------------------------------------------------------------------------

        /// <summary>
        /// The dumps build their JSON as text, because they predate the CLI and were written to be saved to a
        /// file. Returned as a string the server would escape the whole document into one field, and the
        /// caller would have to unpack it a second time before reading anything. Parsing it here hands back
        /// real JSON instead — same content, none of the escaping.
        /// </summary>
        private static object AsJson(string dump)
        {
            try
            {
                return JToken.Parse(dump);
            }
            catch (JsonReaderException)
            {
                // a malformed dump is still more useful raw than swallowed
                return dump;
            }
        }

        private static string ResolveTreePath(string path)
        {
            var normalized = ProjectPaths.Resolve(path, out var error);
            if (normalized == null) throw new ArgumentException(error);

            return normalized.EndsWith(".asset", StringComparison.OrdinalIgnoreCase) ? normalized : normalized + ".asset";
        }

        private static BehaviorTreeGraphAsset ResolveTree(string path, out string normalized)
        {
            normalized = ResolveTreePath(path);

            var asset = AssetDatabase.LoadAssetAtPath<BehaviorTreeGraphAsset>(normalized);
            if (asset == null) throw new ArgumentException($"No behavior tree at '{normalized}'.");

            return asset;
        }

        private static BehaviorTreeNode ResolveNode(BehaviorTreeGraphAsset asset, string nodeGuid)
        {
            if (!Guid.TryParse(nodeGuid, out var parsed))
                throw new ArgumentException($"'{nodeGuid}' is not a guid. Run bt_describe_tree to list them.");

            var node = asset.graph.Nodes.FirstOrDefault(n => n.guid == parsed);
            if (node == null) throw new ArgumentException($"The tree has no node with guid '{nodeGuid}'.");

            return node;
        }

        private static System.Type ResolveNodeType(string typeName)
        {
            var matches = TypeCache.GetTypesDerivedFrom<BehaviorTreeNode>()
                .Where(t => !t.IsAbstract && (t.Name == typeName || t.FullName == typeName))
                .ToArray();

            if (matches.Length == 0)
                throw new ArgumentException($"No behavior tree node type named '{typeName}'.");

            if (matches.Length > 1)
            {
                throw new ArgumentException(
                    $"'{typeName}' is ambiguous: {string.Join(", ", matches.Select(t => t.FullName))}. Pass the full name.");
            }

            return matches[0];
        }

        /// <summary>Reflective form of <see cref="AddNode{T}"/>, for a caller that only has the type name.</summary>
        private static BehaviorTreeNode AddNode(BehaviorTreeGraphAsset asset, System.Type nodeType, float x, float y)
        {
            var node = (BehaviorTreeNode)Activator.CreateInstance(nodeType);
            node.Position = new Rect(new Vector2(x, y), node.StartingSize);

            asset.graph.Nodes.Add(node);

            return node;
        }

        private static ValueInput ResolveValueInput(BehaviorTreeNode node, string portKey)
        {
            var port = node.valueInputs?.FirstOrDefault(p => p.key == portKey);
            if (port != null) return port;

            throw new ArgumentException(
                $"{node.GetType().Name} has no value input '{portKey}'. It has: " +
                $"{string.Join(", ", node.valueInputs?.Select(p => p.key) ?? Enumerable.Empty<string>())}.");
        }

        /// <summary>
        /// Parses a command-line string against the port's declared type. Ports carry their type, so the
        /// caller does not have to say whether "5" is an int or a float — the port already knows.
        /// </summary>
        private static object ParseValue(string raw, System.Type portType, string portKey)
        {
            try
            {
                if (portType == typeof(string)) return raw;
                if (portType == typeof(bool)) return bool.Parse(raw);
                if (portType == typeof(int)) return int.Parse(raw, CultureInfo.InvariantCulture);
                if (portType == typeof(float)) return float.Parse(raw, CultureInfo.InvariantCulture);
                if (portType.IsEnum) return Enum.Parse(portType, raw, true);

                if (portType == typeof(Vector2))
                {
                    var v2 = ParseNumbers(raw, 2);
                    return new Vector2(v2[0], v2[1]);
                }

                if (portType == typeof(Vector3))
                {
                    var v3 = ParseNumbers(raw, 3);
                    return new Vector3(v3[0], v3[1], v3[2]);
                }
            }
            catch (Exception exception) when (exception is FormatException or OverflowException or ArgumentException)
            {
                throw new ArgumentException($"'{raw}' is not a valid {portType.Name} for port '{portKey}'.");
            }

            throw new ArgumentException(
                $"Port '{portKey}' is a {portType.Name}, which has no text form here — connect it to a node " +
                "that produces one instead.");
        }

        /// <summary>Parses against a type the caller names, for values that reach no typed port.</summary>
        private static object ParseNamedType(string raw, string typeName, string what)
        {
            switch (typeName.ToLowerInvariant())
            {
                case "string": return ParseValue(raw, typeof(string), what);
                case "bool": return ParseValue(raw, typeof(bool), what);
                case "int": return ParseValue(raw, typeof(int), what);
                case "float": return ParseValue(raw, typeof(float), what);
                case "vector2": return ParseValue(raw, typeof(Vector2), what);
                case "vector3": return ParseValue(raw, typeof(Vector3), what);
            }

            throw new ArgumentException(
                $"'{typeName}' is not a supported type for {what}. Use string, bool, int, float, vector2 or vector3.");
        }

        private static float[] ParseNumbers(string raw, int components)
        {
            var parts = raw.Split(',');
            if (parts.Length != components)
                throw new FormatException($"expected {components} comma-separated numbers");

            return parts.Select(p => float.Parse(p.Trim(), CultureInfo.InvariantCulture)).ToArray();
        }

        private static object DescribeNode(BehaviorTreeNode node)
        {
            if (node == null) return null;

            return new
            {
                guid = node.guid.ToString(),
                type = node.GetType().Name,
                comment = GetPrivateField(node, "comment") as string,
                x = node.Position.x,
                y = node.Position.y,
                maxChildren = node.MaxChildrenLimit,
                inputs = (node.valueInputs ?? Enumerable.Empty<ValueInput>()).Select(port => new
                {
                    key = port.key,
                    type = port.Type?.Name,
                    source = DescribeSource(port),
                    // a port with neither a source nor a value throws on the first tick
                    value = node.defaultValues.TryGetValue(port.key, out var inline) ? Stringify(inline) : null
                }).ToArray(),
                outputs = (node.valueOutputs ?? Enumerable.Empty<ValueOutput>())
                    .Select(port => port.key).ToArray()
            };
        }

        private static string DescribeSource(ValueInput port)
        {
            var connection = port.validConnections?.FirstOrDefault();
            if (connection?.source?.behaviorTreeNode == null) return null;

            return $"{connection.source.behaviorTreeNode.GetType().Name}.{connection.source.key}";
        }

        private static string Stringify(object value)
        {
            return value switch
            {
                null => null,
                float f => f.ToString(CultureInfo.InvariantCulture),
                _ => value.ToString()
            };
        }

        /// <summary>The read half of <see cref="SetPrivateField"/>, for reporting what a node was given.</summary>
        private static object GetPrivateField(object target, string fieldName)
        {
            var type = target.GetType();

            while (type != null)
            {
                var field = type.GetField(fieldName,
                    System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic |
                    System.Reflection.BindingFlags.DeclaredOnly);

                if (field != null) return field.GetValue(target);

                type = type.BaseType;
            }

            return null;
        }
    }
}
