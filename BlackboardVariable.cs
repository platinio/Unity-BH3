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
    
    public abstract class BlackboardVariable<T>
    {
        protected abstract BlackboardVariableType VariableType { get; }
        protected abstract string VariableName { get; }
        protected abstract Vector3 DefaultValue { get; }


        public Vector3 GetValue(BehaviorTreeMachine machine)
        {
            Vector3 result = DefaultValue;
            
            switch (VariableType)
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
            
            value = DefaultValue;
            
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
            value = DefaultValue;
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
            value = DefaultValue;
            var declarations = machine.GraphInstance.declarations;

            if (!declarations.IsDefined(VariableName)) return false;
            
            value = declarations.Get<Vector3>(VariableName);
            return true;
        }
        
        private bool TryGetValueFromObject(IGraphMachine machine, out Vector3 value)
        {
            value = DefaultValue;
            
            if (machine == null)
            {
                Debug.LogError("BehaviorTreeMachine is null");
                return false;
            }

            if (!machine.Variables.declarations.IsDefined(VariableName)) return false;


            value = machine.Variables.declarations.Get<Vector3>(VariableName);
            return true;
        }


        private bool TryGetValueFromScene(out Vector3 value)
        {
            value = DefaultValue;
            
            var variables =  SceneVariables.Instance(SceneManager.GetActiveScene());
            if (!variables.variables.declarations.IsDefined(VariableName)) return false;
            
            value = variables.variables.declarations.Get<Vector3>(VariableName);
            return true;
        }

        private bool TryGetValueFromApp(out Vector3 value)
        {
            value = DefaultValue;

            if (!ApplicationVariables.current.IsDefined(VariableName)) return false;
            
            value = ApplicationVariables.current.Get<Vector3>(VariableName);
            return true;
        }

        private bool TryGetValueFromSave(out Vector3 value)
        {
            value = DefaultValue;

            if (!SavedVariables.current.IsDefined(VariableName)) return false;
            
            value = SavedVariables.current.Get<Vector3>(VariableName);
            return true;
        }
    }
}