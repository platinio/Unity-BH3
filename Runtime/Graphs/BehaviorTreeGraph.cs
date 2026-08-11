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
        /// Clears first, because nothing stops <see cref="OnAwake"/> running more than once and appending on
        /// the second call gives every composite a duplicate of each branch. No production path does today —
        /// <c>BehaviorTreeMachine.Awake</c> is the only caller — but the authoring docs tell test writers to
        /// call it by hand, and a second call there would otherwise be a silent doubling.
        /// </para>
        /// </summary>
        public void ConvertTransitionNodesIntoTaskNodeChild()
        {
            var childrenByParent = ChildrenByParentInPriorityOrder();

            foreach (var node in Nodes)
            {
                if (node is not ContainerNode containerNode) continue;

                containerNode.ClearChildren();

                if (!childrenByParent.TryGetValue(containerNode, out var children)) continue;

                foreach (var child in children)
                {
                    containerNode.AddChild(child);
                }
            }
        }

        /// <summary>
        /// Every parent's children, each list in the order they will be tried.
        /// <para>
        /// Buckets the transitions by source in one pass and orders each bucket, rather than scanning the whole
        /// transition list once per parent. Callers that want the whole graph should use this:
        /// <see cref="ChildrenInPriorityOrder"/> is O(transitions) per call, so asking it about every node
        /// makes the walk O(nodes × transitions) — fine for a one-off check on a single container, but not for
        /// the editor panels, which rebuild their view of a tree every frame while an agent is running.
        /// </para>
        /// </summary>
        public Dictionary<BehaviorTreeNode, List<BehaviorTreeNode>> ChildrenByParentInPriorityOrder()
        {
            var transitionsByParent = new Dictionary<BehaviorTreeNode, List<BehaviorTreeTransition>>();

            foreach (var transition in Transitions)
            {
                if (transition?.source == null || transition.destination == null) continue;

                if (!transitionsByParent.TryGetValue(transition.source, out var siblings))
                {
                    siblings = new List<BehaviorTreeTransition>();
                    transitionsByParent[transition.source] = siblings;
                }

                siblings.Add(transition);
            }

            var childrenByParent =
                new Dictionary<BehaviorTreeNode, List<BehaviorTreeNode>>(transitionsByParent.Count);

            foreach (var pair in transitionsByParent)
            {
                SortIntoPriorityOrder(pair.Value);

                var children = new List<BehaviorTreeNode>(pair.Value.Count);

                foreach (var transition in pair.Value)
                {
                    children.Add(transition.destination);
                }

                childrenByParent[pair.Key] = children;
            }

            return childrenByParent;
        }

        /// <summary>
        /// A container's outgoing transitions, in the order its children should be tried.
        /// <para>
        /// The serialized index is the priority. Canvas position is only the gesture that writes it, so
        /// tidying a layout no longer changes what the agent does, and a tree generated from code runs in the
        /// order its <c>Connect</c> calls asked for rather than the order its coordinates imply.
        /// </para>
        /// <para>
        /// Falls back to canvas order when the indices are not a real ordering. Every tree authored before
        /// this change stored index 0 on every transition — the canvas computed the index by counting edges
        /// between the same pair of nodes, which is always zero — so trusting those indices would collapse
        /// every container to a single priority. Reading them as "no order recorded" and using position
        /// instead is what makes an old asset run exactly as it did.
        /// </para>
        /// </summary>
        public List<BehaviorTreeTransition> ChildTransitionsInPriorityOrder(BehaviorTreeNode parent)
        {
            var siblings = new List<BehaviorTreeTransition>();

            ChildTransitionsInPriorityOrder(parent, siblings);

            return siblings;
        }

        /// <summary>
        /// The same ordering, filled into a list the caller owns. For anything drawn per node per repaint —
        /// the canvas priority badge — where allocating a list per node per frame is the actual cost rather
        /// than the scan.
        /// </summary>
        public void ChildTransitionsInPriorityOrder(BehaviorTreeNode parent, List<BehaviorTreeTransition> into)
        {
            into.Clear();

            foreach (var transition in Transitions)
            {
                if (transition?.source == parent && transition.destination != null) into.Add(transition);
            }

            SortIntoPriorityOrder(into);
        }

        /// <summary>
        /// Puts one parent's transitions into the order its children will be tried. The single place the
        /// priority rule is written; everything that needs an ordering goes through here.
        /// </summary>
        private static void SortIntoPriorityOrder(List<BehaviorTreeTransition> siblings)
        {
            if (RecordsAPriorityOrder(siblings))
            {
                siblings.Sort((left, right) => left.TransitionIndex.CompareTo(right.TransitionIndex));
            }
            else
            {
                siblings.Sort((left, right) =>
                    left.destination.Position.x.CompareTo(right.destination.Position.x));
            }
        }

        /// <summary>
        /// A node's children in the order they will be tried.
        /// <para>
        /// Reads the transitions rather than <see cref="ContainerNode.GetChildren"/>, so it answers on a tree
        /// that has never been awakened — which is what the dump and the why-panel need, since both run
        /// against assets nobody has played. Both call this rather than ordering for themselves: priority is
        /// one rule, and a debugging view that ordered children differently from the runtime would report the
        /// wrong branch as higher priority.
        /// </para>
        /// </summary>
        public List<BehaviorTreeNode> ChildrenInPriorityOrder(BehaviorTreeNode parent)
        {
            var children = new List<BehaviorTreeNode>();

            foreach (var transition in ChildTransitionsInPriorityOrder(parent))
            {
                children.Add(transition.destination);
            }

            return children;
        }

        /// <summary>
        /// Whether these transitions carry a genuine ordering — indices forming exactly 0..n-1, each once.
        /// <para>
        /// Contiguity is the test rather than "are they all distinct" because a gap means something was
        /// removed without renumbering, and duplicates mean two children claim one priority. In both cases
        /// the recorded order is not trustworthy and position is the better answer.
        /// </para>
        /// </summary>
        private static bool RecordsAPriorityOrder(List<BehaviorTreeTransition> siblings)
        {
            if (siblings.Count == 0) return false;

            var claimed = new bool[siblings.Count];

            foreach (var transition in siblings)
            {
                int index = transition.TransitionIndex;

                if (index < 0 || index >= siblings.Count || claimed[index]) return false;

                claimed[index] = true;
            }

            return true;
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
