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
    }
}

