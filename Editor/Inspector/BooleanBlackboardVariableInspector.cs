using System;
using Unity.VisualScripting;
using UnityEditor;
using UnityEngine;
using ColorUtility = UnityEngine.ColorUtility;

namespace Platinio.BehaviorTree
{
    [Inspector(typeof(BooleanBlackboardVariable))]
    public class BooleanBlackboardVariableInspector : Inspector
    {
        public BooleanBlackboardVariableInspector(Metadata metadata) : base(metadata)
        {
           
        }

        protected override float GetHeight(float width, GUIContent label)
        {
            var foldout = Convert.ToBoolean(metadata["m_foldout"].value);
            if (foldout) return (EditorGUIUtility.singleLineHeight + EditorGUIUtility.standardVerticalSpacing) * 4;
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
            Vector2 indentOffset = Vector2.right * 15.0f;
            
            CalculateLabelAndValueRect(ref position, out var labelRect, out var valueRect);
            labelRect.position += indentOffset;
           
            var foldout = Convert.ToBoolean(metadata["m_foldout"].value);
            bool oldFoldoutValue = foldout;
            foldout = EditorGUI.Foldout(labelRect, foldout, label);

            Vector2 boxPosition = position.position;
            position.position += indentOffset;
            
            if (foldout)
            {
                Vector2 sizeOffset = Vector2.left * 10.0f;
                var selectedVariableType = (BlackboardVariableType)Convert.ToInt32(metadata["m_variableType"].value);

                Vector2 boxSize = new Vector2(position.width, GetHeight(position.width, label) - EditorGUIUtility.singleLineHeight - EditorGUIUtility.standardVerticalSpacing);
                Rect boxRect = new Rect(boxPosition, boxSize);

                ColorUtility.TryParseHtmlString("#333333", out var color);
                EditorGUI.DrawRect(boxRect, color);
            
                CalculateLabelAndValueRect(ref position, out labelRect, out valueRect);
                valueRect.size -= indentOffset;
                valueRect.size += sizeOffset;
                EditorGUI.LabelField(labelRect, "Type");
                selectedVariableType = (BlackboardVariableType)EditorGUI.EnumPopup(valueRect, selectedVariableType);


                var variableName = Convert.ToString(metadata["m_variableName"].value);
            
                CalculateLabelAndValueRect(ref position, out labelRect, out valueRect);
                valueRect.size -= indentOffset;
                valueRect.size += sizeOffset;
                EditorGUI.LabelField(labelRect, "Name");
                variableName = EditorGUI.TextField(valueRect, variableName);
            
                var variableDefaultValue = Convert.ToBoolean(metadata["m_defaultValue"].value);
            
                CalculateLabelAndValueRect(ref position, out labelRect, out valueRect);
                valueRect.size -= indentOffset;
                valueRect.size += sizeOffset;
                EditorGUI.LabelField(labelRect, "Default Value");
                variableDefaultValue = EditorGUI.Toggle(valueRect, variableDefaultValue);

                metadata.RecordUndo();
                metadata["m_variableType"].value = selectedVariableType;
                metadata["m_variableName"].value = variableName;
                metadata["m_defaultValue"].value = variableDefaultValue;   
                
            }
            
            metadata["m_foldout"].value = foldout;

            if (oldFoldoutValue != foldout)
            {
                SetHeightDirty();
            }

        }
        
        private string ConvertToInspectorName(string varName)
        {
            string inspectorName = varName.Replace("m_", string.Empty).Replace("M_", string.Empty).Replace("_", string.Empty);
            inspectorName = $"{char.ToUpper(inspectorName[0])}{inspectorName.Substring(1)}";
          
            for (int n = inspectorName.Length - 1; n > 0; n--)
            {
                if (char.IsUpper(inspectorName[n]))
                {
                    inspectorName = $"{inspectorName.Substring(0, n)} {inspectorName.Substring(n, inspectorName.Length - n )}";
                   
                }
            }
         
            
            return inspectorName;
        }
    }
}