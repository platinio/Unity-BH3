using ArcaneOnyx.GraphCore;
using Unity.VisualScripting;
using UnityEngine.SceneManagement;

namespace ArcaneOnyx.BehaviorTree
{
    [GraphCreateMenu("Unity/Variables/Get Variable")]
    public class GetVariable : Literal
    {
        [Serialize, Inspectable] private VariableKind VariableKind;
        
        [DoNotSerialize]
        public ValueInput Key { get; private set; }
        
        [DoNotSerialize]
        public ValueOutput Value { get; private set; }

        public override string NodeName => string.IsNullOrEmpty(VariableName)? "Get Variable" : VariableName;

        protected override void Definition()
        {
            base.Definition();

            Key = ValueInput<string>(nameof(Key));
            Value = ValueOutput<object>(nameof(Value), () => GetValue((string)Key.GetValue(), BehaviorTreeMachine));
        }
        
        public object GetValue(string key, BehaviorTreeMachine machine)
        {
            switch (VariableKind)
            {
                case VariableKind.Graph:
                    return GetValueFromGraph(key, machine);
                case VariableKind.Object:
                    return GetValueFromObject(key, machine);
                case VariableKind.Scene:
                    return GetValueFromScene(key);
                case VariableKind.Application:
                    return GetValueFromApp(key);
                case VariableKind.Saved:
                    return GetValueFromSaved(key);
            }
           
            return default;
        }
       
        private object GetValueFromGraph(string key, BehaviorTreeMachine machine)
        {
            var declarations = machine.GraphInstance.declarations;
            return declarations.Get(key);
        }
        
        private object GetValueFromObject(string key, IGraphMachine machine)
        {
            return machine.Variables.declarations.Get(key);
        }


        private object GetValueFromScene(string key)
        {
            var variables =  SceneVariables.Instance(SceneManager.GetActiveScene());
            return variables.variables.declarations.Get(key);
        }

        private object GetValueFromApp(string key)
        {
            return ApplicationVariables.current.Get(key);
        }

        private object GetValueFromSaved(string key)
        {
            return SavedVariables.current.Get(key);
        }
       
    }
}

