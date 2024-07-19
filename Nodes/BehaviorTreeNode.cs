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
        public IBehaviorTreePortCollection<BehaviorTreeControlInput> controlInputs { get; }

        [DoNotSerialize]
        public IBehaviorTreePortCollection<BehaviorTreeControlOutput> controlOutputs { get; }

        [DoNotSerialize]
        public IBehaviorTreePortCollection<BehaviorTreeValueInput> valueInputs { get; }

        [DoNotSerialize]
        public IBehaviorTreePortCollection<BehaviorTreeValueOutput> valueOutputs { get; }

        [DoNotSerialize]
        public IBehaviorTreePortCollection<BehaviorTreeInvalidInput> invalidInputs { get; }

        [DoNotSerialize]
        public IBehaviorTreePortCollection<BehaviorTreeInvalidOutput> invalidOutputs { get; }

        [DoNotSerialize]
        public IEnumerable<IBehaviorTreeInputPort> inputs => LinqUtility.Concat<IBehaviorTreeInputPort>(controlInputs, valueInputs, invalidInputs);

        [DoNotSerialize]
        public IEnumerable<IBehaviorTreeOutputPort> outputs => LinqUtility.Concat<IBehaviorTreeOutputPort>(controlOutputs, valueOutputs, invalidOutputs);

        [DoNotSerialize]
        public IEnumerable<IBehaviorTreeInputPort> validInputs => LinqUtility.Concat<IBehaviorTreeInputPort>(controlInputs, valueInputs);

        [DoNotSerialize]
        public IEnumerable<IBehaviorTreeOutputPort> validOutputs => LinqUtility.Concat<IBehaviorTreeOutputPort>(controlOutputs, valueOutputs);

        [DoNotSerialize]
        public IEnumerable<IBehaviorTreePort> ports => LinqUtility.Concat<IBehaviorTreePort>(inputs, outputs);

        [DoNotSerialize]
        public IEnumerable<IBehaviorTreePort> invalidPorts => LinqUtility.Concat<IBehaviorTreePort>(invalidInputs, invalidOutputs);

        [DoNotSerialize]
        public IEnumerable<IBehaviorTreePort> validPorts => LinqUtility.Concat<IBehaviorTreePort>(validInputs, validOutputs);

        public event Action onPortsChanged;
        public IConnectionCollection<IBehaviorTreeRelation, IBehaviorTreePort, IBehaviorTreePort> relations { get; private set; }
        public IEnumerable<IBehaviorTreeConnection> connections { get; }
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
            throw new NotImplementedException();
        }

        public void RemoveUnconnectedInvalidPorts()
        {
            throw new NotImplementedException();
        }

        public Dictionary<string, object> defaultValues { get; }

        protected BehaviorTreeValueInput ValueInput<T>(string key)
        {
            return ValueInput(typeof(T), key);
        }
        
        protected BehaviorTreeValueInput ValueInput(Type type, string key)
        {
            //EnsureUniqueInput(key);
            var port = new BehaviorTreeValueInput(key, type);
            valueInputs.Add(port);
            return port;
        }
        
        protected BehaviorTreeValueOutput ValueOutput(Type type, string key)
        {
            //EnsureUniqueOutput(key);
            var port = new BehaviorTreeValueOutput(key, type);
            valueOutputs.Add(port);
            return port;
        }

        protected BehaviorTreeValueOutput ValueOutput(Type type, string key, Func<Flow, object> getValue)
        {
            //EnsureUniqueOutput(key);
            var port = new BehaviorTreeValueOutput(key, type, getValue);
            valueOutputs.Add(port);
            return port;
        }

        protected BehaviorTreeValueOutput ValueOutput<T>(string key)
        {
            return ValueOutput(typeof(T), key);
        }

        protected BehaviorTreeValueOutput ValueOutput<T>(string key, Func<Flow, T> getValue)
        {
            return ValueOutput(typeof(T), key, (recursion) => getValue(recursion));
        }
        
        protected BehaviorTreeNode() : base()
        {
            controlInputs = new BehaviorTreePortCollection<BehaviorTreeControlInput>(this);
            controlOutputs = new BehaviorTreePortCollection<BehaviorTreeControlOutput>(this);
            valueInputs = new BehaviorTreePortCollection<BehaviorTreeValueInput>(this);
            valueOutputs = new BehaviorTreePortCollection<BehaviorTreeValueOutput>(this);
            invalidInputs = new BehaviorTreePortCollection<BehaviorTreeInvalidInput>(this);
            invalidOutputs = new BehaviorTreePortCollection<BehaviorTreeInvalidOutput>(this);
            
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

