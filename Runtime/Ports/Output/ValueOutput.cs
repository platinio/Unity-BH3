using System;
using System.Collections.Generic;
using System.Linq;
using Unity.VisualScripting;

namespace ArcaneOnyx.BehaviorTree
{
    public delegate object GetPortValue();
    
    public sealed class ValueOutput : Port<ValueInput, IInputPort, PortValueConnection>, IValuePort, IOutputPort
    {
        public ValueOutput(string key, Type type, GetPortValue getPortValue) : base(key)
        {
            Ensure.That(nameof(type)).IsNotNull(type);
            Ensure.That(nameof(getPortValue)).IsNotNull(getPortValue);

            this.Type = type;
            GetPortValue = getPortValue;
        }

        public ValueOutput(string key, Type type) : base(key)
        {
            Ensure.That(nameof(type)).IsNotNull(type);
            this.Type = type;
        }

        public readonly GetPortValue GetPortValue;
      
        public bool supportsFetch => GetPortValue != null;

        public Type Type { get; }

        public override IEnumerable<PortValueConnection> validConnections => behaviorTreeNode?.graph?.valueConnections.WithSource(this) ?? Enumerable.Empty<PortValueConnection>();

        public override IEnumerable<PortInvalidConnection> invalidConnections => behaviorTreeNode?.graph?.invalidConnections.WithSource(this) ?? Enumerable.Empty<PortInvalidConnection>();

        public override IEnumerable<ValueInput> validConnectedPorts => validConnections.Select(c => c.destination);

        public override IEnumerable<IInputPort> invalidConnectedPorts => invalidConnections.Select(c => c.destination);

        public override bool CanConnectToValid(ValueInput port)
        {
            var source = this;
            var destination = port;

            return source.Type.IsConvertibleTo(destination.Type, false);
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

        public override IPort CompatiblePort(IBehaviorTreeNode node)
        {
            if (node == behaviorTreeNode) return null;
            return node.CompatibleValueInput(Type);
        }
    }
}
