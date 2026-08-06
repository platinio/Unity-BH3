using System;
using System.Collections.Generic;
using System.Linq;
using ArcaneOnyx.GraphCore;
using Unity.VisualScripting;
using Object = UnityEngine.Object;

namespace ArcaneOnyx.BehaviorTree
{
    [GraphCreateMenu("Gameplay/Run Behavior Tree Graph")]
    public class RunBehaviorTreeGraphNode : GameplayNode
    {
        [Serialize, Inspectable]
        private BehaviorTreeGraphAsset behaviorTreeGraphAsset;

        private BehaviorTreeGraphAsset behaviorTreeGraphAssetInstance = null;

        public BehaviorTreeGraphAsset BehaviorTreeGraphAsset => behaviorTreeGraphAsset;
        public BehaviorTreeGraph BehaviorTreeGraphInstance => BehaviorTreeGraphAssetInstance.graph;
       
        public void SetBehaviorTreeGraphAsset(BehaviorTreeGraphAsset asset)
        {
            behaviorTreeGraphAsset = asset;
            behaviorTreeGraphAssetInstance = null;
        }

        public override string Description => "Executes BehaviorTreeGraphAsset";

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
        }

        public void ThrowIfCausesRecursion()
        {
            var runStack = new Stack<BehaviorTreeGraphAsset>(new[] { behaviorTreeGraphAsset });
            if (!GraphWillCauseRecursion(runStack)) return;
          
            string stackTraceLog = "";
               
            var runStackList = runStack.ToList();
            string firstNode = runStack.First().name;
                
            while (runStackList.Count > 0)
            {
                var graphAsset = runStackList[0];
                stackTraceLog += $"{graphAsset.name} ->";
                runStackList.RemoveAt(0);
            }

            stackTraceLog += $" {firstNode}";
               
            throw new Exception("RunBehaviorTreeNode causes recursion stack trace: " + stackTraceLog);
        }

        public override void OnEnter()
        {
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

