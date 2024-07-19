using System.Collections.Generic;
using System.Linq;
using Unity.VisualScripting;

namespace Platinio.BehaviorTree
{
    public sealed class BehaviorTreeInvalidOutput : BehaviorTreePort<IBehaviorTreeInputPort, IBehaviorTreeInputPort, BehaviorTreeInvalidConnection>, IBehaviorTreeInvalidPort, IBehaviorTreeOutputPort
    {
        public BehaviorTreeInvalidOutput(string key) : base(key) { }

        public override IEnumerable<BehaviorTreeInvalidConnection> validConnections => behaviorTreeNode?.graph?.invalidConnections.WithSource(this) ?? Enumerable.Empty<BehaviorTreeInvalidConnection>();

        public override IEnumerable<BehaviorTreeInvalidConnection> invalidConnections => Enumerable.Empty<BehaviorTreeInvalidConnection>();

        public override IEnumerable<IBehaviorTreeInputPort> validConnectedPorts => validConnections.Select(c => c.destination);

        public override IEnumerable<IBehaviorTreeInputPort> invalidConnectedPorts => invalidConnections.Select(c => c.destination);

        public override bool CanConnectToValid(IBehaviorTreeInputPort port)
        {
            return false;
        }

        public override void ConnectToValid(IBehaviorTreeInputPort port)
        {
            ConnectInvalid(this, port);
        }

        public override void ConnectToInvalid(IBehaviorTreeInputPort port)
        {
            ConnectInvalid(this, port);
        }

        public override void DisconnectFromValid(IBehaviorTreeInputPort port)
        {
            DisconnectInvalid(this, port);
        }

        public override void DisconnectFromInvalid(IBehaviorTreeInputPort port)
        {
            DisconnectInvalid(this, port);
        }

        public override IBehaviorTreePort CompatiblePort(IBehaviorTreeNode unit)
        {
            return null;
        }
    }
}