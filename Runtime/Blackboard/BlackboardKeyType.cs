using System;


namespace ArcaneOnyx.BehaviorTree
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

