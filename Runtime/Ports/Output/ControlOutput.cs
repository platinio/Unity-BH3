using System;
using System.Collections.Generic;
using System.Linq;
using Unity.VisualScripting;

namespace ArcaneOnyx.BehaviorTree
{
    public sealed class ControlOutput : Port<ControlInput, IInputPort, PortControlConnection>, IControlPort, IOutputPort
    {
        public ControlOutput(string key) : base(key) { }

        public override IEnumerable<PortControlConnection> validConnections => behaviorTreeNode?.graph?.controlConnections.WithSource(this) ?? Enumerable.Empty<PortControlConnection>();

        public override IEnumerable<PortInvalidConnection> invalidConnections => behaviorTreeNode?.graph?.invalidConnections.WithSource(this) ?? Enumerable.Empty<PortInvalidConnection>();

        public override IEnumerable<ControlInput> validConnectedPorts => validConnections.Select(c => c.destination);

        public override IEnumerable<IInputPort> invalidConnectedPorts => invalidConnections.Select(c => c.destination);

        public bool isPredictable
        {
            get
            {
                using (var recursion = Recursion.New(1))
                {
                    return IsPredictable(recursion);
                }
            }
        }

        public bool IsPredictable(Recursion recursion)
        {
            if (behaviorTreeNode.isControlRoot)
            {
                return true;
            }

            if (!recursion?.TryEnter(this) ?? false)
            {
                return false;
            }

            var isPredictable = behaviorTreeNode.relations.WithDestination(this).Where(r => r.source is ControlInput).All(r => ((ControlInput)r.source).IsPredictable(recursion));

            recursion?.Exit(this);

            return isPredictable;
        }

        public bool couldBeEntered
        {
            get
            {
                if (!isPredictable)
                {
                    throw new NotSupportedException();
                }

                if (behaviorTreeNode.isControlRoot)
                {
                    return true;
                }

                return behaviorTreeNode.relations.WithDestination(this).Where(r => r.source is ControlInput).Any(r => ((ControlInput)r.source).couldBeEntered);
            }
        }

        public PortControlConnection connection => behaviorTreeNode.graph?.controlConnections.SingleOrDefaultWithSource(this);

        public override bool hasValidConnection => connection != null;

        public override bool CanConnectToValid(ControlInput port)
        {
            return true;
        }

        public override void ConnectToValid(ControlInput port)
        {
            var source = this;
            var destination = port;

            source.Disconnect();

            behaviorTreeNode.graph.controlConnections.Add(new PortControlConnection(source, destination));
        }

        public override void ConnectToInvalid(IInputPort port)
        {
            ConnectInvalid(this, port);
        }

        public override void DisconnectFromValid(ControlInput port)
        {
            var connection = validConnections.SingleOrDefault(c => c.destination == port);

            if (connection != null)
            {
                behaviorTreeNode.graph.controlConnections.Remove(connection);
            }
        }

        public override void DisconnectFromInvalid(IInputPort port)
        {
            DisconnectInvalid(this, port);
        }

        public override IPort CompatiblePort(IBehaviorTreeNode unit)
        {
            if (unit == this.behaviorTreeNode) return null;

            return unit.controlInputs.FirstOrDefault();
        }
    }
}
