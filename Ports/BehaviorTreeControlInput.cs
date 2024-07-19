using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Unity.VisualScripting;

namespace Platinio.BehaviorTree
{
    public sealed class BehaviorTreeControlInput : BehaviorTreePort<BehaviorTreeControlOutput, IBehaviorTreeOutputPort, BehaviorTreeControlConnection>
        , IBehaviorTreeControlPort, IBehaviorTreeInputPort
    {
        public BehaviorTreeControlInput(string key, Func<Flow, ControlOutput> action) : base(key)
        {
            Ensure.That(nameof(action)).IsNotNull(action);

            this.action = action;
        }

        public BehaviorTreeControlInput(string key, Func<Flow, IEnumerator> coroutineAction) : base(key)
        {
            Ensure.That(nameof(coroutineAction)).IsNotNull(coroutineAction);

            this.coroutineAction = coroutineAction;
        }

        public BehaviorTreeControlInput(string key, Func<Flow, ControlOutput> action, Func<Flow, IEnumerator> coroutineAction) : base(key)
        {
            Ensure.That(nameof(action)).IsNotNull(action);
            Ensure.That(nameof(coroutineAction)).IsNotNull(coroutineAction);

            this.action = action;
            this.coroutineAction = coroutineAction;
        }

        public bool supportsCoroutine => coroutineAction != null;

        public bool requiresCoroutine => action == null;

        internal readonly Func<Flow, ControlOutput> action;

        internal readonly Func<Flow, IEnumerator> coroutineAction;

        public override IEnumerable<BehaviorTreeControlConnection> validConnections => behaviorTreeNode?.graph?.controlConnections.WithDestination(this) ?? Enumerable.Empty<BehaviorTreeControlConnection>();

        public override IEnumerable<BehaviorTreeInvalidConnection> invalidConnections => behaviorTreeNode?.graph?.invalidConnections.WithDestination(this) ?? Enumerable.Empty<BehaviorTreeInvalidConnection>();

        public override IEnumerable<BehaviorTreeControlOutput> validConnectedPorts => validConnections.Select(c => c.source);

        public override IEnumerable<IBehaviorTreeOutputPort> invalidConnectedPorts => invalidConnections.Select(c => c.source);

        public bool isPredictable
        {
            get
            {
                using (var recursion = Recursion.New(1))
                {
                    return IsPredictable(recursion);
                }
            }
        }

        public bool IsPredictable(Recursion recursion)
        {
            if (!hasValidConnection)
            {
                return true;
            }

            if (!recursion?.TryEnter(this) ?? false)
            {
                return false;
            }

            var isPredictable = validConnectedPorts.All(cop => cop.IsPredictable(recursion));

            recursion?.Exit(this);

            return isPredictable;
        }

        public bool couldBeEntered
        {
            get
            {
                if (!isPredictable)
                {
                    throw new NotSupportedException();
                }

                if (!hasValidConnection)
                {
                    return false;
                }

                return validConnectedPorts.Any(cop => cop.couldBeEntered);
            }
        }

        public override bool CanConnectToValid(BehaviorTreeControlOutput port)
        {
            return true;
        }

        public override void ConnectToValid(BehaviorTreeControlOutput port)
        {
            var source = port;
            var destination = this;

            source.Disconnect();

            behaviorTreeNode.graph.controlConnections.Add(new BehaviorTreeControlConnection(source, destination));
        }

        public override void ConnectToInvalid(IBehaviorTreeOutputPort port)
        {
            ConnectInvalid(port, this);
        }

        public override void DisconnectFromValid(BehaviorTreeControlOutput port)
        {
            var connection = validConnections.SingleOrDefault(c => c.source == port);

            if (connection != null)
            {
                behaviorTreeNode.graph.controlConnections.Remove(connection);
            }
        }

        public override void DisconnectFromInvalid(IBehaviorTreeOutputPort port)
        {
            DisconnectInvalid(port, this);
        }

        public override IBehaviorTreePort CompatiblePort(IBehaviorTreeNode unit)
        {
            if (unit == this.behaviorTreeNode) return null;

            return unit.controlOutputs.FirstOrDefault();
        }
    }
}
