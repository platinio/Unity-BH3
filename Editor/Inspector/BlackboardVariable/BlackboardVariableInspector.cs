using System;
using Unity.VisualScripting;
using UnityEditor;
using UnityEngine;
using ColorUtility = UnityEngine.ColorUtility;

namespace Platinio.BehaviorTree
{
    public abstract class BlackboardVariableInspector<T> : Inspector
    {
        private Vector2 indentOffset = Vector2.right * 15.0f;
        private Vector2 sizeOffset = Vector2.left * 10.0f;
        
        public BlackboardVariableInspector(Metadata metadata) : base(metadata)
        {
           
        }

        protected override float GetHeight(float width, GUIContent label)
        {
            var foldout = Convert.ToBoolean(metadata["foldout"].value);
            if (foldout) return (EditorGUIUtility.singleLineHeight + EditorGUIUtility.standardVerticalSpacing) * 3;
            return EditorGUIUtility.singleLineHeight + EditorGUIUtility.standardVerticalSpacing;
        }

        private void CalculateLabelAndValueRect(ref Rect position, out Rect label, out Rect value)
        {
            float horizontalSpace = 5.0f;
            label = new Rect(position.x, position.y, EditorGUIUtility.labelWidth, EditorGUIUtility.singleLineHeight );
            value = new Rect(position.x + label.width + horizontalSpace, position.y, (position.width - label.width), EditorGUIUtility.singleLineHeight);
            position.y += EditorGUIUtility.singleLineHeight + EditorGUIUtility.standardVerticalSpacing;
        }

        protected override void OnGUI(Rect position, GUIContent label)
        {
            label.text = ConvertToInspectorName(label.text);

            CalculateLabelAndValueRect(ref position, out var labelRect, out var valueRect);
            labelRect.position += indentOffset;
           
            var foldout = Convert.ToBoolean(metadata["foldout"].value);
            bool oldFoldoutValue = foldout;
            foldout = EditorGUI.Foldout(labelRect, foldout, label);

            Vector2 boxPosition = position.position;
            position.position += indentOffset;
            
            if (foldout)
            {
                var selectedVariableType = (BlackboardVariableType)Convert.ToInt32(metadata["variableType"].value);

                Vector2 boxSize = new Vector2(position.width, GetHeight(position.width, label) - EditorGUIUtility.singleLineHeight - EditorGUIUtility.standardVerticalSpacing);
                Rect boxRect = new Rect(boxPosition, boxSize);

                ColorUtility.TryParseHtmlString("#333333", out var color);
                EditorGUI.DrawRect(boxRect, color);
            
                CalculateLabelAndValueRect(ref position, out labelRect, out valueRect);
                valueRect.size -= indentOffset;
                valueRect.size += sizeOffset;
                EditorGUI.LabelField(labelRect, "Type");
                selectedVariableType = (BlackboardVariableType)EditorGUI.EnumPopup(valueRect, selectedVariableType);
                
                var variableName = DrawVariableInspector(ref position, ref labelRect, ref valueRect);

                CalculateLabelAndValueRect(ref position, out labelRect, out valueRect);
                valueRect.size -= indentOffset;
                valueRect.size += sizeOffset;
                
                metadata.RecordUndo();
                metadata["variableType"].value = selectedVariableType;
                metadata["variableName"].value = variableName;
            }
            
            metadata["foldout"].value = foldout;

            if (oldFoldoutValue != foldout)
            {
                SetHeightDirty();
            }

        }

        private string DrawVariableInspector(ref Rect position, ref Rect labelRect, ref Rect valueRect)
        {
            var variableName = Convert.ToString(metadata["variableName"].value);
            var selectedVariableType = (BlackboardVariableType)Convert.ToInt32(metadata["variableType"].value);
            
            CalculateLabelAndValueRect(ref position, out labelRect, out valueRect);
            valueRect.size -= indentOffset;
            valueRect.size += sizeOffset;
            EditorGUI.LabelField(labelRect, "Name");

            if (selectedVariableType == BlackboardVariableType.Value)
            {
                metadata["value"].value = ValueField(valueRect, "value", (T) metadata["value"].value);
            }
            else if (selectedVariableType == BlackboardVariableType.Graph)
            {
                if (!IsVariableNameValidOption())
                {
                    metadata["variableName"].value = string.Empty;
                }

                if (EditorGUI.DropdownButton(valueRect, new GUIContent(variableName), FocusType.Passive))
                {
                    var menu = new GenericMenu();
                    var variablesEnumerator = BehaviorTreeCanvas.GetBehaviorTreeGraphAsset().declarations.GetEnumerator();
                    
                    while (variablesEnumerator.MoveNext())
                    {
                        var current = variablesEnumerator.Current;

                        if (current.value.GetType() != typeof(T)) continue;
                        
                        menu.AddItem(new GUIContent(current.name), variableName == current.name, () =>
                        {
                            metadata["variableName"].value = current.name;
                        });
                    }

                    menu.DropDown(valueRect);
                }
            }
            else
            {
                variableName = EditorGUI.TextField(valueRect, variableName);
            }

            return variableName;
        }

        protected abstract T ValueField(Rect position, string name, T value);
       

        private bool IsVariableNameValidOption()
        {
            var variablesEnumerator = BehaviorTreeCanvas.GetBehaviorTreeGraphAsset().declarations.GetEnumerator();
            string variableName = metadata["variableName"].value as string;
            
            while (variablesEnumerator.MoveNext())
            {
                var current = variablesEnumerator.Current;
                        
                if (current.value.GetType() != typeof(T)) continue;
                        
                if (variableName == current.name) return true;
            }

            return false;
        }


        private string ConvertToInspectorName(string varName)
        {
            string inspectorName = varName.Replace("m_", string.Empty).Replace("M_", string.Empty).Replace("_", string.Empty);
            inspectorName = $"{char.ToUpper(inspectorName[0])}{inspectorName.Substring(1)}";

            return inspectorName;
        }
    }
}

