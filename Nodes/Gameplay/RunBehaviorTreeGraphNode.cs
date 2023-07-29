using Platinio.GraphCore;
using Unity.VisualScripting;
using UnityEngine;

namespace Platinio.BehaviorTree
{
    [GraphCreateMenu("Gameplay/Run Behavior Tree Graph")]
    public class RunBehaviorTreeGraphNode : GameplayNode
    {
        [Serialize] [Inspectable]
        private BehaviorTreeGraphAsset m_behaviorTreeGraphAsset;
        
        private BehaviorTreeGraph m_graph = null;

        public override string NodeName
        {
            get
            {
                if (m_behaviorTreeGraphAsset == null) return "Missing Graph!";
                return m_behaviorTreeGraphAsset.name;
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
            if (m_graph == null)
            {
                m_graph = Object.Instantiate(m_behaviorTreeGraphAsset).graph;
            }

            return m_graph;
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

