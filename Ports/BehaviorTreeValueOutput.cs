using System;
using System.Collections.Generic;
using System.Linq;
using Unity.VisualScripting;

namespace Platinio.BehaviorTree
{
    public sealed class BehaviorTreeValueOutput : BehaviorTreePort<BehaviorTreeValueInput, IBehaviorTreeInputPort, BehaviorTreeValueConnection>, IBehaviorTreeValuePort, IBehaviorTreeOutputPort
    {
        public BehaviorTreeValueOutput(string key, Type type, Func<Flow, object> getValue) : base(key)
        {
            Ensure.That(nameof(type)).IsNotNull(type);
            Ensure.That(nameof(getValue)).IsNotNull(getValue);

            this.type = type;
            this.getValue = getValue;
        }

        public BehaviorTreeValueOutput(string key, Type type) : base(key)
        {
            Ensure.That(nameof(type)).IsNotNull(type);

            this.type = type;
        }

        public readonly Func<Flow, object> getValue;

        internal Func<Flow, bool> canPredictValue;

        public bool supportsPrediction => canPredictValue != null;

        public bool supportsFetch => getValue != null;

        public Type type { get; }

        public override IEnumerable<BehaviorTreeValueConnection> validConnections => behaviorTreeNode?.graph?.valueConnections.WithSource(this) ?? Enumerable.Empty<BehaviorTreeValueConnection>();

        public override IEnumerable<BehaviorTreeInvalidConnection> invalidConnections => behaviorTreeNode?.graph?.invalidConnections.WithSource(this) ?? Enumerable.Empty<BehaviorTreeInvalidConnection>();

        public override IEnumerable<BehaviorTreeValueInput> validConnectedPorts => validConnections.Select(c => c.destination);

        public override IEnumerable<IBehaviorTreeInputPort> invalidConnectedPorts => invalidConnections.Select(c => c.destination);

        public override bool CanConnectToValid(BehaviorTreeValueInput port)
        {
            var source = this;
            var destination = port;

            return source.type.IsConvertibleTo(destination.type, false);
        }

        public override void ConnectToValid(BehaviorTreeValueInput port)
        {
            var source = this;
            var destination = port;

            destination.Disconnect();

            behaviorTreeNode.graph.valueConnections.Add(new BehaviorTreeValueConnection(source, destination));
        }

        public override void ConnectToInvalid(IBehaviorTreeInputPort port)
        {
            ConnectInvalid(this, port);
        }

        public override void DisconnectFromValid(BehaviorTreeValueInput port)
        {
            var connection = validConnections.SingleOrDefault(c => c.destination == port);

            if (connection != null)
            {
                behaviorTreeNode.graph.valueConnections.Remove(connection);
            }
        }

        public override void DisconnectFromInvalid(IBehaviorTreeInputPort port)
        {
            DisconnectInvalid(this, port);
        }

        public BehaviorTreeValueOutput PredictableIf(Func<Flow, bool> condition)
        {
            Ensure.That(nameof(condition)).IsNotNull(condition);

            canPredictValue = condition;

            return this;
        }

        public BehaviorTreeValueOutput Predictable()
        {
            canPredictValue = (flow) => true;

            return this;
        }

        public override IBehaviorTreePort CompatiblePort(IBehaviorTreeNode unit)
        {
            if (unit == this.behaviorTreeNode) return null;

            return unit.CompatibleValueInput(type);
        }
    }
}
