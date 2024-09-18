using UnityEditor;
using UnityEngine;

namespace ArcaneOnyx.BehaviorTree
{
    [CustomPropertyDrawer(typeof(TransformBlackboardVariable))]
    public class TransformBlackboardVariablePropertyDrawer : BlackboardVariablePropertyDrawer<Transform>
    {
        
    }
}