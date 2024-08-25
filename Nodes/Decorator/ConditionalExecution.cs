using Platinio.GraphCore;
using Unity.VisualScripting;

namespace Platinio.BehaviorTree
{
    public class ConditionalExecution : GameplayNode
    {
        [Serialize] private GameplayNode owner;

        public override string NodeName => "Conditional Execution";

        public GameplayNode Owner => owner;
        
        public ConditionalExecution(GameplayNode owner)
        {
            this.owner = owner;
        }
    }
}

