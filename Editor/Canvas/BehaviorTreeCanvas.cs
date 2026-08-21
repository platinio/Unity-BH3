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

        /// <summary>
        /// Whether a behaviour tree canvas is currently open, and a handle to reach the ambient edited
        /// context through — which is what <see cref="GetBehaviorTreeGraphAsset"/> and
        /// <see cref="GetSelectedBehaviorTreeMachine"/> answer from, so property drawers outside the canvas
        /// can ask which tree the user is looking at.
        ///
        /// <para>
        /// Cleared in <see cref="Close"/>, and only by the canvas that claimed it. Left set, it rooted the
        /// whole canvas — every widget, port and the graph itself — for the rest of the session, and with
        /// domain reload disabled that survives play cycles too. It also kept both accessors answering with
        /// a tree nobody is editing any more, because <c>context</c> is the ambient edited context rather
        /// than this canvas's own: a non-null static was the only thing standing between a closed window
        /// and a confident wrong answer.
        /// </para>
        ///
        /// <para>
        /// Private setter so the lifetime is a property of this class rather than of whoever assigns last;
        /// the identity check on clear is what keeps a canvas closing after another one opened from
        /// retracting the newer canvas's claim.
        /// </para>
        /// </summary>
        public static BehaviorTreeCanvas OpenBehaviorTreeCanvas { get; private set; }


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
            
            if (HasDanglingElements())
            {
                RemoveDanglingElements();
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

        /// <summary>Scratch list for <see cref="RemoveDanglingElements"/>, reused rather than reallocated.</summary>
        private readonly List<GraphCore.IGraphElement> dangling = new List<GraphCore.IGraphElement>();

        /// <summary>
        /// Deletes the elements whose anchor has gone, with an undo record.
        ///
        /// <para>
        /// This is the one place allowed to remove an element because something it points at is missing.
        /// Transitions used to be culled from inside a bare <c>catch</c> in
        /// <see cref="BehaviorTreeTransitionWidget.CachePosition"/> instead, which destroyed authored data
        /// with no undo record and treated any exception at all as evidence that a node had been deleted.
        /// A layout pass is the wrong place to edit the graph in any case: it runs while the canvas is
        /// iterating the very collection it was mutating.
        /// </para>
        /// </summary>
        private void RemoveDanglingElements()
        {
            UndoUtility.RecordEditedObject("Delete Graph Element");

            // Collected first, then removed. Walking by index meant `ElementAt` on a merged collection, which
            // enumerates from the start every call — an O(n²) pass over a collection it was mutating as it
            // went. Two halves of one rule that scan differently is the drift this class exists to stop.
            dangling.Clear();

            foreach (var graphElement in graph.elements)
            {
                if (IsDangling(graphElement)) dangling.Add(graphElement);
            }

            foreach (var graphElement in dangling) graph.elements.Remove(graphElement);

            dangling.Clear();
        }

        private bool HasDanglingElements()
        {
            foreach (var graphElement in graph.elements)
            {
                if (IsDangling(graphElement)) return true;
            }

            return false;
        }

        private bool IsDangling(GraphCore.IGraphElement graphElement) => IsDangling(graph, graphElement);

        /// <summary>
        /// Whether an element is anchored to something that no longer exists: a guard whose owner has been
        /// deleted, or a transition missing an end. Both are undrawable, and both arise the same way —
        /// deleting a node the element was attached to.
        ///
        /// <para>
        /// Static because the question is about the graph, not about a canvas: the transition widget asks it
        /// to decide whether to lay itself out this frame, and the repair above asks it to decide what to
        /// delete. Stating it twice is how the two ended up disagreeing, with layout deleting elements the
        /// repair was there to handle.
        /// </para>
        /// </summary>
        public static bool IsDangling(BehaviorTreeGraph graph, GraphCore.IGraphElement graphElement)
        {
            if (graph == null) return false;

            if (graphElement is ConditionalExecution conditionalExecution)
            {
                return conditionalExecution.Owner == null || !graph.elements.Contains(conditionalExecution.Owner);
            }

            if (graphElement is BehaviorTreeTransition transition)
            {
                return transition.source == null
                    || transition.destination == null
                    || !graph.elements.Contains(transition.source)
                    || !graph.elements.Contains(transition.destination);
            }

            return false;
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
                // A reference can name a GameObject whose machine component has since been removed, and the
                // drawers calling this run on whatever the inspector is showing. Unguarded, that is an NRE
                // in a property drawer -- which draws as a broken inspector row rather than as a message
                // anyone can act on.
                var machine = gameObject.GetComponent<BehaviorTreeMachine>();
                return machine != null ? machine.GraphAsset : null;
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

            // Only this canvas's own claim. Opening another tree can run Open on the new canvas before Close
            // on the old one, and an unconditional null there would retract the newer canvas's claim and
            // leave the accessors answering null while a window is plainly open.
            if (OpenBehaviorTreeCanvas == this) OpenBehaviorTreeCanvas = null;
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
