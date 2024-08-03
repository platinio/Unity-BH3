using Platinio.GraphCore;
using Unity.VisualScripting;

namespace Platinio.BehaviorTree
{
    [GraphCreateMenu("Value Output/Variable/Get Float")]
    public class GetFloatVariable : GameplayNode
    {
        [Serialize] [Inspectable] private FloatBlackboardVariable variable;
        
        
        [DoNotSerialize]
        public ValueOutput Value { get; private set; }
        
        public override string NodeName
        {
            get
            {
                if (string.IsNullOrEmpty(variable.VariableName)) return "Get Variable";
                return $"Get {variable.VariableName}";
            }
        }
        
        protected override void Definition()
        {
            base.Definition();
            Value = ValueOutput<float>(nameof(Value), () => variable.GetValue(BehaviorTreeMachine));
        }
       
    }
}

