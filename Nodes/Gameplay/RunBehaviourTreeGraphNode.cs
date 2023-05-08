using Platinio.GraphCore;
using Unity.VisualScripting;
using UnityEngine;

namespace Platinio.BehaviourTree
{
    [GraphCreateMenu("Gameplay/Run Behaviour Tree Graph")]
    public class RunBehaviourTreeGraphNode : GameplayNode
    {
        [Serialize] [Inspectable]
        private BehaviourTreeGraphAsset m_behaviourTreeGraphAsset;
        
        private BehaviourTreeGraph m_graph = null;

        public override string NodeName => "Run Behaviour Tree Graph";

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
        
        private BehaviourTreeGraph GetGraph()
        {
            if (m_graph == null)
            {
                m_graph = Object.Instantiate(m_behaviourTreeGraphAsset).graph;
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

