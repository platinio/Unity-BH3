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
        private BehaviorTreeGraphAsset graphInstance = null;
        
        public ExecutionStatus LastExecutionStatus => lastExecutionStatus;

        public BehaviorTreeGraphAsset GraphInstance => graphInstance;
        
        protected override void Awake()
        {
            base.Awake();
            Variables = GetComponent<Variables>();

            if (hasGraph && nest.macro != null)
            {
                graphInstance = Instantiate(nest.macro);
                nest.SwitchToEmbed(graphInstance.graph);
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

        protected override void OnDestroy()
        {
            if (graphInstance)
            {
                Destroy(graphInstance);
            }
        }

        public override BehaviorTreeGraph DefaultGraph()
        {
            return new BehaviorTreeGraph();
        }
    }
}