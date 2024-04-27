using UnityEditor;
using UnityEngine;

namespace Platinio.BehaviorTree
{
    [CustomPropertyDrawer(typeof(GameObjectBlackboardVariable))]
    public class GameObjectBlackboardVariablePropertyDrawer : BlackboardVariablePropertyDrawer<GameObject>
    {
        
    }
}