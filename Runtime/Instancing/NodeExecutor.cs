using System;
using System.Collections.Generic;
using ArcaneOnyx.GraphCore;

namespace ArcaneOnyx.BehaviorTree.Instancing
{
    /// <summary>
    /// A node type's behavior in the shared-runtime world: logic with no home for state. Executors are
    /// singletons shared by every node of their type on every agent — they physically cannot hold per-agent
    /// state, which is the property the whole runtime is built on. State goes through
    /// <see cref="BTick.Memory{T}"/>; config through <see cref="BTick.Config{T}"/>.
    ///
    /// <para>
    /// <b>Why logic lives outside the node class in this demo.</b> Two reasons, one honest and one
    /// demonstrative. The demonstrative one: with executors external, the old runtime's files carry zero
    /// edits, so "the two runtimes coexist without touching each other" is checkable with <c>git diff</c>
    /// rather than taken on faith. The honest one: this is also the shape that supports node types whose
    /// source you do not own. The production design would likely put these bodies back on the node classes
    /// as a second set of virtual methods — same contract, better discoverability — and spec 14 shows that
    /// variant side by side.
    /// </para>
    /// </summary>
    public abstract class NodeExecutor
    {
        /// <summary>Called once when the node starts, after its guards said yes. Seed memory here.</summary>
        public virtual void Enter(in BTick tick) { }

        /// <summary>Called every tick while running.</summary>
        public abstract ExecutionStatus Tick(in BTick tick);

        /// <summary>Called when the node ends or is torn down. Composites exit their children here.</summary>
        public virtual void Exit(in BTick tick) { }
    }

    /// <summary>Typed convenience: the shared node, already cast.</summary>
    public abstract class NodeExecutor<TNode> : NodeExecutor where TNode : BehaviorTreeNode
    {
        protected static TNode Node(in BTick tick) => (TNode)tick.Node;
    }

    /// <summary>
    /// Which executor runs which node type. Exact-type registration, resolved once per plan bake in
    /// practice; a node type with no executor is a loud error naming what the demo does not cover, because
    /// a runtime that silently skipped nodes would look like a tree mysteriously misbehaving.
    /// </summary>
    public static class NodeExecutors
    {
        private static readonly Dictionary<Type, NodeExecutor> registry = new();

        public static void Register<TNode>(NodeExecutor executor) where TNode : BehaviorTreeNode =>
            registry[typeof(TNode)] = executor;

        public static NodeExecutor For(BehaviorTreeNode node)
        {
            if (registry.TryGetValue(node.GetType(), out var executor)) return executor;

            throw new NotSupportedException(
                $"No executor registered for node type '{node.GetType().Name}' ('{node.NodeName}'). "
                + $"The shared-runtime demo covers: {string.Join(", ", registry.Keys)}.");
        }

        static NodeExecutors()
        {
            BuiltInExecutors.RegisterAll();
        }
    }
}
