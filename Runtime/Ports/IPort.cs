using System.Collections.Generic;

namespace ArcaneOnyx.BehaviorTree
{
    public interface IPort : GraphCore.IGraphItem
    {
        IBehaviorTreeNode behaviorTreeNode { get; set; }
        string key { get; }

        IEnumerable<IPortRelation> relations { get; }

        IEnumerable<IPortConnection> validConnections { get; }
        IEnumerable<PortInvalidConnection> invalidConnections { get; }
        IEnumerable<IPortConnection> connections { get; }
        IEnumerable<IPort> connectedPorts { get; }
        bool hasAnyConnection { get; }
        bool hasValidConnection { get; }
        bool hasInvalidConnection { get; }
        bool CanInvalidlyConnectTo(IPort port);
        bool CanValidlyConnectTo(IPort port);
        void InvalidlyConnectTo(IPort port);
        void ValidlyConnectTo(IPort port);
        void Disconnect();
        IPort CompatiblePort(IBehaviorTreeNode unit);
    }
}