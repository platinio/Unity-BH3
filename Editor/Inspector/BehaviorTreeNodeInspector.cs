using Unity.VisualScripting;
using UnityEditor;
using UnityEngine;

namespace ArcaneOnyx.BehaviorTree
{
    [Inspector(typeof(BehaviorTreeNode))]
    public class BehaviorTreeNodeInspector : ReflectedInspector
    {
        public BehaviorTreeNodeInspector(Metadata metadata) : base(metadata) { }
        
        protected override float GetHeight(float width, GUIContent label)
        {
            string description = (string)metadata["Description"].value;
            if (description == string.Empty) return base.GetHeight(width, label);
            
            return base.GetHeight(width, label) + LudiqGUIUtility.GetHelpBoxHeight(description, MessageType.Info, width);
        }

        protected override void OnGUI(Rect position, GUIContent label)
        {
            base.OnGUI(position, label);

            string description = (string)metadata["Description"].value;
            if (description == string.Empty) return;
            
            float h = LudiqGUIUtility.GetHelpBoxHeight(description, MessageType.Info, position.width);
            position.y = position.y + position.height - h;
            position.height = h;
            
            EditorGUI.HelpBox(position, description, MessageType.Info);
        }
    }
}