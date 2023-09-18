using GameDataEngine;
using Platinio.GraphCore;
using Unity.VisualScripting;
using UnityEngine;

namespace Platinio.BehaviorTree
{
    [RequireComponent(typeof(Variables))]
    public class BehaviorTreeMachine : BaseMachine<BehaviorTreeGraph, BehaviorTreeGraphAsset, BehaviorTreeNode, BehaviorTreeTransition>
    {
        private BehaviorTreeGraph behaviorTreeGraph;
        private ExecutionStatus lastExecutionStatus = ExecutionStatus.Inactive;

        public ExecutionStatus LastExecutionStatus => lastExecutionStatus;
        
        protected override void Awake()
        {
            base.Awake();
            Variables = GetComponent<Variables>();
            GameDataEngineVariables = GetComponent<GameDataEngineVariables>();
            
            if (hasGraph && nest.macro != null)
            {
                nest.SwitchToEmbed(Instantiate(nest.macro).graph);
                behaviorTreeGraph = nest.embed;
                
                var nodes = behaviorTreeGraph.Nodes;

                foreach (var node in nodes)
                {
                    node.SetMachine(this);
                }
                
                behaviorTreeGraph.OnAwake();
            }
        }

        public void Switch(BehaviorTreeGraph behaviorTreeGraph)
        {
            lastExecutionStatus = ExecutionStatus.Running;
            this.behaviorTreeGraph = behaviorTreeGraph;
            nest.SwitchToEmbed(behaviorTreeGraph);
        }

        private void Start()
        {
            if (hasGraph && behaviorTreeGraph != null)
            {
                behaviorTreeGraph.OnEnter();
            }
        }

        private void Update()
        {
            if (hasGraph && behaviorTreeGraph != null)
            {
                lastExecutionStatus = behaviorTreeGraph.OnUpdate();
            }
        }

        public override BehaviorTreeGraph DefaultGraph()
        {
            return new BehaviorTreeGraph();
        }
    }
}