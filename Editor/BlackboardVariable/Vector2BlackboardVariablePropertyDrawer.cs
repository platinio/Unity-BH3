using UnityEditor;
using UnityEngine;

namespace ArcaneOnyx.BehaviorTree
{
    [CustomPropertyDrawer(typeof(Vector2BlackboardVariable))]
    public class Vector2BlackboardVariablePropertyDrawer : BlackboardVariablePropertyDrawer<Vector2>
    {
        
    }
}