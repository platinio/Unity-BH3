using System;
using Platinio.GraphCore;
using Unity.VisualScripting;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Platinio.BehaviorTree
{
    public enum BlackboardVariableType
    {
        Dynamic,
        Graph,
        Object,
        Scene,
        App,
        Saved
    }
    
    [Serializable]
    public abstract class BlackboardVariable<T>
    {
        [SerializeField]
        private BlackboardVariableType variableType = BlackboardVariableType.Dynamic;
        [SerializeField]
        private string variableName;
        [SerializeField]
        private Vector3 defaultValue;

        [SerializeField] private bool foldout;


        public Vector3 GetValue(BehaviorTreeMachine machine)
        {
            Vector3 result = defaultValue;
            
            switch (variableType)
            {
                case BlackboardVariableType.Dynamic:
                    TryGetValueDynamic(machine, out result);
                    break;
                case BlackboardVariableType.Graph:
                    TryGetValueFromGraph(machine, out result);
                    break;
                case BlackboardVariableType.Object:
                    TryGetValueFromObject(machine, out result);
                    break;
                case BlackboardVariableType.Scene:
                    TryGetValueFromScene(out result);
                    break;
                case BlackboardVariableType.App:
                    TryGetValueFromApp(out result);
                    break;
                case BlackboardVariableType.Saved:
                    TryGetValueFromSave(out result);
                    break;
            }

            return result;
        }
        
        private bool TryGetValue(BlackboardVariableType variableType, BehaviorTreeMachine machine, out Vector3 value)
        {
            
            value = defaultValue;
            
            switch (variableType)
            {
                case BlackboardVariableType.Dynamic:
                    break;
                case BlackboardVariableType.Graph:
                    return TryGetValueFromGraph(machine, out value);
                case BlackboardVariableType.Object:
                    return TryGetValueFromObject(machine, out value);
                case BlackboardVariableType.Scene:
                    return TryGetValueFromScene(out value);
                case BlackboardVariableType.App:
                    return TryGetValueFromApp(out value);
                case BlackboardVariableType.Saved:
                    return TryGetValueFromSave(out value);
            }

            return false;
        }

        private bool TryGetValueDynamic(BehaviorTreeMachine machine, out Vector3 value)
        {
            value = defaultValue;
            var variableTypes = (BlackboardVariableType[])Enum.GetValues(typeof(BlackboardVariableType));

            foreach (var variableType in variableTypes)
            {
                if (variableType == BlackboardVariableType.Dynamic) continue;

                if (TryGetValue(variableType, machine, out value)) return true;
            }

            return false;
        }

        private bool TryGetValueFromGraph(BehaviorTreeMachine machine, out Vector3 value)
        {
            value = defaultValue;
            var declarations = machine.GraphInstance.declarations;

            if (!declarations.IsDefined(variableName)) return false;
            
            value = declarations.Get<Vector3>(variableName);
            return true;
        }
        
        private bool TryGetValueFromObject(IGraphMachine machine, out Vector3 value)
        {
            value = defaultValue;
            
            if (machine == null)
            {
                Debug.LogError("BehaviorTreeMachine is null");
                return false;
            }

            if (!machine.Variables.declarations.IsDefined(variableName)) return false;


            value = machine.Variables.declarations.Get<Vector3>(variableName);
            return true;
        }


        private bool TryGetValueFromScene(out Vector3 value)
        {
            value = defaultValue;
            
            var variables =  SceneVariables.Instance(SceneManager.GetActiveScene());
            if (!variables.variables.declarations.IsDefined(variableName)) return false;
            
            value = variables.variables.declarations.Get<Vector3>(variableName);
            return true;
        }

        private bool TryGetValueFromApp(out Vector3 value)
        {
            value = defaultValue;

            if (!ApplicationVariables.current.IsDefined(variableName)) return false;
            
            value = ApplicationVariables.current.Get<Vector3>(variableName);
            return true;
        }

        private bool TryGetValueFromSave(out Vector3 value)
        {
            value = defaultValue;

            if (!SavedVariables.current.IsDefined(variableName)) return false;
            
            value = SavedVariables.current.Get<Vector3>(variableName);
            return true;
        }
    }
}