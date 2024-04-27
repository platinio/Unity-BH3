using UnityEditor;

namespace Platinio.BehaviorTree
{
    [CustomPropertyDrawer(typeof(IntBlackboardVariable))]
    public class IntBlackboardVariablePropertyDrawer : BlackboardVariablePropertyDrawer<int>
    {
       
    }
}

