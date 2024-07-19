using System.Collections.Generic;
using Unity.VisualScripting;

namespace Platinio.BehaviorTree
{
    public interface IBehaviorTreePort : IGraphItem
    {
        IBehaviorTreeNode behaviorTreeNode { get; set; }
        string key { get; }

        IEnumerable<IBehaviorTreeRelation> relations { get; }

        IEnumerable<IBehaviorTreeConnection> validConnections { get; }
        IEnumerable<BehaviorTreeInvalidConnection> invalidConnections { get; }
        IEnumerable<IBehaviorTreeConnection> connections { get; }
        IEnumerable<IBehaviorTreePort> connectedPorts { get; }
        bool hasAnyConnection { get; }
        bool hasValidConnection { get; }
        bool hasInvalidConnection { get; }
        bool CanInvalidlyConnectTo(IBehaviorTreePort port);
        bool CanValidlyConnectTo(IBehaviorTreePort port);
        void InvalidlyConnectTo(IBehaviorTreePort port);
        void ValidlyConnectTo(IBehaviorTreePort port);
        void Disconnect();
        IBehaviorTreePort CompatiblePort(IBehaviorTreeNode unit);
    }
}