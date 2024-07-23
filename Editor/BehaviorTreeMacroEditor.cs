using Unity.VisualScripting;
using UnityEditor;
using UnityEngine;

namespace Platinio.BehaviorTree
{
    [Editor(typeof(BehaviorTreeGraphAsset))]
    public class BehaviorTreeMacroEditor : Inspector
    {
        public BehaviorTreeMacroEditor(Metadata metadata) : base(metadata) { }

        protected override float GetHeight(float width, GUIContent label)
        {
            var height = 0f;
            height += EditorGUIUtility.standardVerticalSpacing;
            height += GetButtonHeight(width);
            height += EditorGUIUtility.standardVerticalSpacing;
            return height;
        }

        protected override void OnGUI(Rect position, GUIContent label)
        {
            position = BeginLabeledBlock(metadata, position, GUIContent.none);

            y += EditorGUIUtility.standardVerticalSpacing;

            var buttonPosition = new Rect
            (
                position.x,
                y,
                position.width,
                GetButtonHeight(position.width)
            );

            OnButtonGUI(buttonPosition);

            y += buttonPosition.height;

            EndBlock(metadata);
        }

        private float GetButtonHeight(float width)
        {
            return EditorGUIUtility.singleLineHeight + 3;
        }

        private void OnButtonGUI(Rect sourcePosition)
        {
            if (GUI.Button(sourcePosition, "Edit Graph"))
            {
                GraphCore.GraphWindow.OpenActive<BehaviorTreeGraphWindow>(GraphReference.New((IMacro)metadata.value, true));
            }
        }
    }
}