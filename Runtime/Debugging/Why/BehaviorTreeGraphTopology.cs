using System;
using System.Collections.Generic;
using ArcaneOnyx.GraphCore;

namespace ArcaneOnyx.BehaviorTree.Debugging
{
    /// <summary>
    /// The structural half of an explanation, read off a real graph.
    ///
    /// <para>
    /// Built from transitions rather than from <see cref="ContainerNode.GetChildren"/> because transitions are
    /// the serialized truth and children are a runtime cache populated at awake — so this works on a tree that
    /// has not been played, which is what lets an explanation of an imported recording be given names.
    /// Children are ordered by <see cref="BehaviorTreeGraph.ChildrenInPriorityOrder"/>, the same rule the
    /// runtime uses.
    /// </para>
    /// </summary>
    public sealed class BehaviorTreeGraphTopology : IBehaviorTreeTopology
    {
        /// <summary>How far to chase a guard's inputs looking for variable reads before giving up.</summary>
        private const int MaxGuardInputDepth = 8;

        private readonly Dictionary<Guid, BehaviorTreeNodeInfo> nodes = new();
        private readonly Dictionary<Guid, IReadOnlyList<string>> guardReads = new();

        private BehaviorTreeGraphTopology() { }

        /// <summary>
        /// Reads a graph, and every sub-tree instance already running under it.
        ///
        /// <para>
        /// Only branches that have actually been entered are walked: reading a
        /// <see cref="RunBehaviorTreeGraphNode"/>'s instance clones the asset on first access, so walking
        /// unconditionally would clone every branch of every agent merely by opening the inspector.
        /// </para>
        /// </summary>
        public static BehaviorTreeGraphTopology From(BehaviorTreeGraph graph)
        {
            var topology = new BehaviorTreeGraphTopology();
            if (graph == null) return topology;

            topology.Read(graph, new HashSet<BehaviorTreeGraph>());

            return topology;
        }

        /// <summary>
        /// The tree an agent is running, including the branches it has entered so far.
        ///
        /// <para>
        /// Asks the machine what it is running rather than reaching for its cloned asset. An agent whose
        /// tree is authored into the scene has no clone — it runs <c>nest.embed</c> — so reading the clone
        /// returned an empty topology for it, and every panel built on this then showed that agent a tree
        /// with no nodes in it and fell back to bare guids.
        /// </para>
        /// </summary>
        public static BehaviorTreeGraphTopology From(BehaviorTreeMachine machine)
        {
            return From(machine != null ? machine.RunningGraph : null);
        }

        public bool TryGetNode(Guid guid, out BehaviorTreeNodeInfo node) => nodes.TryGetValue(guid, out node);

        public bool TryGetGuardReads(Guid guardGuid, out IReadOnlyList<string> variableKeys)
        {
            return guardReads.TryGetValue(guardGuid, out variableKeys) && variableKeys.Count > 0;
        }

        private void Read(BehaviorTreeGraph graph, HashSet<BehaviorTreeGraph> visited)
        {
            if (graph == null || !visited.Add(graph)) return;

            var children = new Dictionary<Guid, List<BehaviorTreeNode>>();
            var parents = new Dictionary<Guid, Guid>();

            foreach (var transition in graph.Transitions)
            {
                if (transition?.source == null || transition.destination == null) continue;

                parents[transition.destination.guid] = transition.source.guid;
            }

            // Ordered by the graph rather than here, so the priority this reports is the priority that runs.
            // A why-panel that numbered branches differently from the runtime would explain the wrong one.
            //
            // One call for the whole graph rather than one per node: the panels rebuild their topology every
            // frame while an agent is running — their cache key includes the recording's tick — so asking per
            // node would rescan the transition list once per node, every frame.
            foreach (var pair in graph.ChildrenByParentInPriorityOrder())
            {
                children[pair.Key.guid] = pair.Value;
            }

            foreach (var node in graph.Nodes)
            {
                // Placeholder nodes exist so a transition line can be selected on the canvas. They are not
                // authored structure and have no ports, so nothing downstream should ever see one.
                if (node == null || !node.IsVisible) continue;

                var ordered = OrderedChildGuids(children, node.guid);
                parents.TryGetValue(node.guid, out var parent);

                nodes[node.guid] = new BehaviorTreeNodeInfo(
                    node.guid, DisplayNameOf(node), node.GetType().Name, parent, ordered);

                if (node is ConditionalExecution guard) guardReads[guard.guid] = ReadGuardVariables(guard);

                if (node is RunBehaviorTreeGraphNode runNode && runNode.HasBehaviorTreeGraphInstance)
                {
                    Read(runNode.BehaviorTreeGraphInstance, visited);
                }
            }
        }

        /// <summary>
        /// What to call a node in a sentence.
        ///
        /// <para>
        /// Guards are the reason this is not simply <c>NodeName</c>. Every
        /// <see cref="BooleanConditionalExecution"/> is called "Boolean Conditional Execution", so a sentence
        /// naming one tells the reader nothing and a tree with four guards names them all identically. What a
        /// designer actually calls a guard is the label on the node feeding it — "hasTarget",
        /// "not hasTarget", which is what the authoring helpers write and what the canvas shows.
        /// </para>
        ///
        /// <para>
        /// So a guard is named after the source its value ultimately comes from: follow the value inputs to a
        /// node that has none of its own. That deliberately walks past the operators in between — the Not in
        /// "not hasTarget" is plumbing, and naming a guard "Not" would be no better than naming it after its
        /// own type. A guard reading several variables gets one of them, which is still a name a reader
        /// recognises.
        /// </para>
        /// </summary>
        private static string DisplayNameOf(BehaviorTreeNode node)
        {
            if (node is not ConditionalExecution guard) return node.NodeName;

            var source = ValueSourceUpstream(guard, new HashSet<Guid>(), 0);

            // A variable read is named by what it reads. Its own NodeName is "Get Variable", which is the same
            // string for every such guard in the project — exactly the uselessness this walk exists to avoid.
            if (source is GetVariable read)
            {
                var key = ReadKey(read);
                if (!string.IsNullOrEmpty(key)) return key;
            }

            return source == null ? node.NodeName : source.NodeName;
        }

        private static BehaviorTreeNode ValueSourceUpstream(BehaviorTreeNode node, HashSet<Guid> seen, int depth)
        {
            if (node == null || depth > MaxGuardInputDepth || !seen.Add(node.guid)) return null;

            // A variable read is the end of the walk, not a waypoint. Its Key port is fed by a literal holding
            // the variable's name, so following it names the guard "String Literal" — the plumbing that
            // supplies the question rather than the thing the guard is asking about.
            if (depth > 0 && node is GetVariable) return node;

            BehaviorTreeNode deepest = null;

            if (node.valueInputs != null)
            {
                foreach (var input in node.valueInputs)
                {
                    if (input?.connection?.source?.behaviorTreeNode is not BehaviorTreeNode source) continue;

                    deepest = ValueSourceUpstream(source, seen, depth + 1) ?? source;
                    if (deepest != null) break;
                }
            }

            // A guard with nothing connected has no better name than its own, and returning the guard itself
            // would make the caller name it after the thing it was trying to avoid naming it after.
            if (deepest == null && depth > 0) return node;

            return deepest;
        }

        private static IReadOnlyList<Guid> OrderedChildGuids(Dictionary<Guid, List<BehaviorTreeNode>> children, Guid guid)
        {
            if (!children.TryGetValue(guid, out var list) || list.Count == 0) return Array.Empty<Guid>();

            // Already in priority order — the index in this list is the priority number a designer sees.
            var guids = new Guid[list.Count];
            for (int i = 0; i < list.Count; i++)
            {
                guids[i] = list[i].guid;
            }

            return guids;
        }

        /// <summary>
        /// Which variables a guard's result depends on, by walking back from its inputs.
        ///
        /// <para>
        /// This is what upgrades "the last thing that changed before the guard flipped" into "the variable this
        /// guard reads changed". It follows value connections through whatever sits between — a Not, a
        /// comparison — and collects every <see cref="GetVariable"/> it reaches. Anything it cannot see through,
        /// such as a key computed inside a Visual Scripting graph, simply yields nothing and the explainer
        /// falls back to the weaker wording rather than inventing an answer.
        /// </para>
        /// </summary>
        private static IReadOnlyList<string> ReadGuardVariables(ConditionalExecution guard)
        {
            var keys = new List<string>();
            var seen = new HashSet<Guid>();

            Collect(guard, keys, seen, 0);

            return keys;
        }

        private static void Collect(BehaviorTreeNode node, List<string> keys, HashSet<Guid> seen, int depth)
        {
            if (node == null || depth > MaxGuardInputDepth || !seen.Add(node.guid)) return;

            if (node is GetVariable getVariable) Add(keys, ReadKey(getVariable));

            // The guard pattern the authoring helpers produce reads its variable inside a Visual Scripting
            // graph, not through a behavior tree node — so a walk that only followed node ports would resolve
            // nothing on the majority of real trees and hedge every sentence it produced.
            CollectFromScriptGraphs(node, keys);

            // A Function reaches nothing above. It is not a ScriptGraphAsset — it derives from Macro<FlowGraph>
            // directly, because ScriptGraphAsset is sealed — so it can never arrive through
            // scriptGraphAssets, and this walk was silently blind to every guard whose condition is one.
            // Widening that seam is not the fix: it is also what the repository sweep enumerates, and a
            // standalone Function must never be a deletion candidate.
            if (node is IDeclaresWatchedKeys declarer && declarer.DeclaredWatchedKeys != null)
            {
                foreach (var declared in declarer.DeclaredWatchedKeys) Add(keys, declared);
            }

            if (node.valueInputs == null) return;

            foreach (var input in node.valueInputs)
            {
                var connection = input?.connection;
                if (connection?.source?.behaviorTreeNode is BehaviorTreeNode source)
                {
                    Collect(source, keys, seen, depth + 1);
                }
            }
        }

        /// <summary>
        /// Variable names read by the Visual Scripting graphs hanging off a node.
        ///
        /// <para>
        /// The Functions a node reads are the seam here — the same one the tree dump reads — so this sees
        /// exactly what the dump can see and nothing it cannot.
        /// </para>
        /// </summary>
        private static void CollectFromScriptGraphs(BehaviorTreeNode node, List<string> keys)
        {
            if (node is not BaseVisualScriptingNode holder) return;

            foreach (var asset in holder.Functions)
            {
                if (asset == null || asset.graph == null) continue;

                foreach (var unit in asset.graph.units)
                {
                    // Fully qualified: inside this namespace the bare name is the behavior tree node, not the
                    // Visual Scripting unit, and the two are unrelated types with the same short name.
                    if (unit is Unity.VisualScripting.GetVariable read) Add(keys, ReadKey(read));
                }
            }
        }

        private static string ReadKey(Unity.VisualScripting.GetVariable read)
        {
            // Authored graphs set the name inline; a hand-built one may feed it from a literal instead.
            if (read.defaultValues != null && read.defaultValues.TryGetValue("name", out var inline))
            {
                if (inline is string named && !string.IsNullOrEmpty(named)) return named;
            }

            var connection = read.name?.connection;

            return connection?.source?.unit is Unity.VisualScripting.Literal literal ? literal.value as string : null;
        }

        private static void Add(List<string> keys, string key)
        {
            if (string.IsNullOrEmpty(key) || keys.Contains(key)) return;

            keys.Add(key);
        }

        private static string ReadKey(GetVariable getVariable)
        {
            // The Key port is usually fed by a literal, whose value lives in a private serialized field, so
            // asking the port is the only way to get it without reflecting into every literal type. It throws
            // when nothing is connected, which is a tree that would throw at runtime anyway — not this tool's
            // problem to report, and certainly not its problem to crash on.
            try
            {
                return getVariable.Key?.GetValueOrDefault<string>();
            }
            catch (Exception)
            {
                return null;
            }
        }
    }
}
