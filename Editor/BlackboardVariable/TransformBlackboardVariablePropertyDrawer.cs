using UnityEditor;
using UnityEngine;

namespace Platinio.BehaviorTree
{
    [CustomPropertyDrawer(typeof(TransformBlackboardVariable))]
    public class TransformBlackboardVariablePropertyDrawer : BlackboardVariablePropertyDrawer<Transform>
    {
        
    }
}