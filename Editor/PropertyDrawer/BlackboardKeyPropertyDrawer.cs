using ArcaneOnyx.EditorTools;
using UnityEditor;
using UnityEngine;

namespace ArcaneOnyx.BehaviorTree
{
    [CustomPropertyDrawer(typeof(BlackboardKey))]
    public class BlackboardKeyPropertyDrawer : PropertyDrawer
    {
        public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
        {
            EditorGUI.BeginProperty(position,label,property);
         
            string variableName = property.FindPropertyRelative("blackboardKeyName").stringValue;
            float yPosition = position.position.y;

            var rect = ArcaneOnyxPropertyDrawer.CalculatePropertyRect(position, ref yPosition);
            ArcaneOnyxPropertyDrawer.CalculateLabelAndValueRect(rect, out Rect labelRect, out Rect valueRect);
            
            EditorGUI.LabelField(labelRect, label.text);

            if (EditorGUI.DropdownButton(valueRect, new GUIContent(GetLabelVariableName(variableName)), FocusType.Passive))
            {
                var menu = new GenericMenu();

                foreach (var variableDeclaration in BehaviorTreePropertyDrawerUtil.GetVariableDeclarations())
                {                   
                    menu.AddItem(new GUIContent(variableDeclaration.name), variableName == variableDeclaration.name, () =>
                    {
                        property.FindPropertyRelative("blackboardKeyName").stringValue = variableDeclaration.name;
                        property.serializedObject.ApplyModifiedProperties();
                    });
                }

                menu.DropDown(position);
            }
            
            EditorGUI.EndProperty();
        }
        
        private string GetLabelVariableName(string variableName)
        {
            if (!IsVariableNameValidOption(variableName)) return $"{variableName} (MISSING!)";
            return variableName;
        }
        
        private bool IsVariableNameValidOption(string variableName)
        {
            foreach (var variableDeclaration in BehaviorTreePropertyDrawerUtil.GetVariableDeclarations())
            {
                if (variableName == variableDeclaration.name) return true;
            }

            return false;
        }

    }
}

