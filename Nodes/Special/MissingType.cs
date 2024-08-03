using Unity.VisualScripting;

namespace Platinio.BehaviorTree
{
    public sealed class MissingType : BehaviorTreeNode
    {
        public override string NodeName => "MISSING TYPE!";
        
        [Serialize]
        public string formerType { get; private set; } // Private set is required by the deserializer.

        [Serialize]
        public string formerValue { get; private set; }

        // Although this unit will have no ports, the already existing graph
        // connections will create invalid ones to connect themselves to.
        protected override void Definition() { }
    }
}

