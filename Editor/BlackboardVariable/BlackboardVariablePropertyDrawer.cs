using UnityEditor;
using UnityEngine;

namespace Platinio.BehaviorTree
{
    public abstract class BlackboardVariablePropertyDrawer<T> : PropertyDrawer
    {
        public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
        {
            return PlatinioPropertyDrawer.LineHeight;
        }

        public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
        {
            EditorGUI.BeginProperty(position, label, property);
            
            float yPosition = position.y;

            bool inlineValue = property.FindPropertyRelative("inlineValue").boolValue;

            if (inlineValue)
            {
                float buttonRectPercent = 0.04f;
                DrawToggleButton(position, property, buttonRectPercent);
                
                position.width = position.width * (1.0f - buttonRectPercent);
                PlatinioPropertyDrawer.PropertyField(position, property.FindPropertyRelative("value"), label.text, ref yPosition);
            }
            else
            {
                DrawDropdownVariableOptions(position, property, label.text, ref yPosition);
            }

            EditorGUI.EndProperty();
        }

        private void DrawToggleButton(Rect position, SerializedProperty property, float buttonSizePercent)
        {
            Rect buttonRect = position;
            buttonRect.height = PlatinioPropertyDrawer.LineHeight;
            buttonRect.width = position.width * buttonSizePercent;
            Vector2 newPosition = buttonRect.position;
            newPosition.x += position.width * (1.0f - buttonSizePercent);
            buttonRect.position = newPosition;
            
            if (GUI.Button(buttonRect, "•"))
            {
                property.FindPropertyRelative("inlineValue").boolValue = !property.FindPropertyRelative("inlineValue").boolValue;
            }
        }

        private void DrawDropdownVariableOptions(Rect position, SerializedProperty property, string propertyName, ref float yPosition)
        {
            var variableName = property.FindPropertyRelative("variableName").stringValue;

            var rect = PlatinioPropertyDrawer.CalculatePropertyRect(position, ref yPosition);
            PlatinioPropertyDrawer.CalculateLabelAndValueRect(rect, out Rect label, out Rect value);
            
            EditorGUI.LabelField(label, propertyName);

            float buttonRectPercent = 0.08f;
            
            DrawToggleButton(value, property, buttonRectPercent);
            value.width = value.width * (1.0f - buttonRectPercent);

            if (EditorGUI.DropdownButton(value, new GUIContent(GetLabelVariableName(variableName)), FocusType.Passive))
            {
                var menu = new GenericMenu();

                foreach (var variableDeclaration in BehaviorTreePropertyDrawerUtil.GetVariableDeclarations(typeof(T)))
                {                   
                    menu.AddItem(new GUIContent(variableDeclaration.name), variableName == variableDeclaration.name, () =>
                    {
                        property.FindPropertyRelative("variableName").stringValue = variableDeclaration.name;
                        property.serializedObject.ApplyModifiedProperties();
                    });
                }

                menu.DropDown(position);
            }
        }
        
        private string GetLabelVariableName(string variableName)
        {
            if (!IsVariableNameValidOption(variableName)) return $"{variableName} (MISSING!)";
            return variableName;
        }
        
        private bool IsVariableNameValidOption(string variableName)
        {
            foreach (var variableDeclaration in BehaviorTreePropertyDrawerUtil.GetVariableDeclarations(typeof(T)))
            {
                if (variableName == variableDeclaration.name) return true;
            }

            return false;
        }
    }
}