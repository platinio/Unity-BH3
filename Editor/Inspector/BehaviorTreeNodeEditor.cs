using Unity.VisualScripting;
using UnityEditor;
using UnityEngine;

namespace ArcaneOnyx.BehaviorTree
{
    [Editor(typeof(BehaviorTreeNode))]
    public class BehaviorTreeNodeEditor : Inspector
    {
        public BehaviorTreeNodeEditor(Metadata metadata) : base(metadata) { }

        protected override float GetHeight(float width, GUIContent label)
        {
            return LudiqGUI.GetInspectorHeight(this, metadata, 250.0f) + EditorGUIUtility.singleLineHeight + EditorGUIUtility.standardVerticalSpacing;
        }

        protected override void OnGUI(Rect position, GUIContent label)
        {
            Vector2 margin = new Vector2(15.0f, 20.0f);
            
            position.size -= margin;
            position.position += margin * 0.5f;
            
            LudiqGUI.Inspector(metadata, position, GUIContent.none);
        }
    }
}