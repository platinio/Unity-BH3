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

            switch (VariableKind)
            {
                case VariableKind.Graph:
                    BehaviorTreeMachine.GraphAsset.declarations.Set(key, value);
                    break;
                case VariableKind.Object:
                    BehaviorTreeMachine.Variables.declarations.Set(key, value);
                    break;
                case VariableKind.Scene:
                    SceneVariables.Instance(SceneManager.GetActiveScene()).variables.declarations.Set(key, value);
                    break;
                case VariableKind.Application:
                    ApplicationVariables.current.Set(key, value);
                    break;
                case VariableKind.Saved:
                    SavedVariables.current.Set(key, value);
                    break;
                case VariableKind.Flow:
                    Debug.LogError($"BehaviorTree doesnt support Flow VariableKind Node={NodeName} Key={key}");
                    break;
            }

            return ExecutionStatus.Success;
        }
    }
}