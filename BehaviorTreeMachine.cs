using GameDataEngine;
using Platinio.GraphCore;
using Unity.VisualScripting;
using UnityEngine;

namespace Platinio.BehaviorTree
{
    [RequireComponent(typeof(Variables))]
    public class BehaviorTreeMachine : BaseMachine<BehaviorTreeGraph, BehaviorTreeGraphAsset, BehaviorTreeNode, BehaviorTreeTransition>
    {
        private BehaviorTreeGraph m_graph;
        private ExecutionStatus m_lastExecutionStatus = ExecutionStatus.Inactive;

        public ExecutionStatus LastExecutionStatus => m_lastExecutionStatus;
        
        protected override void Awake()
        {
            base.Awake();
            Variables = GetComponent<IVariables>();
            GameDataEngineVariables = GetComponent<GameDataEngineVariables>();
            
            if (hasGraph && nest.macro != null)
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

        public void Switch(BehaviorTreeGraph behaviorTreeGraph)
        {
            m_lastExecutionStatus = ExecutionStatus.Running;
            m_graph = behaviorTreeGraph;
            nest.SwitchToEmbed(behaviorTreeGraph);
        }

        private void Start()
        {
            if (hasGraph && m_graph != null)
            {
                m_graph.OnEnter();
            }
        }

        private void Update()
        {
            if (hasGraph && m_graph != null)
            {
                m_lastExecutionStatus = m_graph.OnUpdate();
            }
        }

        public override BehaviorTreeGraph DefaultGraph()
        {
            return new BehaviorTreeGraph();
        }
    }
}