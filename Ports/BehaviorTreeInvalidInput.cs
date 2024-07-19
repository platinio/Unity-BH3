using System.Collections.Generic;
using System.Linq;
using Unity.VisualScripting;

namespace Platinio.BehaviorTree
{
    public sealed class BehaviorTreeInvalidInput : BehaviorTreePort<IBehaviorTreeOutputPort, IBehaviorTreeOutputPort, BehaviorTreeInvalidConnection>, IBehaviorTreeInvalidPort, IBehaviorTreeInputPort
    {
        public BehaviorTreeInvalidInput(string key) : base(key) { }

        public override IEnumerable<BehaviorTreeInvalidConnection> validConnections => behaviorTreeNode?.graph?.invalidConnections.WithDestination(this) ?? Enumerable.Empty<BehaviorTreeInvalidConnection>();

        public override IEnumerable<BehaviorTreeInvalidConnection> invalidConnections => Enumerable.Empty<BehaviorTreeInvalidConnection>();

        public override IEnumerable<IBehaviorTreeOutputPort> validConnectedPorts => validConnections.Select(c => c.source);

        public override IEnumerable<IBehaviorTreeOutputPort> invalidConnectedPorts => invalidConnections.Select(c => c.source);

        public override bool CanConnectToValid(IBehaviorTreeOutputPort port)
        {
            return false;
        }

        public override void ConnectToValid(IBehaviorTreeOutputPort port)
        {
            ConnectInvalid(port, this);
        }

        public override void ConnectToInvalid(IBehaviorTreeOutputPort port)
        {
            ConnectInvalid(port, this);
        }

        public override void DisconnectFromValid(IBehaviorTreeOutputPort port)
        {
            DisconnectInvalid(port, this);
        }

        public override void DisconnectFromInvalid(IBehaviorTreeOutputPort port)
        {
            DisconnectInvalid(port, this);
        }

        public override IBehaviorTreePort CompatiblePort(IBehaviorTreeNode unit)
        {
            return null;
        }
    }
}