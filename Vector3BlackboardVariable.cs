using Unity.VisualScripting;
using UnityEngine;

namespace Platinio.BehaviorTree
{
    [Inspectable]
    public class Vector3BlackboardVariable : BlackboardVariable<Vector3>
    {
        [Serialize] [Inspectable]
        private BlackboardVariableType variableType = BlackboardVariableType.Dynamic;
        [Serialize] [Inspectable]
        private string variableName;
        [Serialize] [Inspectable] 
        private Vector3 defaultValue;

        [Serialize] private bool foldout;
        
        
        protected override BlackboardVariableType VariableType => variableType;
        protected override string VariableName => variableName;
        protected override Vector3 DefaultValue => defaultValue;
    }
}

