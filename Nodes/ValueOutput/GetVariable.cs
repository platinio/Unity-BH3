using Platinio.GraphCore;
using Unity.VisualScripting;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Platinio.BehaviorTree
{
    [GraphCreateMenu("Unity/Variables/Get/Variable")]
    public class GetVariable : GameplayNode
    {
        [DoNotSerialize]
        public ValueInput Key { get; private set; }
        
        [DoNotSerialize]
        public ValueOutput Value { get; private set; }

        public override string NodeName => "Get Variable";
        
        protected override void Definition()
        {
            base.Definition();

            Key = ValueInput<string>(nameof(Key));
            Value = ValueOutput<float>(nameof(Value), () => GetValue((string)Key.GetValue(), BehaviorTreeMachine));
        }
        
        public object GetValue(string key, BehaviorTreeMachine machine)
        {
            object result;            

            if (TryGetValueFromObject(key, machine, out result)) return result;
            if (TryGetValueFromGraph(key, machine, out result)) return result;
            if (TryGetValueFromScene(key, out result)) return result;
            if (TryGetValueFromApp(key, out result)) return result;
            if (TryGetValueFromSave(key, out result)) return result;

            return default;
        }
       
        private bool TryGetValueFromGraph(string key, BehaviorTreeMachine machine, out object value)
        {
            value = default;
            var declarations = machine.GraphInstance.declarations;

            if (!declarations.IsDefined(key)) return false;
            
            value = declarations.Get(key);
            return true;
        }
        
        private bool TryGetValueFromObject(string key, IGraphMachine machine, out object value)
        {
            value = default;
            
            if (machine == null)
            {
                Debug.LogError("BehaviorTreeMachine is null");
                return false;
            }

            if (!machine.Variables.declarations.IsDefined(key)) return false;


            value = machine.Variables.declarations.Get(key);
            return true;
        }


        private bool TryGetValueFromScene(string key, out object value)
        {
            value = default;
            
            var variables =  SceneVariables.Instance(SceneManager.GetActiveScene());
            if (!variables.variables.declarations.IsDefined(key)) return false;
            
            value = variables.variables.declarations.Get(key);
            return true;
        }

        private bool TryGetValueFromApp(string key, out object value)
        {
            value = default;

            if (!ApplicationVariables.current.IsDefined(key)) return false;
            
            value = ApplicationVariables.current.Get(key);
            return true;
        }

        private bool TryGetValueFromSave(string key, out object value)
        {
            value = default;

            if (!SavedVariables.current.IsDefined(key)) return false;
            
            value = SavedVariables.current.Get(key);
            return true;
        }
       
    }
}

