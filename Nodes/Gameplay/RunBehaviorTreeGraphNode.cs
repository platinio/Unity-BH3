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
        
        private BehaviorTreeGraph behaviorTreeGraph = null;

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
            GetGraph().OnAwake();
        }

        public override void OnEnter()
        {
            GetGraph().OnEnter();
        }
        
        public override ExecutionStatus OnUpdate()
        {
            return GetGraph().OnUpdate();
        }
        
        private BehaviorTreeGraph GetGraph()
        {
            if (behaviorTreeGraph == null)
            {
                behaviorTreeGraph = Object.Instantiate(behaviorTreeGraphAsset).graph;
            }

            return behaviorTreeGraph;
        }
        
        public override void SetMachine(IGraphMachine machine)
        {
            base.SetMachine(machine);
            var nodes = GetGraph().Nodes;
            foreach (var node in nodes)
            {
                node.SetMachine(machine);
            }
        }
    }
}

