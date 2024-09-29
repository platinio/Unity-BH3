using System.Collections.Generic;
using System.Linq;

namespace ArcaneOnyx.BehaviorTree
{
    public sealed class InvalidInput : Port<IOutputPort, IOutputPort, PortInvalidConnection>, IInvalidPort, IInputPort
    {
        public InvalidInput(string key) : base(key) { }

        public override IEnumerable<PortInvalidConnection> validConnections => behaviorTreeNode?.graph?.invalidConnections.WithDestination(this) ?? Enumerable.Empty<PortInvalidConnection>();

        public override IEnumerable<PortInvalidConnection> invalidConnections => Enumerable.Empty<PortInvalidConnection>();

        public override IEnumerable<IOutputPort> validConnectedPorts => validConnections.Select(c => c.source);

        public override IEnumerable<IOutputPort> invalidConnectedPorts => invalidConnections.Select(c => c.source);

        public override bool CanConnectToValid(IOutputPort port)
        {
            return false;
        }

        public override void ConnectToValid(IOutputPort port)
        {
            ConnectInvalid(port, this);
        }

        public override void ConnectToInvalid(IOutputPort port)
        {
            ConnectInvalid(port, this);
        }

        public override void DisconnectFromValid(IOutputPort port)
        {
            DisconnectInvalid(port, this);
        }

        public override void DisconnectFromInvalid(IOutputPort port)
        {
            DisconnectInvalid(port, this);
        }

        public override IPort CompatiblePort(IBehaviorTreeNode unit)
        {
            return null;
        }
    }
}