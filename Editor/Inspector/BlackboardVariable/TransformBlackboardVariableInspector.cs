using Unity.VisualScripting;
using UnityEditor;
using UnityEngine;

namespace Platinio.BehaviorTree
{
    [Inspector(typeof(IntBlackboardVariable))]
    public class TransformBlackboardVariableInspector : BlackboardVariableInspector<Transform>
    {
        public TransformBlackboardVariableInspector(Metadata metadata) : base(metadata)
        {
        }

        protected override Transform ValueField(Rect position, string name, Transform value) 
        {
            return (Transform) EditorGUI.ObjectField(position, GUIContent.none, value, typeof(Transform));
        }
    }
}