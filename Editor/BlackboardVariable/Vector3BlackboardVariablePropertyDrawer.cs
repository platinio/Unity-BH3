using UnityEditor;
using UnityEngine;

namespace Platinio.BehaviorTree
{
    [CustomPropertyDrawer(typeof(Vector3BlackboardVariable))]
    public class Vector3BlackboardVariablePropertyDrawer : BlackboardVariablePropertyDrawer<Vector3>
    {
        
    }
}

