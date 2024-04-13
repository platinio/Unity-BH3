using Unity.VisualScripting;
using UnityEditor;
using UnityEngine;

namespace Platinio.BehaviorTree
{
    [Inspector(typeof(IntBlackboardVariable))]
    public class IntBlackboardVariableInspector : BlackboardVariableInspector<int>
    {
        public IntBlackboardVariableInspector(Metadata metadata) : base(metadata)
        {
        }

        protected override int ValueField(Rect position, string name, int value) => EditorGUI.IntField(position, GUIContent.none, value);
    }
}