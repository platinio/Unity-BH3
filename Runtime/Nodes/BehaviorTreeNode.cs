using System;
using System.Collections.Generic;
using System.Linq;
using ArcaneOnyx.GraphCore;
using Unity.VisualScripting;
using UnityEngine;
using UnityEngine.AI;

namespace ArcaneOnyx.BehaviorTree
{
    /// <summary>
    /// Base class for all behavior tree nodes
    /// </summary>
    public class BehaviorTreeNode : BaseGraphNode<BehaviorTreeGraph, BehaviorTreeNode, BehaviorTreeTransition>, IBehaviorTreeNode
    {
        [DoNotSerialize]
        public IPortCollection<ControlInput> controlInputs { get; }

        [DoNotSerialize]
        public IPortCollection<ControlOutput> controlOutputs { get; }

        [DoNotSerialize]
        public IPortCollection<ValueInput> valueInputs { get; }

        [DoNotSerialize]
        public IPortCollection<ValueOutput> valueOutputs { get; }

        [DoNotSerialize]
        public IPortCollection<InvalidInput> invalidInputs { get; }

        [DoNotSerialize]
        public IPortCollection<InvalidOutput> invalidOutputs { get; }

        [DoNotSerialize]
        public IEnumerable<IInputPort> inputs => LinqUtility.Concat<IInputPort>(controlInputs, valueInputs, invalidInputs);

        [DoNotSerialize]
        public IEnumerable<IOutputPort> outputs => LinqUtility.Concat<IOutputPort>(controlOutputs, valueOutputs, invalidOutputs);

        [DoNotSerialize]
        public IEnumerable<IInputPort> validInputs => LinqUtility.Concat<IInputPort>(controlInputs, valueInputs);

        [DoNotSerialize]
        public IEnumerable<IOutputPort> validOutputs => LinqUtility.Concat<IOutputPort>(controlOutputs, valueOutputs);

        [DoNotSerialize]
        public IEnumerable<IPort> ports => LinqUtility.Concat<IPort>(inputs, outputs);

        [DoNotSerialize]
        public IEnumerable<IPort> invalidPorts => LinqUtility.Concat<IPort>(invalidInputs, invalidOutputs);

        [DoNotSerialize]
        public IEnumerable<IPort> validPorts => LinqUtility.Concat<IPort>(validInputs, validOutputs);

        [DoNotSerialize]
        private List<ConditionalExecution> conditionalExecutions = new();

        [DoNotSerialize] 
        private Dictionary<Guid, int> conditionalExecutionIndexCache = new();

        public virtual bool CanUseConditionalExecutions => true;
        public event Action onPortsChanged;
        public IConnectionCollection<IPortRelation, IPort, IPort> relations { get; private set; }
        
        [DoNotSerialize]
        public IEnumerable<IPortConnection> connections => ports.SelectMany(p => p.connections);
        
        [DoNotSerialize]
        public virtual bool isControlRoot { get; protected set; } = false;
        
        [Serialize]
        public Vector2 position { get; set; }

        public virtual bool ShowIcon => false;

        public virtual bool CanCopy => true;
        public virtual bool CanDuplicate => true;
        public virtual bool CanCut => true;
        public override bool  DrawInSubTree => true;

        public virtual string Description => string.Empty;
        
        public void PortsChanged()
        {
            onPortsChanged?.Invoke();
        }
        
        public override void AfterAdd()
        {
            // Important to define before notifying instances
            Define();

            base.AfterAdd();
        }

        public IGraphElementDebugData CreateDebugData()
        {
            return new Unit.DebugData();
        }

        [DoNotSerialize]
        public virtual bool canDefine => true;
        [DoNotSerialize]
        public bool isDefined { get; private set; }
        public bool failedToDefine { get; }
        public Exception definitionException { get; }

       
        public void AddConditionalExecution(ConditionalExecution conditionalExecution)
        {
            conditionalExecutions.Add(conditionalExecution);
        }

        /// <summary>
        /// Drops every guard armed on this node, so <see cref="BehaviorTreeGraph.OnAwake"/> can rebuild the
        /// list from the graph rather than append to whatever a previous call left behind.
        /// <para>
        /// Rebuilding from source is what makes arming idempotent: a guard deleted between two awakes
        /// disappears, which a dedupe-on-insert scheme would not manage. See
        /// <see cref="BehaviorTreeGraph.AddConditionalExecutionNodes"/> for why that matters.
        /// </para>
        /// </summary>
        public void ClearConditionalExecutions()
        {
            conditionalExecutions.Clear();
        }

        public void Define()
        {
            var preservation = NodePreservation.Preserve(this);

            // A node needs to undefine even if it wasn't defined,
            // because there might be invalid ports and connections
            // that we need to clear to avoid duplicates on definition.
            Undefine();

            if (canDefine)
            {
                try
                {
                    Definition();
                    isDefined = true;
                    //definitionException = null;
                    AfterDefine();
                }
                catch (Exception ex)
                {
                    Undefine();
                    //definitionException = ex;
                    Debug.LogWarning($"Failed to define {this}:\n{ex}");
                }
            }

            preservation.RestoreTo(this);
        }

        protected virtual void Definition() { }
        
        protected virtual void AfterDefine() { }

        private void Undefine()
        {
            // Because a node is always undefined on definition,
            // even if it wasn't defined before, we make sure the user
            // code for undefinition can safely presume it was defined.
            if (isDefined)
            {
                BeforeUndefine();
            }

            Disconnect();
            defaultValues?.Clear();
            controlInputs?.Clear();
            controlOutputs?.Clear();
            valueInputs?.Clear();
            valueOutputs?.Clear();
            invalidInputs?.Clear();
            invalidOutputs?.Clear();
            relations?.Clear();
            isDefined = false;
        }
        
        protected virtual void BeforeUndefine() { }
        
        public override void BeforeRemove()
        {
            base.BeforeRemove();
            Disconnect();
        }

        public void EnsureDefined()
        {
            
        }

        public void RemoveUnconnectedInvalidPorts()
        {
            
        }
        
        public void Disconnect()
        {
            // Can't use a foreach because invalid ports may get removed as they disconnect
            while (ports.Any(p => p.hasAnyConnection))
            {
                ports.First(p => p.hasAnyConnection).Disconnect();
            }
        }

        [Serialize]
        public Dictionary<string, object> defaultValues { get; private set; }

        protected ValueInput ValueInput<T>(string key)
        {
            return ValueInput(typeof(T), key);
        }
        
        protected ValueInput ValueInput<T>(string key, object defaultValue)
        {
            return ValueInput(typeof(T), key, defaultValue);
        }
        
        protected ValueInput ValueInput(Type type, string key)
        {
            //EnsureUniqueInput(key);
            var port = new ValueInput(key, type);
            valueInputs.Add(port);
            return port;
        }
        
        protected ValueInput ValueInput(Type type, string key, object defaultValue)
        {
            //EnsureUniqueInput(key);
            var port = new ValueInput(key, type);
            valueInputs.Add(port);
            port.SetDefaultValue(defaultValue);
            return port;
        }
        
        protected ValueOutput ValueOutput(Type type, string key)
        {
            //EnsureUniqueOutput(key);
            var port = new ValueOutput(key, type);
            valueOutputs.Add(port);
            return port;
        }

        protected ValueOutput ValueOutput(Type type, string key, GetPortValue getValue)
        {
            //EnsureUniqueOutput(key);
            var port = new ValueOutput(key, type, getValue);
            valueOutputs.Add(port);
            return port;
        }

        protected ValueOutput ValueOutput<T>(string key)
        {
            return ValueOutput(typeof(T), key);
        }

        protected ValueOutput ValueOutput<T>(string key, GetPortValue getValue)
        {
            return ValueOutput(typeof(T), key, getValue);
        }
        
        protected BehaviorTreeNode() : base()
        {
            controlInputs = new PortCollection<ControlInput>(this);
            controlOutputs = new PortCollection<ControlOutput>(this);
            valueInputs = new PortCollection<ValueInput>(this);
            valueOutputs = new PortCollection<ValueOutput>(this);
            invalidInputs = new PortCollection<InvalidInput>(this);
            invalidOutputs = new PortCollection<InvalidOutput>(this);
            relations = new ConnectionCollection<IPortRelation, IPort, IPort>();
            defaultValues = new Dictionary<string, object>();
        }

        protected BehaviorTreeMachine BehaviorTreeMachine => Machine as BehaviorTreeMachine;

        [DoNotSerialize]
        private BehaviorTreeVariableScope variableScope;

        /// <summary>
        /// The variables this node can see. Assigned alongside the machine when the graph loads, so a node
        /// inside a sub-tree resolves against that sub-tree's own instance before searching outward — see
        /// <see cref="BehaviorTreeVariableScope"/>.
        /// <para>
        /// Falls back to a scope over the root tree's declarations when nothing assigned one, so any path that
        /// loads a graph without calling <see cref="SetVariableScope"/> behaves exactly as it did before.
        /// </para>
        /// </summary>
        [DoNotSerialize]
        public BehaviorTreeVariableScope VariableScope
        {
            get
            {
                if (variableScope != null) return variableScope;

                var instance = BehaviorTreeMachine?.GraphInstance;
                if (instance == null) return null;

                variableScope = new BehaviorTreeVariableScope(instance.declarations);
                return variableScope;
            }
        }

        /// <summary>
        /// Hands this node the scope it resolves variables against. Mirrors <c>SetMachine</c> — the machine
        /// calls it across the root graph, and <see cref="RunBehaviorTreeGraphNode"/> overrides it to build a
        /// child scope and pass that one down instead.
        /// </summary>
        public virtual void SetVariableScope(BehaviorTreeVariableScope scope)
        {
            variableScope = scope;
        }

        [DoNotSerialize]
        private Debugging.BehaviorTreeFlightRecorder flightRecorder;

        /// <summary>
        /// Where this node reports what it did, or null when nothing is recording.
        /// <para>
        /// Injected rather than fetched from the machine so a node can be recorded without one. The
        /// composites and decorators drive their children purely through the lifecycle hooks and never touch
        /// the machine, which is what lets a tree be ticked in an edit-mode test — and a recorder that only
        /// worked with a live machine could not be tested by the same means.
        /// </para>
        /// </summary>
        [DoNotSerialize]
        public Debugging.BehaviorTreeFlightRecorder FlightRecorder => flightRecorder;

        /// <summary>
        /// Hands this node the recorder to report to. Mirrors <see cref="SetVariableScope"/>;
        /// <see cref="RunBehaviorTreeGraphNode"/> overrides it to reach the nodes inside its branch.
        /// </summary>
        public virtual void SetFlightRecorder(Debugging.BehaviorTreeFlightRecorder recorder)
        {
            flightRecorder = recorder;
        }

        /// <summary>
        /// What an embedded Visual Scripting graph sees, as the flat collection those entry points take.
        /// Collapses the scope chain so a script graph inside a branch reads that branch's values first and
        /// the agent's underneath — matching what every other read in the tree resolves to.
        /// <para>
        /// A node in the root tree hands over the root declarations unchanged, so nothing is copied in the
        /// common case; only a node inside a sub-tree pays for the merge.
        /// </para>
        /// </summary>
        [DoNotSerialize]
        protected VariableDeclarations ScriptGraphVariables =>
            VariableScope?.Flatten() ?? BehaviorTreeMachine?.GraphInstance?.declarations;

        public virtual int MaxChildrenLimit => 0;
        
        protected GameObject GetTargetGameObject(GameObjectBlackboardVariable gameObjectVariable)
        {
            var target = gameObjectVariable.GetValue(BehaviorTreeMachine, VariableScope);
            return target == null ? gameObject : target;
        }
        
        protected T GetComponent<T>(ValueInput valueInput) where T : Component
        {
            var component = valueInput.GetComponent<T>();
            if (component == null) return gameObject.GetComponent<T>();

            return component;
        }
        
        public IReadOnlyCollection<ConditionalExecution> ConditionalExecutions => conditionalExecutions;
        
        public int GetConditionalIndex(ConditionalExecution conditionalExecution)
        {
            if (conditionalExecutionIndexCache.TryGetValue(conditionalExecution.guid, out int index))
            {
                return index;
            }

            index = 0;
            
            foreach (var graphElement in graph.elements)
            {
                if (graphElement is ConditionalExecution conditionalExecutionNode)
                {
                    if (conditionalExecutionNode.Owner == this)
                    {
                        if (conditionalExecutionNode == conditionalExecution) break;
                        index++;
                    }
                }
            }

            conditionalExecutionIndexCache[conditionalExecution.guid] = index;
            return index;
        }
        
        public int CountConditionalExection()
        {
            int count = 0;
            
            foreach (var graphElement in graph.elements)
            {
                if (graphElement is ConditionalExecution conditionalExecutionNode)
                {
                    if (conditionalExecutionNode.Owner == this)
                    {
                        count++;
                    }
                }
            }

            return count;
        }

        public void ClearConditionalExecutionInexCache()
        {
            conditionalExecutionIndexCache.Clear();
        }

        public sealed override void OnNodeEnter()
        {
            foreach (var conditionalExecution in conditionalExecutions)
            {
                bool passed = conditionalExecution.EvaluateInternal();
                Debugging.BehaviorTreeRecorder.GuardEval(this, conditionalExecution, passed);

                if (!passed)
                {
                    // Never started, as opposed to started and killed. The two read differently to whoever is
                    // asking why this branch did not happen, so they are recorded as different events.
                    Debugging.BehaviorTreeRecorder.NodeSkipped(this, conditionalExecution);
                    return;
                }
            }

            Debugging.BehaviorTreeRecorder.NodeEnter(this);

            base.OnNodeEnter();
        }

        /// <summary>
        /// Records the exit and the status it ended on.
        /// <para>
        /// <see cref="BaseGraphNode{TGraph,TNode,TNodeTransition}.OnNodeExit"/> is called on nodes that never
        /// ran and does nothing for them, so <see cref="BaseGraphNode{TGraph,TNode,TNodeTransition}.IsRunning"/>
        /// is read first — base clears it — and only a real exit is recorded. Without that the recording fills
        /// with exits for branches a selector never reached.
        /// </para>
        /// </summary>
        public sealed override void OnNodeExit()
        {
            bool wasRunning = IsRunning;

            base.OnNodeExit();

            if (wasRunning) Debugging.BehaviorTreeRecorder.NodeExit(this, LastExecutionStatus);
        }

        public sealed override ExecutionStatus OnUpdateInternal()
        {
            foreach (var conditionalExecution in conditionalExecutions)
            {
                bool passed = conditionalExecution.EvaluateInternal();
                Debugging.BehaviorTreeRecorder.GuardEval(this, conditionalExecution, passed);

                if (!passed)
                {
                    // The guard that did it is named here and nowhere else: by the time the parent composite
                    // sees the Failure, which guard caused it is gone.
                    //
                    // A composite ticks a child on the same frame it declined to enter it, so this also runs
                    // for nodes that never started. Only a node that was running can be aborted; the other
                    // case was already recorded as skipped by OnNodeEnter, and recording it twice would
                    // report every declined branch as though something had interrupted it.
                    if (IsRunning) Debugging.BehaviorTreeRecorder.NodeAborted(this, conditionalExecution);

                    LastExecutionStatus = ExecutionStatus.Failure;
                    return ExecutionStatus.Failure;
                }
            }

            return base.OnUpdateInternal();
        }
    }
}

