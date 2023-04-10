using System.Collections.Generic;
using Platinio.GraphCore;
using Unity.VisualScripting;
using UnityEngine;

namespace Platinio.BehaviourTree
{
    [SerializationVersion("A")]
    public class BehaviourTreeGraph : BaseGraph<BehaviourTreeGraph, BehaviourTreeNode, BehaviourTreeTransition>
    {
        [Serialize] 
        private Entry m_entryNode;

        public Entry EntryNode => m_entryNode;
        
        public BehaviourTreeGraph()
        {
            m_entryNode = new Entry();
            m_entryNode.Position = new Rect(new Vector2(-100, -15), m_entryNode.StartingSize);

            Nodes.Add(m_entryNode);
        }

        public override IGraphData CreateData()
        {
            return new BehaviourTreeGraphData(this);
        }

        public bool IsListening(GraphPointer pointer)
        {
            return pointer.GetGraphData<BehaviourTreeGraphData>().isListening;
        }

        #region Elements
        [DoNotSerialize]
        public GraphElementCollection<GraphGroup> Groups { get; internal set; }
        #endregion

        public static BehaviourTreeGraph CreateEmpty()
        {
            var stateGraph = new BehaviourTreeGraph();

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

        public IEnumerable<T> GetTaskNodesOfType<T>() where T : BehaviourTreeNode
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
