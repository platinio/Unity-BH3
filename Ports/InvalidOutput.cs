using System.Collections.Generic;
using System.Linq;

namespace ArcaneOnyx.BehaviorTree
{
    public sealed class InvalidOutput : Port<IInputPort, IInputPort, PortInvalidConnection>, IInvalidPort, IOutputPort
    {
        public InvalidOutput(string key) : base(key) { }

        public override IEnumerable<PortInvalidConnection> validConnections => behaviorTreeNode?.graph?.invalidConnections.WithSource(this) ?? Enumerable.Empty<PortInvalidConnection>();

        public override IEnumerable<PortInvalidConnection> invalidConnections => Enumerable.Empty<PortInvalidConnection>();

        public override IEnumerable<IInputPort> validConnectedPorts => validConnections.Select(c => c.destination);

        public override IEnumerable<IInputPort> invalidConnectedPorts => invalidConnections.Select(c => c.destination);

        public override bool CanConnectToValid(IInputPort port)
        {
            return false;
        }

        public override void ConnectToValid(IInputPort port)
        {
            ConnectInvalid(this, port);
        }

        public override void ConnectToInvalid(IInputPort port)
        {
            ConnectInvalid(this, port);
        }

        public override void DisconnectFromValid(IInputPort port)
        {
            DisconnectInvalid(this, port);
        }

        public override void DisconnectFromInvalid(IInputPort port)
        {
            DisconnectInvalid(this, port);
        }

        public override IPort CompatiblePort(IBehaviorTreeNode unit)
        {
            return null;
        }
    }
}