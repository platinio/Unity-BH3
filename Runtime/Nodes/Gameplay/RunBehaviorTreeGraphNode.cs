using System;
using System.Collections.Generic;
using ArcaneOnyx.GraphCore;
using Unity.VisualScripting;
using Object = UnityEngine.Object;

namespace ArcaneOnyx.BehaviorTree
{
    [GraphCreateMenu("Gameplay/Run Behavior Tree Graph")]
    public class RunBehaviorTreeGraphNode : GameplayNode
    {
        [Serialize] [Inspectable]
        private BehaviorTreeGraphAsset behaviorTreeGraphAsset;

        private BehaviorTreeGraphAsset behaviorTreeGraphAssetInstance = null;

        public BehaviorTreeGraphAsset BehaviorTreeGraphAsset => behaviorTreeGraphAsset;
        public BehaviorTreeGraph BehaviorTreeGraphInstance => BehaviorTreeGraphAssetInstance.graph;

        public BehaviorTreeGraphAsset BehaviorTreeGraphAssetInstance
        {
            get
            {
                if (behaviorTreeGraphAssetInstance == null)
                {
                    behaviorTreeGraphAssetInstance = Object.Instantiate(behaviorTreeGraphAsset);
                }

                return behaviorTreeGraphAssetInstance;
            }
        }

        private bool willCauseRecursion = false;
        private string stackTraceLog = null;

        //public override bool DrawInSubTree => false;

        public override string NodeName
        {
            get
            {
                if (behaviorTreeGraphAsset == null) return "Missing Graph!";
                return behaviorTreeGraphAsset.name;
            }
        }
       
        public override void OnAwake()
        {
            BehaviorTreeGraphInstance.OnAwake();
            
            var runStack = new Stack<BehaviorTreeGraphAsset>(new[] { behaviorTreeGraphAsset });
            willCauseRecursion = GraphWillCauseRecursion(runStack);
            if (willCauseRecursion)
            {
                stackTraceLog = "";
                while (runStack.Count > 0)
                {
                    var graphAsset = runStack.Pop();
                    stackTraceLog += $"{graphAsset.name} ->";
                }
                
                throw new Exception("RunBehaviorTreeNode causes recursion stack trace: " + stackTraceLog);
            }
        }

        private void ThrowIfWillCauseRecursion()
        {
            if (willCauseRecursion)
            {
                throw new Exception("RunBehaviorTreeNode causes recursion stack trace: " + stackTraceLog);
            }
        }

        public override void OnEnter()
        {
            ThrowIfWillCauseRecursion();
            BehaviorTreeGraphInstance.OnEnter();
        }

        public bool GraphWillCauseRecursion(Stack<BehaviorTreeGraphAsset> runStack)
        {
            var graphAsset = runStack.Peek();
            if (graphAsset == null) return false;
            
            foreach (var node in graphAsset.graph.Nodes)
            {
                if (node is RunBehaviorTreeGraphNode runBehaviorTreeGraphNode)
                {
                    if (runStack.Contains(runBehaviorTreeGraphNode.behaviorTreeGraphAsset)) return true;
                    
                    runStack.Push(runBehaviorTreeGraphNode.behaviorTreeGraphAsset);
                    if (GraphWillCauseRecursion(runStack)) return true;

                    runStack.Pop();
                }
            }

            return false;
        }

        public override ExecutionStatus OnUpdate()
        {
            ThrowIfWillCauseRecursion();
            return BehaviorTreeGraphInstance.OnUpdate();
        }

        public override void SetMachine(IGraphMachine machine)
        {
            base.SetMachine(machine);

            var nodes = BehaviorTreeGraphInstance.Nodes;
            foreach (var node in nodes)
            {
                node.SetMachine(machine);
            }
        }

        public override void CanvasUpdate()
        {
            base.CanvasUpdate();

            foreach (var node in BehaviorTreeGraphInstance.Nodes)
            {
               node.CanvasUpdate(); 
            }
        }
    }
}

