using Unity.VisualScripting;
using UnityEditor;
using UnityEngine;

namespace Platinio.BehaviorTree
{
    [Inspector(typeof(BooleanBlackboardVariable))]
    public class BooleanBlackboardVariableInspector : BlackboardVariableInspector<bool>
    {
        public BooleanBlackboardVariableInspector(Metadata metadata) : base(metadata)
        {
           
        }

        protected override bool ValueField(Rect position, string name, bool value)
        {
            return EditorGUI.Toggle(position, GUIContent.none, value);
        }
    }
}