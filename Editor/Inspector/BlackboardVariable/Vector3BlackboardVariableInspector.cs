using Unity.VisualScripting;
using UnityEditor;
using UnityEngine;

namespace Platinio.BehaviorTree
{
    [Inspector(typeof(Vector3BlackboardVariable))]
    public class Vector3BlackboardVariableInspector : BlackboardVariableInspector<Vector3>
    {
        public Vector3BlackboardVariableInspector(Metadata metadata) : base(metadata)
        {
        }

        protected override Vector3 ValueField(Rect position, string name, Vector3 value)
        {
            return EditorGUI.Vector3Field(position, GUIContent.none, value);
        }
    }
}