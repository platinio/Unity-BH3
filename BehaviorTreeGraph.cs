using System.Collections.Generic;
using Platinio.GraphCore;
using Unity.VisualScripting;
using UnityEngine;

namespace Platinio.BehaviorTree
{
    [SerializationVersion("A")]
    public class BehaviorTreeGraph : BaseGraph<BehaviorTreeGraph, BehaviorTreeNode, BehaviorTreeTransition>
    {
        [Serialize] 
        private Entry entryNode;

        public Entry EntryNode => entryNode;
        
        public BehaviorTreeGraph()
        {
            entryNode = new Entry();
            entryNode.Position = new Rect(new Vector2(-100, -15), entryNode.StartingSize);

            Nodes.Add(entryNode);
        }

        public override IGraphData CreateData()
        {
            return new BehaviorTreeGraphData(this);
        }

        public bool IsListening(GraphPointer pointer)
        {
            return pointer.GetGraphData<BehaviorTreeGraphData>().isListening;
        }

        #region Elements
        [DoNotSerialize]
        public GraphElementCollection<GraphGroup> Groups { get; internal set; }
        #endregion

        public static BehaviorTreeGraph CreateEmpty()
        {
            var stateGraph = new BehaviorTreeGraph();

            var entryNode = new Entry();
            entryNode.Position = new Rect(new Vector2(-100, -15), entryNode.StartingSize);

            stateGraph.Nodes.Add(entryNode);
            stateGraph.elements.Add(entryNode);
            return stateGraph;
        }

        public void ConvertTransitionNodesIntoTaskNodeChild()
        {
            foreach (var nodeTransitions in Transitions)
            {
                if (nodeTransitions.source is ContainerNode containerNode)
                {
                    containerNode.AddChild(nodeTransitions.destination);
                }
            }
        }

        public void SortContainerNodesChildren()
        {
            var containerNodes = GetTaskNodesOfType<ContainerNode>();
            foreach (var containerNode in containerNodes)
            {
                containerNode.SortChildren();
            }
        }

        public IEnumerable<T> GetTaskNodesOfType<T>() where T : BehaviorTreeNode
        {
            List<T> nodes = new List<T>();

            foreach (var taskNode in Nodes)
            {
                if (taskNode is T node) nodes.Add(node);
            }

            return nodes;
        }

        public override void OnAwake()
        {
            ConvertTransitionNodesIntoTaskNodeChild();
            SortContainerNodesChildren();
            
            var nodes = Nodes;

            foreach (var node in nodes)
            {
                node.OnAwake();
            }
        }

        public override void OnEnter()
        {
            EntryNode.OnNodeEnter();
        }

        public override ExecutionStatus OnUpdate()
        {
            var result = EntryNode.OnUpdate();
            if (result == ExecutionStatus.Failure || result == ExecutionStatus.Success)
            {
                EntryNode.OnNodeExit();
                return result;
            }

            return ExecutionStatus.Running;
        }
    }
}
