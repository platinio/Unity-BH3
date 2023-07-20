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
        Object,
        Scene,
        App,
        Saved
    }

    public class BlackboardVariable<T>
    {
        [Serialize] [Inspectable]
        private BlackboardVariableType m_variableType = BlackboardVariableType.Dynamic;
        [Serialize] [Inspectable]
        private string m_variableName;
        [Serialize] [Inspectable] 
        private T m_defaultValue;

        [Serialize] private bool m_foldout;
        
        public T GetValue(IGraphMachine machine)
        {
            T result = m_defaultValue;
            
            switch (m_variableType)
            {
                case BlackboardVariableType.Dynamic:
                    TryGetValueDynamic(machine, out result);
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
        
        private bool TryGetValue(BlackboardVariableType variableType, IGraphMachine machine, out T value)
        {
            value = m_defaultValue;
            switch (variableType)
            {
                case BlackboardVariableType.Dynamic:
                    break;
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

        private bool TryGetValueDynamic(IGraphMachine machine, out T value)
        {
            value = m_defaultValue;
            var variableTypes = (BlackboardVariableType[])Enum.GetValues(typeof(BlackboardVariableType));

            foreach (var variableType in variableTypes)
            {
                if (variableType == BlackboardVariableType.Dynamic) continue;

                if (TryGetValue(variableType, machine, out value)) return true;
            }

            return false;
        }

        private bool TryGetValueFromObject(IGraphMachine machine, out T value)
        {
            value = m_defaultValue;
            
            if (machine == null)
            {
                Debug.LogError("BehaviorTreeMachine is null");
                return false;
            }

            if (!machine.VariableDeclarations.IsDefined(m_variableName)) return false;


            value = machine.VariableDeclarations.Get<T>(m_variableName);
            return true;
        }


        private bool TryGetValueFromScene(out T value)
        {
            value = m_defaultValue;
            
            var variables =  SceneVariables.Instance(SceneManager.GetActiveScene());
            if (!variables.variables.declarations.IsDefined(m_variableName)) return false;
            
            value = variables.variables.declarations.Get<T>(m_variableName);
            return true;
        }

        private bool TryGetValueFromApp(out T value)
        {
            value = m_defaultValue;

            if (!ApplicationVariables.current.IsDefined(m_variableName)) return false;
            
            value = ApplicationVariables.current.Get<T>(m_variableName);
            return true;
        }

        private bool TryGetValueFromSave(out T value)
        {
            value = m_defaultValue;

            if (!SavedVariables.current.IsDefined(m_variableName)) return false;
            
            value = SavedVariables.current.Get<T>(m_variableName);
            return true;
        }
    }
}