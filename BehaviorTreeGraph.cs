using System.Collections.Generic;
using System.Linq;
using Platinio.GraphCore;
using Unity.VisualScripting;
using UnityEngine;

namespace Platinio.BehaviorTree
{
    [SerializationVersion("A")]
    public class BehaviorTreeGraph : BaseGraph<BehaviorTreeGraph, BehaviorTreeNode, BehaviorTreeTransition>
    {
        
         private const string DefinitionRemoveWarningTitle = "Remove Port Definition";

        private const string DefinitionRemoveWarningMessage = "Removing this definition will break any existing connection to this port. Are you sure you want to continue?";

        
        [DoNotSerialize]
        public GraphConnectionCollection<PortControlConnection, ControlOutput, ControlInput> controlConnections { get; private set; }

        [DoNotSerialize]
        public GraphConnectionCollection<PortValueConnection, ValueOutput, ValueInput> valueConnections { get; private set; }

        [DoNotSerialize]
        public GraphConnectionCollection<PortInvalidConnection, IOutputPort, IInputPort> invalidConnections { get; private set; }

        [Serialize]
        [InspectorLabel("Trigger Inputs")]
        [InspectorWide(true)]
        [WarnBeforeRemoving(DefinitionRemoveWarningTitle, DefinitionRemoveWarningMessage)]
        public PortDefinitionCollection<InputPortDefinition> controlInputDefinitions { get; private set; }

        [Serialize]
        [InspectorLabel("Trigger Outputs")]
        [InspectorWide(true)]
        [WarnBeforeRemoving(DefinitionRemoveWarningTitle, DefinitionRemoveWarningMessage)]
        public PortDefinitionCollection<PortControlOutputDefinition> controlOutputDefinitions { get; private set; }

        [Serialize]
        [InspectorLabel("Data Inputs")]
        [InspectorWide(true)]
        [WarnBeforeRemoving(DefinitionRemoveWarningTitle, DefinitionRemoveWarningMessage)]
        public PortDefinitionCollection<ValueInputDefinition> valueInputDefinitions { get; private set; }

        [Serialize]
        [InspectorLabel("Data Outputs")]
        [InspectorWide(true)]
        [WarnBeforeRemoving(DefinitionRemoveWarningTitle, DefinitionRemoveWarningMessage)]
        public PortDefinitionCollection<ValueOutputDefinition> valueOutputDefinitions { get; private set; }

        public IEnumerable<IPortDefinition> validPortDefinitions =>
            LinqUtility.Concat<IPortDefinition>(controlInputDefinitions,
                    controlOutputDefinitions,
                    valueInputDefinitions,
                    valueOutputDefinitions)
                .Where(upd => upd.isValid)
                .DistinctBy(upd => upd.key);
        
        
        [Serialize] 
        private Entry entryNode;

        public Entry EntryNode => entryNode;
        
        public BehaviorTreeGraph() : base()
        {
            entryNode = new Entry();
            entryNode.Position = new Rect(new Vector2(-100, -15), entryNode.StartingSize);

            Nodes.Add(entryNode);
            
            controlConnections = new GraphConnectionCollection<PortControlConnection, ControlOutput, ControlInput>(this);
            valueConnections = new GraphConnectionCollection<PortValueConnection, ValueOutput, ValueInput>(this);
            invalidConnections = new GraphConnectionCollection<PortInvalidConnection, IOutputPort, IInputPort>(this);
         
            //elements.Include(units);
            elements.Include(controlConnections);
            elements.Include(valueConnections);
            elements.Include(invalidConnections);
            //elements.Include(groups);
            //elements.Include(sticky);

            controlInputDefinitions = new PortDefinitionCollection<InputPortDefinition>();
            controlOutputDefinitions = new PortDefinitionCollection<PortControlOutputDefinition>();
            valueInputDefinitions = new PortDefinitionCollection<ValueInputDefinition>();
            valueOutputDefinitions = new PortDefinitionCollection<ValueOutputDefinition>();
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
            foreach (var nodeTransition in Transitions)
            {
                if (nodeTransition.source is ContainerNode containerNode)
                {
                    containerNode.AddChild(nodeTransition.destination);
                }
            }
        }

        public int CountTransitionsFromNode(BehaviorTreeNode node)
        {
            int transitionCount = 0;
            foreach (var nodeTransition in Transitions)
            {
                if (nodeTransition.source == node)
                {
                    transitionCount++;
                }
            }

            return transitionCount;
        }

        public bool TransitionExist(BehaviorTreeNode from, BehaviorTreeNode to)
        {
            foreach (var nodeTransition in Transitions)
            {
                if (from == nodeTransition.source && to == nodeTransition.destination)
                {
                    return true;
                }
            }

            return false;
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
            var result = EntryNode.OnUpdateInternal();
            if (result == ExecutionStatus.Failure || result == ExecutionStatus.Success)
            {
                EntryNode.OnNodeExit();
                return result;
            }

            return ExecutionStatus.Running;
        }

        public Vector2 GetEntryNodeOffset()
        {
            Vector2 entryOffset = new Vector2();

            foreach (var node in Nodes)
            {
                if (node is Entry entryNode)
                {
                    Vector2 sizeOffset = new Vector2(entryNode.Position.size.x * 0.5f, entryNode.Position.size.y * 0.5f);
                    sizeOffset += new Vector2(0, -50.0f);
                    entryOffset = (entryNode.Position.position + sizeOffset) * -1;
                }
            }

            return entryOffset;
        }
        
    }
}
