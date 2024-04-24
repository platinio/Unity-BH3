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
        public BehaviorTreeGraphAsset GraphAsset => nest.macro;
        
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
                
                OverrideGraphAndSubGraphVariables(graphInstance);
                behaviorTreeGraph.OnAwake();
            }
        }

        private void OverrideGraphAndSubGraphVariables(BehaviorTreeGraphAsset graphAsset)
        {
            OverrideGraphVariables(graphAsset);

            foreach (var behaviorTreeNode in graphAsset.graph.Nodes)
            {
                if (behaviorTreeNode is RunBehaviorTreeGraphNode runBehaviorTreeGraphNode)
                {
                    OverrideGraphAndSubGraphVariables(runBehaviorTreeGraphNode.BehaviorTreeGraphAssetInstance);
                }
            }
        }

        private void OverrideGraphVariables(BehaviorTreeGraphAsset graphAsset)
        {
            foreach (var variableDeclaration in Variables.declarations)
            {
                graphAsset.declarations.Set(variableDeclaration.name, variableDeclaration.value);
            }
        }

        public void Switch(BehaviorTreeGraphAsset behaviorTreeGraphAsset)
        {
            graphInstance = behaviorTreeGraphAsset;
            lastExecutionStatus = ExecutionStatus.Running;
            behaviorTreeGraph = behaviorTreeGraphAsset.graph;
            
            var nodes = behaviorTreeGraph.Nodes;

            foreach (var node in nodes)
            {
                node.SetMachine(this);
            }

            OverrideGraphAndSubGraphVariables(behaviorTreeGraphAsset);
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

#if UNITY_EDITOR
                foreach (var node in behaviorTreeGraph.Nodes)
                {
                    node.CanvasUpdate();
                }
#endif
                if (lastExecutionStatus == ExecutionStatus.Success || lastExecutionStatus == ExecutionStatus.Failure) return;
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