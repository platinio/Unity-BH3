using Platinio.GraphCore;
using Unity.VisualScripting;
using UnityEngine;

namespace Platinio.BehaviorTree
{
    [GraphCreateMenu("Gameplay/Run Behavior Tree Graph")]
    public class RunBehaviorTreeGraphNode : GameplayNode
    {
        [Serialize] [Inspectable]
        private BehaviorTreeGraphAsset behaviorTreeGraphAsset;
        
        private BehaviorTreeGraph behaviorTreeGraphInstance = null;

        public BehaviorTreeGraphAsset BehaviorTreeGraphAsset => behaviorTreeGraphAsset;

        public BehaviorTreeGraph BehaviorTreeGraphInstance
        {
            get
            {
                if (behaviorTreeGraphInstance == null)
                {
                    behaviorTreeGraphInstance = Object.Instantiate(behaviorTreeGraphAsset).graph;
                }

                return behaviorTreeGraphInstance;
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

        public override void OnEnter()
        {
            BehaviorTreeGraphInstance.OnEnter();
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

