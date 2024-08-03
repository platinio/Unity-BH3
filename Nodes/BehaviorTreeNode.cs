using System;
using System.Collections.Generic;
using System.Linq;
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
        
        [DoNotSerialize]
        public IEnumerable<IPortConnection> connections => ports.SelectMany(p => p.connections);
        
        [DoNotSerialize]
        public virtual bool isControlRoot { get; protected set; } = false;
        
        [Serialize]
        public Vector2 position { get; set; }

        public virtual bool ShowIcon => false;
        
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
            return default;
        }

        [DoNotSerialize]
        public virtual bool canDefine => true;
        [DoNotSerialize]
        public bool isDefined { get; private set; }
        public bool failedToDefine { get; }
        public Exception definitionException { get; }

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


        private BehaviorTreeMachineDebug machineDebug;
        protected BehaviorTreeMachine BehaviorTreeMachine => Machine as BehaviorTreeMachine;

        public virtual int MaxChildrenLimit => 0;
        
        protected GameObject GetTargetGameObject(GameObjectBlackboardVariable gameObjectVariable)
        {
            var target = gameObjectVariable.GetValue(BehaviorTreeMachine);
            return target == null ? gameObject : target;
        }
        
        protected GameObject GetTargetGameObject(ValueInput valueInput)
        {
            var target = valueInput.GetValue() as GameObject;
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

