using UnityEditor;
using UnityEngine;

namespace ArcaneOnyx.BehaviorTree
{
    [CustomPropertyDrawer(typeof(GameObjectBlackboardVariable))]
    public class GameObjectBlackboardVariablePropertyDrawer : BlackboardVariablePropertyDrawer<GameObject>
    {
        
    }
}