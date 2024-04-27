using System;


namespace Platinio.BehaviorTree
{
    public class BlackboardKeyType : Attribute
    {
        public Type VariableType;       
        
        public BlackboardKeyType(Type variableType)
        {
            VariableType = variableType;
        }
    }

}

