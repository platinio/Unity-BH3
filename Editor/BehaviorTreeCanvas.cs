using System;
using System.Collections.Generic;
using System.Linq;
using Platinio.GraphCore;
using Unity.VisualScripting;
using UnityEditor;
using UnityEditor.Callbacks;
using UnityEngine;
using UnityObject = UnityEngine.Object;

namespace Platinio.BehaviorTree
{
    [Canvas(typeof(BehaviorTreeGraph))]
    public class BehaviorTreeCanvas : BaseCanvas<BehaviorTreeGraph, BehaviorTreeNode, BehaviorTreeTransition>
    {
        public BehaviorTreeCanvas(BehaviorTreeGraph graph) : base(graph) { }

        public static BehaviorTreeCanvas OpenBehaviorTreeCanvas;
        public static BehaviorTreeGraphAsset OpenBehaviorTreeGraphAsset;
        
        public Vector2 ConnectionEnd { get; set; }
        public bool IsCreatingConnection => ConnectionSource != null &&
                                            ConnectionSource.behaviorTreeNode != null;        
        public IPort ConnectionSource { get; set; }
        
        public bool ShowRelations { get; set; }
        
        public void CancelConnection()
        {
            ConnectionSource = null;
        }
        
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
            GraphCore.GraphWindow.OpenActive<BehaviorTreeGraphWindow>(reference);
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
        
        protected override void HandleHighPriorityInput()
        {
            if (IsCreatingConnection)
            {
                if (e.IsFree(EventType.KeyDown) && e.keyCode == KeyCode.Escape)
                {
                    CancelConnection();
                    e.Use();
                }
            }

            base.HandleHighPriorityInput();
        }
        
        public override void Close()
        {
            base.Close();

            CancelConnection();
        }

        
    }
}
