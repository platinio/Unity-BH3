using System.Collections.Generic;
using System.Linq;
using Unity.VisualScripting;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Platinio.BehaviorTree
{
    [CustomPropertyDrawer(typeof(BlackboardKey))]
    public class BlackboardKeyPropertyDrawer : PropertyDrawer
    {
        public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
        {
            EditorGUI.BeginProperty(position,label,property);
         
            string variableName = property.FindPropertyRelative("blackboardKeyName").stringValue;
            float yPosition = position.position.y;

            var rect = PlatinioPropertyDrawer.CalculatePropertyRect(position, ref yPosition);
            PlatinioPropertyDrawer.CalculateLabelAndValueRect(rect, out Rect labelRect, out Rect valueRect);
            
            EditorGUI.LabelField(labelRect, label.text);

            if (EditorGUI.DropdownButton(valueRect, new GUIContent(GetLabelVariableName(variableName)), FocusType.Passive))
            {
                var menu = new GenericMenu();

                foreach (var variableDeclaration in GetVariableDeclarations())
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
            foreach (var variableDeclaration in GetVariableDeclarations())
            {
                if (variableName == variableDeclaration.name) return true;
            }

            return false;
        }
        
        private List<VariableDeclaration> GetVariableDeclarations()
        {
            List<VariableDeclaration> variableDeclarations = new();

            var selectedMachine = BehaviorTreeCanvas.GetSelectedBehaviorTreeMachine();
            if (selectedMachine != null)
            {
                AddVariableDeclarations(variableDeclarations, selectedMachine.Variables.declarations);
            }
            
            var graphAsset = BehaviorTreeCanvas.GetBehaviorTreeGraphAsset();
            if (graphAsset != null)
            {
                AddVariableDeclarations(variableDeclarations, graphAsset.declarations);
            }

            AddVariableDeclarations(variableDeclarations, Variables.Scene(SceneManager.GetActiveScene()));
            AddVariableDeclarations(variableDeclarations, Variables.Application);
            AddVariableDeclarations(variableDeclarations, Variables.Saved);

            return variableDeclarations;
        }
        
        private void AddVariableDeclarations(List<VariableDeclaration> variableDeclarationList, VariableDeclarations variableDeclarations)
        {
            if (variableDeclarations == null) return;
            
            var variablesEnumerator = variableDeclarations.GetEnumerator();

            while (variablesEnumerator.MoveNext())
            {
                var current = variablesEnumerator.Current;
                if (current == null) continue;
                
                if (variableDeclarationList.Where(x => x.name == current.name).FirstOrDefault() != null) continue;
                
                variableDeclarationList.Add(current);
            }
        }
        
    }
}

