using UnityEngine;

namespace Platinio.BehaviorTree
{
    public class Vector2BlackboardVariable : BlackboardVariable<Vector2>
    {
        public Vector2BlackboardVariable() { }
        
        public Vector2BlackboardVariable(Vector2 value)
        {
            this.value = value;
        }
    }
}