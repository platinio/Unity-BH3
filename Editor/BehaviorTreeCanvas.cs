using System;
using System.Collections.Generic;
using Platinio.GraphCore;
using Unity.VisualScripting;
using UnityEditor;
using UnityEditor.Callbacks;
using UnityObject = UnityEngine.Object;

namespace Platinio.BehaviorTree
{
    [Canvas(typeof(BehaviorTreeGraph))]
    public class BehaviorTreeCanvas : BaseCanvas<BehaviorTreeGraph, BehaviorTreeNode, BehaviorTreeTransition>
    {
        public BehaviorTreeCanvas(BehaviorTreeGraph graph) : base(graph) { }

        public static BehaviorTreeCanvas OpenBehaviorTreeCanvas;
        public static BehaviorTreeGraphAsset OpenBehaviorTreeGraphAsset;
        
        
        [OnOpenAsset(int.MinValue)]
        public static bool OnOpenAsset(int instanceID, int line)
        {
            UnityObject obj = EditorUtility.InstanceIDToObject(instanceID);
            if (!(obj is BehaviorTreeGraphAsset)) return false;
            
            GraphReference reference = null;
            if (obj is IMacro macro)
                reference = GraphReference.New(macro, true);
            else if (obj is IGraphRoot root)
                reference = GraphReference.New(root, false);
            if (obj is IGraphNesterElement nesterElement)
                reference = LudiqGraphsEditorUtility.editedContext.value.reference.ChildReference(nesterElement, false);
            if (reference == null)
                return false;
            GraphCore.GraphWindow.OpenActive(reference);
            return true;
        }
        
        protected override IEnumerable<Type> GetValidNodes() => new List<Type>()
        {
            typeof(Composite),
            typeof(ContainerNode),
            typeof(Decorator),
            typeof(GameplayNode),
            typeof(Condition)
        };

        public override void Open()
        {
            base.Open();
            
            OpenBehaviorTreeCanvas = this;
        }

        public static BehaviorTreeGraphAsset GetBehaviorTreeGraphAsset()
        {
            if (OpenBehaviorTreeCanvas == null || OpenBehaviorTreeCanvas.context == null) return null;
        
            var gameObject = OpenBehaviorTreeCanvas.context.reference.gameObject;
            if (gameObject != null)
            {
                return gameObject.GetComponent<BehaviorTreeMachine>().GraphAsset;
            }

            return OpenBehaviorTreeCanvas.context.reference.scriptableObject as BehaviorTreeGraphAsset;
        }

        public static BehaviorTreeMachine GetSelectedBehaviorTreeMachine()
        {
            if (OpenBehaviorTreeCanvas == null || OpenBehaviorTreeCanvas.context == null) return null;
            
            var gameObject = OpenBehaviorTreeCanvas.context.reference.gameObject;
            if (gameObject != null)
            {
                return gameObject.GetComponent<BehaviorTreeMachine>();
            }

            return null;
        }
    }
}
