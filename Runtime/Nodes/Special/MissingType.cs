using Unity.VisualScripting;

namespace ArcaneOnyx.BehaviorTree
{
    public sealed class MissingType : BehaviorTreeNode
    {
        public override string NodeName => "MISSING TYPE!";
        
        [Serialize]
        public string formerType { get; private set; } // Private set is required by the deserializer.

        [Serialize]
        public string formerValue { get; private set; }

        public override string Description => "The type of this node was remove, remove this node and create a new one";

        // Although this unit will have no ports, the already existing graph
        // connections will create invalid ones to connect themselves to.
        protected override void Definition() { }

        /// <summary>
        /// A node standing in for a type that no longer exists is wrong in every configuration, so it says
        /// so on the canvas rather than relying on its name being read. This is how the instances left in
        /// a tree by a deleted node type -- RunScriptGraph was the first -- get found.
        /// </summary>
        public override void CollectProblems(System.Collections.Generic.List<NodeProblem> into)
        {
            base.CollectProblems(into);

            into.Add(new NodeProblem(NodeProblemSeverity.Error,
                string.IsNullOrEmpty(formerType)
                    ? "This node's type no longer exists, so it does nothing."
                    : $"This node's type ('{formerType}') no longer exists, so it does nothing.",
                "Delete it and rebuild what it did with a node that exists."));
        }
    }
}

