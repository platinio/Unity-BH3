using System;
using ArcaneOnyx.GraphCore;
using Unity.VisualScripting;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace ArcaneOnyx.BehaviorTree
{
    [Serializable]
    public abstract class BlackboardVariable<T>
    {
        [SerializeField] private bool inlineValue = true;
        [SerializeField] private string variableName;
        [SerializeField] protected T value;

        public string VariableName => variableName;
        
        public T GetValue(BehaviorTreeMachine machine)
        {
            if (inlineValue) return value;
            if (string.IsNullOrEmpty(variableName)) return default;
            
            T result;            

            if (TryGetValueFromObject(machine, out result)) return result;
            if (TryGetValueFromGraph(machine, out result)) return result;
            if (TryGetValueFromScene(out result)) return result;
            if (TryGetValueFromApp(out result)) return result;
            if (TryGetValueFromSave(out result)) return result;

            return default;
        }
        
        public bool TryGetValue(BehaviorTreeMachine machine, out T result)
        {
            if (inlineValue)
            {
                result = value;
                return true;
            }

            result = default;
            if (string.IsNullOrEmpty(variableName)) return false;
            
            if (TryGetValueFromObject(machine, out result)) return true;
            if (TryGetValueFromGraph(machine, out result)) return true;
            if (TryGetValueFromScene(out result)) return true;
            if (TryGetValueFromApp(out result)) return true;
            if (TryGetValueFromSave(out result)) return true;

            return false;
        }

        private bool TryGetValueFromGraph(BehaviorTreeMachine machine, out T value)
        {
            value = default;
            var declarations = machine.GraphInstance.declarations;

            if (!declarations.IsDefined(variableName)) return false;
            
            value = declarations.Get<T>(variableName);
            return true;
        }
        
        private bool TryGetValueFromObject(IGraphMachine machine, out T value)
        {
            value = default;
            
            if (machine == null)
            {
                Debug.LogError("BehaviorTreeMachine is null");
                return false;
            }

            if (!machine.Variables.declarations.IsDefined(variableName)) return false;


            value = machine.Variables.declarations.Get<T>(variableName);
            return true;
        }


        private bool TryGetValueFromScene(out T value)
        {
            value = default;
            
            var variables =  SceneVariables.Instance(SceneManager.GetActiveScene());
            if (!variables.variables.declarations.IsDefined(variableName)) return false;
            
            value = variables.variables.declarations.Get<T>(variableName);
            return true;
        }

        private bool TryGetValueFromApp(out T value)
        {
            value = default;

            if (!ApplicationVariables.current.IsDefined(variableName)) return false;
            
            value = ApplicationVariables.current.Get<T>(variableName);
            return true;
        }

        private bool TryGetValueFromSave(out T value)
        {
            value = default;

            if (!SavedVariables.current.IsDefined(variableName)) return false;
            
            value = SavedVariables.current.Get<T>(variableName);
            return true;
        }
    }
}