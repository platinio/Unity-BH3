using System;
using System.Collections.Generic;
using System.Linq;
using Unity.VisualScripting;

namespace Platinio.BehaviorTree
{
    public sealed class ValueOutput : Port<ValueInput, IInputPort, PortValueConnection>, IValuePort, IOutputPort
    {
        public ValueOutput(string key, Type type, Func<Flow, object> getValue) : base(key)
        {
            Ensure.That(nameof(type)).IsNotNull(type);
            Ensure.That(nameof(getValue)).IsNotNull(getValue);

            this.type = type;
            this.getValue = getValue;
        }

        public ValueOutput(string key, Type type) : base(key)
        {
            Ensure.That(nameof(type)).IsNotNull(type);

            this.type = type;
        }

        public readonly Func<Flow, object> getValue;

        internal Func<Flow, bool> canPredictValue;

        public bool supportsPrediction => canPredictValue != null;

        public bool supportsFetch => getValue != null;

        public Type type { get; }

        public override IEnumerable<PortValueConnection> validConnections => behaviorTreeNode?.graph?.valueConnections.WithSource(this) ?? Enumerable.Empty<PortValueConnection>();

        public override IEnumerable<PortInvalidConnection> invalidConnections => behaviorTreeNode?.graph?.invalidConnections.WithSource(this) ?? Enumerable.Empty<PortInvalidConnection>();

        public override IEnumerable<ValueInput> validConnectedPorts => validConnections.Select(c => c.destination);

        public override IEnumerable<IInputPort> invalidConnectedPorts => invalidConnections.Select(c => c.destination);

        public override bool CanConnectToValid(ValueInput port)
        {
            var source = this;
            var destination = port;

            return source.type.IsConvertibleTo(destination.type, false);
        }

        public override void ConnectToValid(ValueInput port)
        {
            var source = this;
            var destination = port;

            destination.Disconnect();

            behaviorTreeNode.graph.valueConnections.Add(new PortValueConnection(source, destination));
        }

        public override void ConnectToInvalid(IInputPort port)
        {
            ConnectInvalid(this, port);
        }

        public override void DisconnectFromValid(ValueInput port)
        {
            var connection = validConnections.SingleOrDefault(c => c.destination == port);

            if (connection != null)
            {
                behaviorTreeNode.graph.valueConnections.Remove(connection);
            }
        }

        public override void DisconnectFromInvalid(IInputPort port)
        {
            DisconnectInvalid(this, port);
        }

        public ValueOutput PredictableIf(Func<Flow, bool> condition)
        {
            Ensure.That(nameof(condition)).IsNotNull(condition);

            canPredictValue = condition;

            return this;
        }

        public ValueOutput Predictable()
        {
            canPredictValue = (flow) => true;

            return this;
        }

        public override IPort CompatiblePort(IBehaviorTreeNode unit)
        {
            if (unit == this.behaviorTreeNode) return null;

            return unit.CompatibleValueInput(type);
        }
    }
}
