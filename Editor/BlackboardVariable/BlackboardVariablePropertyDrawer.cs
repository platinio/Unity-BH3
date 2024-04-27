using Unity.VisualScripting;
using UnityEditor;
using UnityEngine;

namespace Platinio.BehaviorTree
{
    public abstract class BlackboardVariablePropertyDrawer<T> : PropertyDrawer
    {
        private const int OpenLineHeight = 3;
        private const int CloseLineHeight = 1;
        private const float WidthOffset = 10.0f;

        public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
        {
            float fullPropertyHeight = PlatinioPropertyDrawer.LineHeight * OpenLineHeight;
            float closePropertyHeight = PlatinioPropertyDrawer.LineHeight * CloseLineHeight;
            
            var foldoutProperty = property.FindPropertyRelative("foldout");
           
            if (foldoutProperty.boolValue) return fullPropertyHeight;
            return closePropertyHeight;
        }

        public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
        {
            //for some reason the foldout arrow is being draw outside the inspector window
            //so I move it to the right
            position.x += WidthOffset;
            position.width -= WidthOffset;
        
            EditorGUI.BeginProperty(position, label, property);

            float yPosition = position.y;
            var rect = PlatinioPropertyDrawer.CalculatePropertyRect(position, ref yPosition);
            bool foldout = PlatinioPropertyDrawer.FoldoutProperty(rect, property.FindPropertyRelative("foldout"), label.text);

            var indent = EditorGUI.indentLevel;

            if (foldout)
            {
                DrawVariableInspector(position, property, ref yPosition);
            }

            EditorGUI.indentLevel = indent;
            EditorGUI.EndProperty();
        }

        private void DrawVariableInspector(Rect position, SerializedProperty property, ref float yPosition)
        {
            EditorGUI.indentLevel = 1;
            PlatinioPropertyDrawer.PropertyField(position, property.FindPropertyRelative("variableType"), ref yPosition);

            var variableType = (BlackboardVariableType)property.FindPropertyRelative("variableType").enumValueIndex;
            if (variableType == BlackboardVariableType.Value)
            {
                PlatinioPropertyDrawer.PropertyField(position, property.FindPropertyRelative("value"), ref yPosition);
            }
            else if (variableType == BlackboardVariableType.Graph || variableType == BlackboardVariableType.Saved || variableType == BlackboardVariableType.App)
            {
                DrawDropdownVariableOptions(position, property, ref yPosition);
            }
            else
            {
                PlatinioPropertyDrawer.PropertyField(position, property.FindPropertyRelative("variableName"), ref yPosition);
            }
        }


        private void DrawDropdownVariableOptions(Rect position, SerializedProperty property, ref float yPosition)
        {
            var variableName = property.FindPropertyRelative("variableName").stringValue;
            var variableType = (BlackboardVariableType)property.FindPropertyRelative("variableType").enumValueIndex;

            var rect = PlatinioPropertyDrawer.CalculatePropertyRect(position, ref yPosition);
            PlatinioPropertyDrawer.CalculateLabelAndValueRect(rect, out Rect label, out Rect value);
            
            EditorGUI.LabelField(label, "Variable Name");
            if (EditorGUI.DropdownButton(value, new GUIContent(GetLabelVariableName(variableType, variableName)), FocusType.Passive))
            {
                var menu = new GenericMenu();
                var variablesEnumerator = GetVariableDeclarations(variableType).GetEnumerator();
                    
                while (variablesEnumerator.MoveNext())
                {
                    var current = variablesEnumerator.Current;
                    if (current.typeHandle.Resolve() != typeof(T)) continue;
                        
                    menu.AddItem(new GUIContent(current.name), variableName == current.name, () =>
                    {
                        property.FindPropertyRelative("variableName").stringValue = current.name;
                        property.serializedObject.ApplyModifiedProperties();
                    });
                }

                menu.DropDown(position);
            }
        }
        
        private string GetLabelVariableName(BlackboardVariableType variableType, string variableName)
        {
            if (!IsVariableNameValidOption(variableType, variableName)) return $"{variableName} (MISSING!)";
            return variableName;
        }
        
        private bool IsVariableNameValidOption(BlackboardVariableType variableType, string variableName)
        {
            var variablesEnumerator = GetVariableDeclarations(variableType).GetEnumerator();

            while (variablesEnumerator.MoveNext())
            {
                var current = variablesEnumerator.Current;
                if (current.typeHandle.Resolve() != typeof(T)) continue;
                        
                if (variableName == current.name) return true;
            }

            return false;
        }
        
        private VariableDeclarations GetVariableDeclarations(BlackboardVariableType variableType)
        {
            switch (variableType)
            {
                case BlackboardVariableType.Graph:
                    var graphAsset = BehaviorTreeCanvas.GetBehaviorTreeGraphAsset();
                    if (graphAsset == null)
                    {
                        Debug.LogError("Graph asset is null!");
                        return null;
                    }
                    
                    return graphAsset.declarations;
                case BlackboardVariableType.App:
                    return Variables.Application;
                case BlackboardVariableType.Saved:
                    return Variables.Saved;
            }

            return null;
        }
        
    }
}