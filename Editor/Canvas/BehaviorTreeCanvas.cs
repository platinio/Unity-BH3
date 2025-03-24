using System;
using System.Collections.Generic;
using System.Linq;
using ArcaneOnyx.GraphCore;
using Unity.VisualScripting;
using UnityEditor;
using UnityEditor.Callbacks;
using UnityEngine;
using GraphReference = ArcaneOnyx.GraphCore.GraphReference;
using IGraphNesterElement = ArcaneOnyx.GraphCore.IGraphNesterElement;
using IGraphRoot = ArcaneOnyx.GraphCore.IGraphRoot;
using IMacro = ArcaneOnyx.GraphCore.IMacro;
using LudiqGraphsEditorUtility = ArcaneOnyx.GraphCore.LudiqGraphsEditorUtility;
using UnityObject = UnityEngine.Object;

namespace ArcaneOnyx.BehaviorTree
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
        
        private DateTime lastPasteTime;
        
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

        public override void OnGUI()
        {
            base.OnGUI();
            
            if (HasInvalidConditionals())
            {
                RemoveInvalidConditionals();
            }

            ScriptGraphAssetsRepository.Instance.RemoveInvalid();
            
            //read new script graph assets
            foreach (var graphElement in graph.elements)
            {
                if (graphElement.scriptGraphAssets == null || graphElement.scriptGraphAssets.Count() == 0) continue;
                graph.AddScriptGraphAssets(GetBehaviorTreeGraphAsset(), graphElement.scriptGraphAssets);
            }
            
            if (!EditorApplication.isPlaying) graph.DestroyUnusedScriptGraphAssets(GetBehaviorTreeGraphAsset());
        }

        private void RemoveInvalidConditionals()
        {
            UndoUtility.RecordEditedObject("Delete Graph Element");
                
            for (int i = graph.elements.Count - 1; i >= 0; i--)
            {
                var graphElement = graph.elements.ElementAt(i);
                var conditionalExecution = graphElement as ConditionalExecution;
                var owner = conditionalExecution?.Owner;
                
                if (conditionalExecution != null)
                {
                    if (owner == null || !graph.elements.Contains(owner))
                    {
                        graph.elements.Remove(graphElement);
                    }
                }
            }
        }

        private bool HasInvalidConditionals()
        {
            for (int i = graph.elements.Count - 1; i >= 0; i--)
            {
                var graphElement = graph.elements.ElementAt(i);
                if (IsInvalidConditional(graphElement as ConditionalExecution)) return true;
            }

            return false;
        }

        private bool IsInvalidConditional(ConditionalExecution conditionalExecution)
        {
            if (conditionalExecution == null) return false;
            return conditionalExecution.Owner == null || !graph.elements.Contains(conditionalExecution.Owner);
        }

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
        
        protected override void HandleLowPriorityInput()
        {
            HandleClipboard();
            base.HandleLowPriorityInput();
        }
        
        private void HandleClipboard()
        {
            if (e.IsExecuteCommand("Copy") || e.IsValidateCommand("Paste"))
            {
                selection.RemoveWhere(x =>
                {
                    if (x is BehaviorTreeNode node)
                    {
                        return !node.CanCopy;
                    }

                    return false;
                });
            }

           if (e.IsExecuteCommand("Cut"))
            {
                selection.RemoveWhere(x =>
                {
                    if (x is BehaviorTreeNode node)
                    {
                        return !node.CanCut;
                    }

                    return false;
                });
            }

            if (e.IsExecuteCommand("Duplicate"))
            {
                selection.RemoveWhere(x =>
                {
                    if (x is BehaviorTreeNode node)
                    {
                        return !node.CanDuplicate;
                    }

                    return false;
                });
            }
        }
    }
}
