using System;
using ArcaneOnyx.BehaviorTree;
using ArcaneOnyx.VisualScripting;
using ArcaneOnyxArcaneOnyx.VisualScripting;
using Platinio;
using Unity.VisualScripting;
using UnityEditor;
using UnityEngine;

namespace ArcaneOnyx
{
    [CustomPropertyDrawer(typeof(BTScriptGraphVariable))]
    public class BTScriptGraphVariablePropertyDrawer : PropertyDrawer
    {
        public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
        {
            PlatinioPropertyDrawer.CalculateLabelAndValueRect(position, out Rect labelRect, out Rect valueRect);
            
            GUI.Label(labelRect, label);

            var scriptGraphProperty = property.FindPropertyRelative("scriptGraphAsset");
            
            if (GUI.Button(valueRect, "Open Graph"))
            {
                var scriptGraphAsset = scriptGraphProperty.objectReferenceValue as ScriptGraphAsset;

                if (scriptGraphAsset == null)
                {
                    var returnType = (property.boxedValue as BTScriptGraphVariable).ReturnType;
                    scriptGraphAsset = returnType == null? CreateRunnableGraph() : CreateGraphWithOutput(returnType);
                    scriptGraphProperty.objectReferenceValue = scriptGraphAsset;
                }

                UnityEngine.Object obj = scriptGraphAsset;
                GraphReference reference = null;
                if (obj is IMacro macro)
                    reference = GraphReference.New(macro, true);
                else if (obj is IGraphRoot root)
                    reference = GraphReference.New(root, false);
                if (obj is IGraphNesterElement nesterElement)
                    reference = LudiqGraphsEditorUtility.editedContext.value.reference.ChildReference(nesterElement, false);
                if (reference == null)
                    return;
                
                GraphWindow.OpenActive(reference);
            }
            
            
            scriptGraphProperty.serializedObject.ApplyModifiedProperties();
            property.serializedObject.SetIsDifferentCacheDirty();
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

