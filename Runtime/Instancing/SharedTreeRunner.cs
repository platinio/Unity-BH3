using System;
using System.Collections.Generic;
using System.Reflection;
using ArcaneOnyx.GraphCore;

namespace ArcaneOnyx.BehaviorTree.Instancing
{
    /// <summary>
    /// Ticks agents over one shared tree, calling <b>the node classes themselves</b>.
    ///
    /// <para>
    /// This is the whole claim of spec 07 step 4, and the thing that makes the two runtimes one system
    /// rather than two: there is no second implementation of any node here. <c>WaitTime.OnUpdate(ctx)</c>
    /// is the same method <see cref="BehaviorTreeMachine"/> calls on a cloned tree. Only the context
    /// differs — one is backed by the node, one by this agent's arrays — and the body cannot tell.
    /// </para>
    ///
    /// <para>
    /// It owns the bookkeeping <c>BaseGraphNode</c> keeps on the node instance, because that state is
    /// per-agent too: the running flag and the last status are slots in
    /// <see cref="TreeInstance"/>, not fields on the shared node.
    /// </para>
    /// </summary>
    public static class SharedTreeRunner
    {
        /// <summary>Ticks a node: enter if not running (guards permitting), tick, exit on completion.</summary>
        public static ExecutionStatus TickNode(TreeInstance instance, int index)
        {
            var node = instance.Plan.Nodes[index];
            RequireMigrated(node, TickHooks);

            var context = new BTContext(instance, index);

            if (!instance.Running[index])
            {
                var guards = instance.Plan.GuardsOn(index);

                for (int i = 0; i < guards.Count; i++)
                {
                    // Demo scope: entry-only evaluation. A guard that keeps per-agent state (ReactiveGuard's
                    // cache, GuardTrigger's seen versions) needs that state in instance memory first --
                    // spec 13 maps every field. A stateless guard reading a port works on both runtimes
                    // unchanged, which is what the demo tree uses.
                    if (!guards[i].Evaluate())
                    {
                        instance.LastStatus[index] = ExecutionStatus.Failure;
                        return ExecutionStatus.Failure;
                    }
                }

                instance.Running[index] = true;
                node.OnEnter(context);
            }

            var status = node.OnUpdate(context);
            instance.LastStatus[index] = status;

            if (status != ExecutionStatus.Running) ExitNode(instance, index);

            return status;
        }

        /// <summary>Exits a node if it is running. Idempotent, like <c>OnNodeExit</c>'s early return.</summary>
        public static void ExitNode(TreeInstance instance, int index)
        {
            if (!instance.Running[index]) return;

            instance.Running[index] = false;
            instance.Plan.Nodes[index].OnExit(new BTContext(instance, index));
        }

        /// <summary>Runs the awake pass for every node in the tree, once per agent.</summary>
        public static void AwakeAll(TreeInstance instance)
        {
            for (int i = 0; i < instance.Plan.Nodes.Length; i++)
            {
                var node = instance.Plan.Nodes[i];
                RequireMigrated(node, AwakeHooks);
                node.OnAwake(new BTContext(instance, i));
            }
        }

        private static readonly Dictionary<(Type, string[]), string> unmigrated = new();

        /// <summary>
        /// Checked before the awake pass, which runs on <em>every</em> node in the graph.
        /// </summary>
        private static readonly string[] AwakeHooks = { "OnAwake" };

        /// <summary>
        /// Checked before a node is ticked. Deliberately excludes <c>OnAwake</c>, and only these three are
        /// asked, because the awake pass reaches nodes the tick walk never does — a <c>Literal</c> is pulled
        /// through a port and never entered, so its unmigrated <c>OnUpdate()</c> is not a problem to solve
        /// before it can be woken.
        /// </summary>
        private static readonly string[] TickHooks = { "OnEnter", "OnUpdate", "OnExit" };

        /// <summary>
        /// Refuses to run a node that still keeps its state in fields.
        ///
        /// <para>
        /// On a shared tree an unmigrated node is not slow, it is <em>wrong</em>: every agent writes the
        /// same field, so a <c>WaitTime</c> would count down one timer shared by all of them. That failure
        /// is silent and looks like the tree misbehaving, so it is turned into an exception naming the node
        /// and the fix. This is the runtime counterpart of <c>NodeContextConventionTests</c>, which fails
        /// the build for the same reason.
        /// </para>
        /// </summary>
        private static void RequireMigrated(BehaviorTreeNode node, string[] hooks)
        {
            var type = node.GetType();
            var key = (type, hooks);

            if (!unmigrated.TryGetValue(key, out var problem))
            {
                problem = FindLegacyOverride(type, hooks);
                unmigrated[key] = problem;
            }

            if (problem == null) return;

            throw new InvalidOperationException(
                $"'{node.NodeName}' ({type.Name}) still overrides {problem} and cannot run on a shared tree: "
                + "its state would be shared by every agent. Migrate it to the context-taking overload and "
                + "move its fields into ctx.Memory<T>().");
        }

        /// <summary>
        /// Which hook, if any, this type would run the <em>legacy</em> body for.
        ///
        /// <para>
        /// Not "does any ancestor override the parameterless hook" — that flags a migrated node whose base
        /// class has not moved yet, and those are fine: a context override on a subclass shadows the legacy
        /// override above it, so the legacy body is unreachable. <c>Entry</c> overriding
        /// <c>OnUpdate(BTContext)</c> is correct even though <c>ContainerNode.OnUpdate()</c> still exists.
        /// </para>
        ///
        /// <para>
        /// The question is which declaration is <b>most derived</b>. Walking down from the concrete type,
        /// the first class that declares either form decides: the context form means migrated, the bare
        /// form means the forwarding default will reach a legacy body. A class declaring both is migrated —
        /// the context override is what virtual dispatch selects.
        /// </para>
        /// </summary>
        private static string FindLegacyOverride(Type type, string[] hooks)
        {
            const BindingFlags Declared = BindingFlags.Instance | BindingFlags.Public
                                          | BindingFlags.NonPublic | BindingFlags.DeclaredOnly;

            foreach (var hook in hooks)
            {
                for (var t = type; t != null && t != typeof(BehaviorTreeNode); t = t.BaseType)
                {
                    if (t.GetMethod(hook, Declared, null, new[] { typeof(BTContext) }, null) != null) break;

                    if (t.GetMethod(hook, Declared, null, Type.EmptyTypes, null) != null)
                    {
                        return $"{hook}() on {t.Name}";
                    }
                }
            }

            return null;
        }
    }
}
