using System.Collections.Generic;
using System.Linq;
using Unity.VisualScripting;

namespace Platinio.BehaviorTree
{
    public abstract class Port<TValidOther, TInvalidOther, TExternalConnection> : IPort
        where TValidOther : IPort
        where TInvalidOther : IPort
        where TExternalConnection : IPortConnection
    {
        protected Port(string key)
        {
            Ensure.That(nameof(key)).IsNotNull(key);

            this.key = key;
        }

        public IBehaviorTreeNode behaviorTreeNode { get; set; }

        public string key { get; }

        public IGraph graph => behaviorTreeNode?.graph;

        public IEnumerable<IPortRelation> relations =>
            LinqUtility.Concat<IPortRelation>(behaviorTreeNode.relations.WithSource(this),
                behaviorTreeNode.relations.WithDestination(this)).Distinct();

        public abstract IEnumerable<TExternalConnection> validConnections { get; }

        public abstract IEnumerable<PortInvalidConnection> invalidConnections { get; }

        public abstract IEnumerable<TValidOther> validConnectedPorts { get; }

        public abstract IEnumerable<TInvalidOther> invalidConnectedPorts { get; }

        IEnumerable<IPortConnection> IPort.validConnections => validConnections.Cast<IPortConnection>();

        public IEnumerable<IPortConnection> connections => LinqUtility.Concat<IPortConnection>(validConnections, invalidConnections);

        public IEnumerable<IPort> connectedPorts => LinqUtility.Concat<IPort>(validConnectedPorts, invalidConnectedPorts);

        public bool hasAnyConnection => hasValidConnection || hasInvalidConnection;

        // Allow for more efficient overrides

        public virtual bool hasValidConnection => validConnections.Any();

        public virtual bool hasInvalidConnection => invalidConnections.Any();

        private bool CanConnectTo(IPort port)
        {
            Ensure.That(nameof(port)).IsNotNull(port);

            return behaviorTreeNode != null && // We belong to a unit
                port.behaviorTreeNode != null &&    // Port belongs to a unit
                port.behaviorTreeNode != behaviorTreeNode &&    // that is different than the current one
                port.behaviorTreeNode.graph == behaviorTreeNode.graph;    // but is on the same graph.
        }

        public bool CanValidlyConnectTo(IPort port)
        {
            return CanConnectTo(port) && port is TValidOther && CanConnectToValid((TValidOther)port);
        }

        public bool CanInvalidlyConnectTo(IPort port)
        {
            return CanConnectTo(port) && port is TInvalidOther && CanConnectToInvalid((TInvalidOther)port);
        }

        public void ValidlyConnectTo(IPort port)
        {
            Ensure.That(nameof(port)).IsNotNull(port);

            if (!(port is TValidOther))
            {
                throw new InvalidConnectionException();
            }

            ConnectToValid((TValidOther)port);
        }

        public void InvalidlyConnectTo(IPort port)
        {
            Ensure.That(nameof(port)).IsNotNull(port);

            if (!(port is TInvalidOther))
            {
                throw new InvalidConnectionException();
            }

            ConnectToInvalid((TInvalidOther)port);
        }

        public void Disconnect()
        {
            while (validConnectedPorts.Any())
            {
                DisconnectFromValid(validConnectedPorts.First());
            }

            while (invalidConnectedPorts.Any())
            {
                DisconnectFromInvalid(invalidConnectedPorts.First());
            }
        }

        public abstract bool CanConnectToValid(TValidOther port);

        public bool CanConnectToInvalid(TInvalidOther port)
        {
            return true;
        }

        public abstract void ConnectToValid(TValidOther port);

        public abstract void ConnectToInvalid(TInvalidOther port);

        public abstract void DisconnectFromValid(TValidOther port);

        public abstract void DisconnectFromInvalid(TInvalidOther port);

        public abstract IPort CompatiblePort(IBehaviorTreeNode unit);

        protected void ConnectInvalid(IOutputPort source, IInputPort destination)
        {
            //TODO: FIX THIS
            /*
            var connection = behaviorTreeNode.graph.invalidConnections.SingleOrDefault(c => c.source == source && c.destination == destination);

            if (connection != null)
            {
                return;
            }

            behaviorTreeNode.graph.invalidConnections.Add(new InvalidConnection(source, destination));*/
        }

        protected void DisconnectInvalid(IOutputPort source, IInputPort destination)
        {
            //TODO: FIX THIS
            /*
            var connection = behaviorTreeNode.graph.invalidConnections.SingleOrDefault(c => c.source == source && c.destination == destination);

            if (connection == null)
            {
                return;
            }

            behaviorTreeNode.graph.invalidConnections.Remove(connection);*/
        }
    }
}
