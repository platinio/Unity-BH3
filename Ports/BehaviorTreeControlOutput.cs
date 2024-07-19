using System;
using System.Collections.Generic;
using System.Linq;
using Unity.VisualScripting;

namespace Platinio.BehaviorTree
{
    public sealed class BehaviorTreeControlOutput : BehaviorTreePort<BehaviorTreeControlInput, IBehaviorTreeInputPort, BehaviorTreeControlConnection>, IBehaviorTreeControlPort, IBehaviorTreeOutputPort
    {
        public BehaviorTreeControlOutput(string key) : base(key) { }

        public override IEnumerable<BehaviorTreeControlConnection> validConnections => behaviorTreeNode?.graph?.controlConnections.WithSource(this) ?? Enumerable.Empty<BehaviorTreeControlConnection>();

        public override IEnumerable<BehaviorTreeInvalidConnection> invalidConnections => behaviorTreeNode?.graph?.invalidConnections.WithSource(this) ?? Enumerable.Empty<BehaviorTreeInvalidConnection>();

        public override IEnumerable<BehaviorTreeControlInput> validConnectedPorts => validConnections.Select(c => c.destination);

        public override IEnumerable<IBehaviorTreeInputPort> invalidConnectedPorts => invalidConnections.Select(c => c.destination);

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

            var isPredictable = behaviorTreeNode.relations.WithDestination(this).Where(r => r.source is BehaviorTreeControlInput).All(r => ((BehaviorTreeControlInput)r.source).IsPredictable(recursion));

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

                return behaviorTreeNode.relations.WithDestination(this).Where(r => r.source is BehaviorTreeControlInput).Any(r => ((BehaviorTreeControlInput)r.source).couldBeEntered);
            }
        }

        public BehaviorTreeControlConnection connection => behaviorTreeNode.graph?.controlConnections.SingleOrDefaultWithSource(this);

        public override bool hasValidConnection => connection != null;

        public override bool CanConnectToValid(BehaviorTreeControlInput port)
        {
            return true;
        }

        public override void ConnectToValid(BehaviorTreeControlInput port)
        {
            var source = this;
            var destination = port;

            source.Disconnect();

            behaviorTreeNode.graph.controlConnections.Add(new BehaviorTreeControlConnection(source, destination));
        }

        public override void ConnectToInvalid(IBehaviorTreeInputPort port)
        {
            ConnectInvalid(this, port);
        }

        public override void DisconnectFromValid(BehaviorTreeControlInput port)
        {
            var connection = validConnections.SingleOrDefault(c => c.destination == port);

            if (connection != null)
            {
                behaviorTreeNode.graph.controlConnections.Remove(connection);
            }
        }

        public override void DisconnectFromInvalid(IBehaviorTreeInputPort port)
        {
            DisconnectInvalid(this, port);
        }

        public override IBehaviorTreePort CompatiblePort(IBehaviorTreeNode unit)
        {
            if (unit == this.behaviorTreeNode) return null;

            return unit.controlInputs.FirstOrDefault();
        }
    }
}
