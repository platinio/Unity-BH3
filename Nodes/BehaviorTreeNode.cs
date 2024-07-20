using System;
using System.Collections.Generic;
using Platinio.GraphCore;
using Unity.VisualScripting;
using UnityEngine;

namespace Platinio.BehaviorTree
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

        public event Action onPortsChanged;
        public IConnectionCollection<IPortRelation, IPort, IPort> relations { get; private set; }
        public IEnumerable<IPortConnection> connections { get; }
        public bool isControlRoot { get; }
        public Vector2 position { get; set; }

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
            throw new NotImplementedException();
        }

        public bool canDefine { get; }
        public bool isDefined { get; }
        public bool failedToDefine { get; }
        public Exception definitionException { get; }

        public virtual void Define()
        {
           
        }

        public void EnsureDefined()
        {
            
        }

        public void RemoveUnconnectedInvalidPorts()
        {
            
        }

        public Dictionary<string, object> defaultValues { get; }

        protected ValueInput ValueInput<T>(string key)
        {
            return ValueInput(typeof(T), key);
        }
        
        protected ValueInput ValueInput(Type type, string key)
        {
            //EnsureUniqueInput(key);
            var port = new ValueInput(key, type);
            valueInputs.Add(port);
            return port;
        }
        
        protected ValueOutput ValueOutput(Type type, string key)
        {
            //EnsureUniqueOutput(key);
            var port = new ValueOutput(key, type);
            valueOutputs.Add(port);
            return port;
        }

        protected ValueOutput ValueOutput(Type type, string key, Func<Flow, object> getValue)
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

        protected ValueOutput ValueOutput<T>(string key, Func<Flow, T> getValue)
        {
            return ValueOutput(typeof(T), key, (recursion) => getValue(recursion));
        }
        
        protected BehaviorTreeNode() : base()
        {
            controlInputs = new PortCollection<ControlInput>(this);
            controlOutputs = new PortCollection<ControlOutput>(this);
            valueInputs = new PortCollection<ValueInput>(this);
            valueOutputs = new PortCollection<ValueOutput>(this);
            invalidInputs = new PortCollection<InvalidInput>(this);
            invalidOutputs = new PortCollection<InvalidOutput>(this);
            
            //relations = new ConnectionCollection<IBehaviorTreeNodeRelation, IUnitPort, IUnitPort>();

            defaultValues = new Dictionary<string, object>();
        }


        private BehaviorTreeMachineDebug machineDebug;
        protected BehaviorTreeMachine BehaviorTreeMachine => Machine as BehaviorTreeMachine;

        public virtual int MaxChildrenLimit => 0;
        
        protected GameObject GetTargetGameObject(GameObjectBlackboardVariable gameObjectVariable)
        {
            var target = gameObjectVariable.GetValue(BehaviorTreeMachine);
            return target == null ? gameObject : target;
        }

        protected BehaviorTreeMachineDebug GetMachineDebug()
        {
            if (machineDebug == null)
            {
                machineDebug = gameObject.GetComponent<BehaviorTreeMachineDebug>();
            }

            return machineDebug;
        }

        public override void OnNodeEnter()
        {
            base.OnNodeEnter();
            
#if UNITY_EDITOR
            var debugComponent = GetMachineDebug();
            if (debugComponent == null) return;
            
            debugComponent.PushNodeToCallStack(this);
#endif
        }

        public override void OnNodeExit()
        {
            base.OnNodeExit();
            
#if UNITY_EDITOR
            var debugComponent = GetMachineDebug();
            if (debugComponent == null) return;
            
            debugComponent.PopCallStack();
#endif
        }
    }
}

