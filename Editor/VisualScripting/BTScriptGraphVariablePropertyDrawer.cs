using System;
using ArcaneOnyx.UnityExtensions;
using ArcaneOnyx.VisualScriptingExtension;
using Unity.VisualScripting;
using UnityEditor;
using UnityEngine;

namespace ArcaneOnyx.BehaviorTree
{
    /// <summary>
    /// Draws the two ways a node can get its value: a <b>Function</b> it references, or an embedded graph it
    /// owns.
    ///
    /// <para>
    /// The Function field is what makes a Function reachable without the CLI. Before it existed, one could
    /// only arrive through <c>bt_guard_on_function</c> or <c>fn_extract</c>, so the shared-predicate half of
    /// the feature was invisible to anyone working in the editor.
    /// </para>
    ///
    /// <para>
    /// <b>Assigning one here needs no other wiring.</b> A guard reading this node picks the Function's watched
    /// keys up at evaluation, and the schedule is written into the asset by <c>GuardScheduleSeeder</c> on the
    /// next save — a hook on saving rather than on assignment, so this drawer does not have to remember to do
    /// anything.
    /// </para>
    /// </summary>
    [CustomPropertyDrawer(typeof(BTScriptGraphVariable))]
    public class BTScriptGraphVariablePropertyDrawer : PropertyDrawer
    {
        private const string FunctionField = "function";
        private const string GraphField = "scriptGraphAsset";

        private static float Row => EditorGUIUtility.singleLineHeight + EditorGUIUtility.standardVerticalSpacing;

        public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
        {
            // Two rows normally, plus a warning row only when both sources are assigned — which is a state a
            // designer can now reach by dragging, so it has to be visible at the moment they do it.
            return IsAmbiguous(property) ? Row * 3.0f : Row * 2.0f;
        }

        public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
        {
            var functionProperty = property.FindPropertyRelative(FunctionField);
            var graphProperty = property.FindPropertyRelative(GraphField);

            var row = new Rect(position.x, position.y, position.width, EditorGUIUtility.singleLineHeight);
            ArcaneOnyxPropertyDrawer.CalculateLabelAndValueRect(row, out var labelRect, out var valueRect);

            GUI.Label(labelRect, label);
            EditorGUI.PropertyField(valueRect, functionProperty, GUIContent.none);

            row.y += Row;
            ArcaneOnyxPropertyDrawer.CalculateLabelAndValueRect(row, out _, out var buttonRect);

            var function = functionProperty.objectReferenceValue as FunctionGraphAsset;

            if (GUI.Button(buttonRect, function != null ? "Open Function" : "Open Graph"))
            {
                Open(function, graphProperty, property);
            }

            if (IsAmbiguous(property))
            {
                row.y += Row;

                EditorGUI.HelpBox(
                    row,
                    "The Function runs; the embedded graph is editable but dead. Clear one.",
                    MessageType.Warning);
            }

            graphProperty.serializedObject.ApplyModifiedProperties();
            property.serializedObject.SetIsDifferentCacheDirty();
        }

        private static bool IsAmbiguous(SerializedProperty property)
        {
            var functionProperty = property.FindPropertyRelative(FunctionField);
            var graphProperty = property.FindPropertyRelative(GraphField);

            return functionProperty?.objectReferenceValue != null && graphProperty?.objectReferenceValue != null;
        }

        /// <summary>
        /// Opens whichever graph this node actually reads. A referenced Function is opened as it is; an
        /// embedded graph is created first if there is none yet, which is how an empty node becomes editable.
        /// </summary>
        private void Open(
            FunctionGraphAsset function, SerializedProperty graphProperty, SerializedProperty property)
        {
            UnityEngine.Object target = function;

            if (target == null)
            {
                var scriptGraphAsset = graphProperty.objectReferenceValue as ScriptGraphAsset;

                if (scriptGraphAsset == null)
                {
                    var returnType = (property.boxedValue as BTScriptGraphVariable).ReturnType;
                    scriptGraphAsset = returnType == null ? CreateRunnableGraph() : CreateGraphWithOutput(returnType);
                    graphProperty.objectReferenceValue = scriptGraphAsset;
                }

                target = scriptGraphAsset;
            }

            GraphReference reference = null;

            if (target is IMacro macro)
                reference = GraphReference.New(macro, true);
            else if (target is IGraphRoot root)
                reference = GraphReference.New(root, false);
            if (target is IGraphNesterElement nesterElement)
                reference = LudiqGraphsEditorUtility.editedContext.value.reference.ChildReference(nesterElement, false);
            if (reference == null)
                return;

            GraphWindow.OpenActive(reference);
        }

        protected ScriptGraphAsset CreateGraphWithOutput(Type returnType)
        {
            var scriptGraphAsset = CreateRunnableGraph();

            var valueOutputDefinition = new Unity.VisualScripting.ValueOutputDefinition();
            valueOutputDefinition.key = "Result";
            valueOutputDefinition.label = "Result";
            valueOutputDefinition.type = returnType;

            scriptGraphAsset.graph.valueOutputDefinitions.Add(valueOutputDefinition);
            scriptGraphAsset.graph.PortDefinitionsChanged();

            return scriptGraphAsset;
        }

        protected ScriptGraphAsset CreateRunnableGraph()
        {
            var scriptGraphAssetInstance = ScriptableObject.CreateInstance(typeof(ScriptGraphAsset)) as ScriptGraphAsset;

            var graphInput = new ScriptGraphInput();
            Vector2 newPosition = graphInput.position;
            newPosition.x -= 400.0f;
            graphInput.position = newPosition;


            var graphOutput = new ScriptGraphOutput();
            newPosition = graphOutput.position;
            newPosition.x += 400.0f;
            graphOutput.position = newPosition;

            scriptGraphAssetInstance.graph.units.Add(graphInput);
            scriptGraphAssetInstance.graph.units.Add(graphOutput);

            var controlInputDefinition = new ControlInputDefinition();
            controlInputDefinition.key = "Enter";
            controlInputDefinition.label = "Enter";

            var controlOutputDefinition = new ControlOutputDefinition();
            controlOutputDefinition.key = "Exit";
            controlOutputDefinition.label = "Exit";

            scriptGraphAssetInstance.graph.controlInputDefinitions.Add(controlInputDefinition);
            scriptGraphAssetInstance.graph.controlOutputDefinitions.Add(controlOutputDefinition);

            var behaviorTreeGraphAsset = BehaviorTreeCanvas.GetBehaviorTreeGraphAsset();
            AssetDatabase.AddObjectToAsset(scriptGraphAssetInstance, behaviorTreeGraphAsset);
            EditorUtility.SetDirty(behaviorTreeGraphAsset);

            scriptGraphAssetInstance.graph.PortDefinitionsChanged();

            return scriptGraphAssetInstance;
        }
    }
}
