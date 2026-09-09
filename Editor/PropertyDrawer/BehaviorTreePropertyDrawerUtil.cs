using System;
using System.Collections.Generic;
using System.Linq;
using Unity.VisualScripting;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace ArcaneOnyx.BehaviorTree
{
    public static class BehaviorTreePropertyDrawerUtil
    {
        /// <summary>One inspector row, spacing included: what a drawer reports as its height per line.</summary>
        public static float LineHeight => EditorGUIUtility.singleLineHeight + EditorGUIUtility.standardVerticalSpacing;

        /// <summary>The next row of a drawer's rect, advancing <paramref name="y"/> past it.</summary>
        public static Rect NextLine(Rect position, ref float y)
        {
            var rect = new Rect(position.x, y, position.width, EditorGUIUtility.singleLineHeight);
            y += LineHeight;
            return rect;
        }

        /// <summary>Divides a row into the label column and the value column beside it.</summary>
        public static void SplitLabelAndValue(Rect line, out Rect label, out Rect value)
        {
            const float horizontalSpace = 5.0f;

            label = new Rect(line.x, line.y, EditorGUIUtility.labelWidth, EditorGUIUtility.singleLineHeight);
            value = new Rect(line.x + label.width + horizontalSpace, line.y, line.width - label.width, EditorGUIUtility.singleLineHeight);
        }

        /// <summary>Draws a property with a prefix label on the next row.</summary>
        public static void PropertyField(Rect position, SerializedProperty property, string label, ref float y)
        {
            var rect = NextLine(position, ref y);
            rect = EditorGUI.PrefixLabel(rect, GUIUtility.GetControlID(FocusType.Passive), new GUIContent(label));
            EditorGUI.PropertyField(rect, property, GUIContent.none);
        }

        public static List<VariableDeclaration> GetVariableDeclarations(Type validType = null)
        {
            List<VariableDeclaration> variableDeclarations = new();

            var selectedMachine = BehaviorTreeCanvas.GetSelectedBehaviorTreeMachine();
            if (selectedMachine != null)
            {
                Variables variables = selectedMachine.Variables;
                if (variables == null)
                {
                    variables = selectedMachine.gameObject.GetComponent<Variables>();
                }

                AddVariableDeclarations(variableDeclarations, variables.declarations);
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

        private static void AddVariableDeclarations(List<VariableDeclaration> variableDeclarationList, VariableDeclarations variableDeclarations, Type validType = null)
        {
            if (variableDeclarations == null) return;

            var variablesEnumerator = variableDeclarations.GetEnumerator();

            while (variablesEnumerator.MoveNext())
            {
                var current = variablesEnumerator.Current;
                string typeHandleIdetification = current.typeHandle.Identification;

                if (current == null || (validType != null && validType.AssemblyQualifiedName != typeHandleIdetification)) continue;

                if (variableDeclarationList.Where(x => x.name == current.name).FirstOrDefault() != null) continue;

                variableDeclarationList.Add(current);
            }
        }
    }
}
