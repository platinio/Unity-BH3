using System;
using UnityEngine;

namespace ArcaneOnyx.BehaviorTree
{
    [Serializable]
    public class BTScriptGraphVariable : ScriptGraphVariable
    {
        [SerializeField] private string returnType;

        public Type ReturnType => Type.GetType(returnType);
        
        public BTScriptGraphVariable(Type returnType = null)
        {
            this.returnType = returnType == null ? string.Empty : returnType.AssemblyQualifiedName;
        }
    }
}