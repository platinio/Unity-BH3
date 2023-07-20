using Platinio.GraphCore;
using Unity.VisualScripting;
using UnityEngine;

namespace Platinio.BehaviorTree
{
    [RequireComponent(typeof(Variables))]
    public class BehaviorTreeMachine : BaseMachine<BehaviorTreeGraph, BehaviorTreeGraphAsset, BehaviorTreeNode, BehaviorTreeTransition>
    {
        private BehaviorTreeGraph m_graph;

        protected override void Awake()
        {
            base.Awake();
            
            m_variables = GetComponent<Variables>();
            
            if (hasGraph)
            {
                nest.SwitchToEmbed(Instantiate(nest.macro).graph);
                m_graph = nest.embed;
                
                var nodes = m_graph.Nodes;

                foreach (var node in nodes)
                {
                    node.SetMachine(this);
                }
                
                m_graph.OnAwake();
                
                
            }
        }

        private void Start()
        {
            if (hasGraph)
            {
                m_graph.OnEnter();
            }
        }

        private void Update()
        {
            if (hasGraph)
            {
                m_graph.OnUpdate();
            }
        }

        public override BehaviorTreeGraph DefaultGraph()
        {
            return new BehaviorTreeGraph();
        }
    }
}