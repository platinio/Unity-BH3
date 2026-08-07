using ArcaneOnyx.GraphCore;
using Unity.VisualScripting;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace ArcaneOnyx.BehaviorTree
{
    [GraphCreateMenu("Unity/Variables/Set Variable")]
    public class SetVariable : GameplayNode
    {
        [Serialize, Inspectable] private VariableKind VariableKind;
      
        [DoNotSerialize]
        public ValueInput Key { get; private set; }
        
        [DoNotSerialize]
        public ValueInput Value { get; private set; }

        public override string NodeName => "Set Variable";

        protected override void Definition()
        {
            base.Definition();
            
            Key = ValueInput<string>(nameof(Key));
            Value = ValueInput<object>(nameof(Value));
        }
       
        public override ExecutionStatus OnUpdate()
        {
            string key = (string) Key.GetValue();
            object value = Value.GetValue();

            // Same switch as GameplayNode.SaveVariable, which is why this defers to it: two copies of the
            // rule about where a Graph write lands is exactly one copy too many.
            SaveVariable(key, VariableKind, value);

            return ExecutionStatus.Success;
        }
    }
}