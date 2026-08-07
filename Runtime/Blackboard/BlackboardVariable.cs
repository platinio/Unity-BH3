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
        
        public T GetValue(BehaviorTreeMachine machine, BehaviorTreeVariableScope scope = null)
        {
            if (inlineValue) return value;
            if (string.IsNullOrEmpty(variableName)) return default;
            
            T result;            

            if (TryGetValueFromObject(machine, out result)) return result;
            if (TryGetValueFromGraph(machine, scope, out result)) return result;
            if (TryGetValueFromScene(out result)) return result;
            if (TryGetValueFromApp(out result)) return result;
            if (TryGetValueFromSave(out result)) return result;

            return default;
        }
        
        public bool TryGetValue(BehaviorTreeMachine machine, out T result)
            => TryGetValue(machine, null, out result);

        public bool TryGetValue(BehaviorTreeMachine machine, BehaviorTreeVariableScope scope, out T result)
        {
            if (inlineValue)
            {
                result = value;
                return true;
            }

            result = default;
            if (string.IsNullOrEmpty(variableName)) return false;
            
            if (TryGetValueFromObject(machine, out result)) return true;
            if (TryGetValueFromGraph(machine, scope, out result)) return true;
            if (TryGetValueFromScene(out result)) return true;
            if (TryGetValueFromApp(out result)) return true;
            if (TryGetValueFromSave(out result)) return true;

            return false;
        }

        /// <summary>
        /// Resolves through the reading node's scope when it has one — the tree the node lives in first, then
        /// outward — so a branch sees its own parameter rather than a same-named value elsewhere on the agent.
        /// Falls back to the root instance for callers that have no scope to offer.
        /// </summary>
        private bool TryGetValueFromGraph(BehaviorTreeMachine machine, BehaviorTreeVariableScope scope, out T value)
        {
            value = default;

            if (scope != null)
            {
                if (!scope.TryGet(variableName, out var scoped) || scoped is not T typed) return false;

                value = typed;
                return true;
            }

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