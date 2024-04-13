using Unity.VisualScripting;

namespace Platinio.BehaviorTree
{
    [Inspector(typeof(BooleanBlackboardVariable))]
    public class BooleanBlackboardVariableInspector : BlackboardVariableInspector<bool>
    {
        public BooleanBlackboardVariableInspector(Metadata metadata) : base(metadata)
        {
           
        }
    }
}