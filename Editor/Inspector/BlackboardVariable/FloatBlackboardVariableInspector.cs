using Unity.VisualScripting;
using UnityEditor;
using UnityEngine;

namespace Platinio.BehaviorTree
{
    [Inspector(typeof(FloatBlackboardVariable))]
    public class FloatBlackboardVariableInspector : BlackboardVariableInspector<float>
    {
        public FloatBlackboardVariableInspector(Metadata metadata) : base(metadata)
        {
        }

        protected override float ValueField(Rect position, string name, float value) => EditorGUI.FloatField(position, GUIContent.none, value);
    }
}