using System;
using System.Collections.Generic;
using ArcaneOnyx.GraphCore;
using Unity.VisualScripting;
using UnityEditor;
using UnityEditor.Callbacks;
using UnityEngine;
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
        
        protected override void HandleClipboard()
        {
            if (e.IsValidateCommand("Copy"))
            {
                if (GraphClipboard.canCopySelection)
                {
                    e.ValidateCommand();
                }
            }
            else if (e.IsExecuteCommand("Copy"))
            {
                selection.RemoveWhere(x =>
                {
                    if (x is BehaviorTreeNode node)
                    {
                        return !node.CanCopy;
                    }

                    return false;
                });
                
                if (selection.Count > 0) GraphClipboard.CopySelection();
            }

            if (e.IsValidateCommand("Cut"))
            {
                if (GraphClipboard.canCopySelection)
                {
                    e.ValidateCommand();
                }
            }
            else if (e.IsExecuteCommand("Cut"))
            {
                selection.RemoveWhere(x =>
                {
                    if (x is BehaviorTreeNode node)
                    {
                        return !node.CanCut;
                    }

                    return false;
                });
                
                if (selection.Count > 0) GraphClipboard.CutSelection();
            }

            if (e.IsValidateCommand("Paste"))
            {
                if (GraphClipboard.canPaste && (DateTime.Now - lastPasteTime).TotalSeconds >= 0.25)
                {
                    e.ValidateCommand();
                }
            }
            else if (e.IsExecuteCommand("Paste"))
            {
                GraphClipboard.Paste();
                lastPasteTime = DateTime.Now;
            }

            if (e.IsValidateCommand("Duplicate"))
            {
                if (GraphClipboard.canDuplicateSelection && (DateTime.Now - lastPasteTime).TotalSeconds >= 0.25)
                {
                    e.Use();
                }
            }
            else if (e.IsExecuteCommand("Duplicate"))
            {
                selection.RemoveWhere(x =>
                {
                    if (x is BehaviorTreeNode node)
                    {
                        return !node.CanDuplicate;
                    }

                    return false;
                });
                
                if (selection.Count > 0) GraphClipboard.DuplicateSelection();
                lastPasteTime = DateTime.Now;
            }
        }
    }
}
