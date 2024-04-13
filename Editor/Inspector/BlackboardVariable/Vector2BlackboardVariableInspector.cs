using Unity.VisualScripting;
using UnityEditor;
using UnityEngine;

namespace Platinio.BehaviorTree
{
    [Inspector(typeof(Vector2BlackboardVariable))]
    public class Vector2BlackboardVariableInspector : BlackboardVariableInspector<Vector2>
    {
        public Vector2BlackboardVariableInspector(Metadata metadata) : base(metadata)
        {
        }

        protected override Vector2 ValueField(Rect position, string name, Vector2 value) => EditorGUI.Vector2Field(position, GUIContent.none, value);
    }
}