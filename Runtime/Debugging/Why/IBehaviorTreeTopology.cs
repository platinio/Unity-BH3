using System;
using System.Collections.Generic;

namespace ArcaneOnyx.BehaviorTree.Debugging
{
    /// <summary>
    /// What the tree looks like, for the parts of an explanation a recording cannot supply on its own.
    ///
    /// <para>
    /// A recording says what happened; it does not say what could have happened instead. "This branch never
    /// ran because the Selector above it picked a higher-priority sibling" is a claim about structure, and the
    /// structure is in the asset, not in the buffer. Rather than teach the recorder to write structure into
    /// every event — which would grow the hot path to serve a question only a reader asks — the explainer
    /// takes structure separately.
    /// </para>
    ///
    /// <para>
    /// Optional on purpose. Every sentence the recording alone can justify is produced without a topology;
    /// supplying one adds names instead of guids and the sentences that need siblings. An imported recording
    /// with no tree to hand still explains, just more tersely — which is the honest outcome, not a degraded
    /// one.
    /// </para>
    /// </summary>
    public interface IBehaviorTreeTopology
    {
        /// <summary>The node with this guid, or false when the topology does not cover it.</summary>
        bool TryGetNode(Guid guid, out BehaviorTreeNodeInfo node);

        /// <summary>
        /// The variable keys a guard reads, when they can be determined statically.
        ///
        /// <para>
        /// This is what separates "the last thing that changed before the guard flipped" from "the variable
        /// this guard reads changed". The first is a coincidence the reader has to judge; the second is the
        /// answer. Returning false is fine and common — a guard fed by a script graph with a computed key
        /// cannot be resolved — and the explainer falls back to the weaker wording rather than guessing.
        /// </para>
        /// </summary>
        bool TryGetGuardReads(Guid guardGuid, out IReadOnlyList<string> variableKeys);
    }

    /// <summary>
    /// One node, as the explainer needs it: what to call it, and who is next to it.
    /// </summary>
    public readonly struct BehaviorTreeNodeInfo
    {
        public readonly Guid Guid;

        /// <summary>What a designer calls it — the node's name, or its comment when it has one.</summary>
        public readonly string DisplayName;

        /// <summary>The concrete node type, e.g. <c>Selector</c>. Used to word what a parent did.</summary>
        public readonly string TypeName;

        /// <summary>The node above this one, or <see cref="Guid.Empty"/> at the root of its graph.</summary>
        public readonly Guid ParentGuid;

        /// <summary>
        /// Children in execution order — which for a behavior tree is canvas X order, already sorted by
        /// <c>SortContainerNodesChildren</c> at awake. The index is the priority the designer sees.
        /// </summary>
        public readonly IReadOnlyList<Guid> Children;

        public BehaviorTreeNodeInfo(Guid guid, string displayName, string typeName, Guid parentGuid, IReadOnlyList<Guid> children)
        {
            Guid = guid;
            DisplayName = displayName;
            TypeName = typeName;
            ParentGuid = parentGuid;
            Children = children ?? Array.Empty<Guid>();
        }

        public bool IsValid => Guid != Guid.Empty;
    }
}
