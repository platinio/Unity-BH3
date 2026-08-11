using System.Collections.Generic;
using System.Linq;
using ArcaneOnyx.GraphCore;
using Unity.VisualScripting;
using UnityEngine;
using GraphPointer = ArcaneOnyx.GraphCore.GraphPointer;
using Object = UnityEngine.Object;

namespace ArcaneOnyx.BehaviorTree
{
    [SerializationVersion("A")]
    public class BehaviorTreeGraph : BaseGraph<BehaviorTreeGraph, BehaviorTreeNode, BehaviorTreeTransition>
    {
        private const string DefinitionRemoveWarningTitle = "Remove Port Definition";
        private const string DefinitionRemoveWarningMessage = "Removing this definition will break any existing connection to this port. Are you sure you want to continue?";

        
        [DoNotSerialize]
        public GraphCore.GraphConnectionCollection<PortControlConnection, ControlOutput, ControlInput> controlConnections { get; private set; }

        [DoNotSerialize]
        public GraphCore.GraphConnectionCollection<PortValueConnection, ValueOutput, ValueInput> valueConnections { get; private set; }

        [DoNotSerialize]
        public GraphCore.GraphConnectionCollection<PortInvalidConnection, IOutputPort, IInputPort> invalidConnections { get; private set; }

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

        [SerializeAs(nameof(scriptGraphAssets))]
        private List<ScriptGraphAsset> scriptGraphAssets = new();

        public Entry EntryNode => entryNode;
      
        public BehaviorTreeGraph() : base()
        {
            entryNode = new Entry();
            entryNode.Position = new Rect(new Vector2(-96, -15), entryNode.StartingSize);

            Nodes.Add(entryNode);
            
            controlConnections = new GraphCore.GraphConnectionCollection<PortControlConnection, ControlOutput, ControlInput>(this);
            valueConnections = new GraphCore.GraphConnectionCollection<PortValueConnection, ValueOutput, ValueInput>(this);
            invalidConnections = new GraphCore.GraphConnectionCollection<PortInvalidConnection, IOutputPort, IInputPort>(this);
         
            elements.Include(controlConnections);
            elements.Include(valueConnections);
            elements.Include(invalidConnections);
           
            controlInputDefinitions = new PortDefinitionCollection<InputPortDefinition>();
            controlOutputDefinitions = new PortDefinitionCollection<PortControlOutputDefinition>();
            valueInputDefinitions = new PortDefinitionCollection<ValueInputDefinition>();
            valueOutputDefinitions = new PortDefinitionCollection<ValueOutputDefinition>();
        }

        public void DestroyUnusedScriptGraphAssets(BehaviorTreeGraphAsset graphAsset)
        {
            List<ScriptGraphAsset> unusedScriptGraphAssets = ScriptGraphAssetsRepository.Instance.GetScriptGraphAssets(graphAsset);
            if (unusedScriptGraphAssets == null) return;

            foreach (var graphElement in elements)
            {
                if (graphElement.scriptGraphAssets == null || graphElement.scriptGraphAssets.Count() == 0) continue;

                foreach (var scriptGraphAsset in graphElement.scriptGraphAssets)
                {
                    unusedScriptGraphAssets.Remove(scriptGraphAsset);
                }
            }

            for (int i = unusedScriptGraphAssets.Count - 1; i >= 0; i--)
            {
                ScriptGraphAssetsRepository.Instance.RemoveScriptGraphAsset(unusedScriptGraphAssets[i]);
                Object.DestroyImmediate(unusedScriptGraphAssets[i], true);
            }
        }

        public void AddScriptGraphAssets(BehaviorTreeGraphAsset asset, IEnumerable<ScriptGraphAsset> newScriptGraphAssets)
        {
            foreach (var scriptGraphAsset in newScriptGraphAssets)
            {
                ScriptGraphAssetsRepository.Instance.AddScriptGraphAsset(asset, scriptGraphAsset);
            }
        }
       

        public override GraphCore.IGraphData CreateData()
        {
            return new BehaviorTreeGraphData(this);
        }

        public bool IsListening(GraphPointer pointer)
        {
            return pointer.GetGraphData<BehaviorTreeGraphData>().isListening;
        }

        #region Elements
        [DoNotSerialize]
        public GraphCore.GraphElementCollection<GraphCore.GraphGroup> Groups { get; internal set; }
        #endregion

        public static BehaviorTreeGraph CreateEmpty()
        {
            return new BehaviorTreeGraph();
        }

        /// <summary>
        /// Rebuilds every container's child list from the graph's transitions.
        /// <para>
        /// Clears first, because this runs from <see cref="OnAwake"/> and <see cref="OnAwake"/> may run more
        /// than once — a test that awakens a graph the machine also awakens, editor tooling that warms a graph
        /// for inspection, or a future live-edit path that re-initialises by design. Appending on the second
        /// call gives every composite a duplicate of each branch.
        /// </para>
        /// </summary>
        public void ConvertTransitionNodesIntoTaskNodeChild()
        {
            foreach (var node in Nodes)
            {
                if (node is ContainerNode containerNode) containerNode.ClearChildren();
            }

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
            AddConditionalExecutionNodes();
            
            var nodes = Nodes;
            
            foreach (var node in nodes)
            {
                node.OnAwake();
            }
        }

        /// <summary>
        /// Arms every guard onto the node it names as its owner.
        /// <para>
        /// Clears each owner's list before rebuilding, so calling <see cref="OnAwake"/> twice arms each guard
        /// once rather than twice. Duplicate guards are semantically masked — an AND of the same predicate
        /// twice is the same boolean — which is exactly what makes the bug a landmine: nothing looks wrong
        /// while every guard evaluates twice per tick and any side effect in a guard's graph fires twice.
        /// </para>
        /// <para>
        /// Rebuilt from the graph rather than deduplicated on insert, because only a rebuild drops a guard
        /// that was deleted between two calls.
        /// </para>
        /// </summary>
        private void AddConditionalExecutionNodes()
        {
            List<ConditionalExecution> conditionalExecutions = new();

            foreach (var node in Nodes)
            {
                node.ClearConditionalExecutions();

                if (node is ConditionalExecution conditionalExecution)
                {
                    conditionalExecutions.Add(conditionalExecution);
                }
            }

            foreach (var conditionalExecution in conditionalExecutions)
            {
                foreach (var node in Nodes)
                {
                    if (conditionalExecution.Owner == node)
                    {
                        node.AddConditionalExecution(conditionalExecution);
                    }
                }
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
      
        public void OnLateUpdate()
        {
            foreach (var node in Nodes)
            {
                if (node.LastExecutionStatus == ExecutionStatus.Running) continue;
                node.OnLateUpdate();
            }
        }

        public void OnFixedUpdate()
        {
            foreach (var node in Nodes)
            {
                if (node.LastExecutionStatus == ExecutionStatus.Running) continue;
                node.OnFixedUpdate();
            }
        }

        public void OnDestroy()
        {
            foreach (var node in Nodes)
            {
                node.OnDestroy();
            }
        }
        
        public void OnApplicationPause(bool pauseStatus)
        {
            foreach (var node in Nodes)
            {
                node.OnApplicationPause(pauseStatus);
            }
        }

        public void OnDrawGizmos()
        {
            foreach (var node in Nodes)
            {
                node.OnDrawGizmos();
            }
        }
        
        public void OnDrawGizmosSelected()
        {
            foreach (var node in Nodes)
            {
                node.OnDrawGizmosSelected();
            }
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

        public void ThrowIfCausesRecursion()
        {
            foreach (var node in Nodes)
            {
                if (node is RunBehaviorTreeGraphNode runBehaviorTreeGraphNode)
                {
                    runBehaviorTreeGraphNode.ThrowIfCausesRecursion();
                }
            }
        }
    }
}
