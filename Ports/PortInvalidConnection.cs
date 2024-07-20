using System;
using System.Linq;
using Unity.VisualScripting;

namespace Platinio.BehaviorTree
{
    public sealed class PortInvalidConnection : PortConnection<IOutputPort, IInputPort>, IPortConnection
    {
        [Obsolete(Serialization.ConstructorWarning)]
        public PortInvalidConnection() : base() { }

        public PortInvalidConnection(IOutputPort source, IInputPort destination) : base(source, destination) { }

        public override void AfterRemove()
        {
            base.AfterRemove();
            source.behaviorTreeNode.RemoveUnconnectedInvalidPorts();
            destination.behaviorTreeNode.RemoveUnconnectedInvalidPorts();
        }

        #region Ports

        public override IOutputPort source => sourceUnit.outputs.Single(p => p.key == sourceKey);

        public override IInputPort destination => destinationUnit.inputs.Single(p => p.key == destinationKey);

        public IOutputPort validSource => sourceUnit.validOutputs.Single(p => p.key == sourceKey);

        public IInputPort validDestination => destinationUnit.validInputs.Single(p => p.key == destinationKey);

        #endregion

        #region Dependencies

        public override bool sourceExists => sourceUnit.outputs.Any(p => p.key == sourceKey);

        public override bool destinationExists => destinationUnit.inputs.Any(p => p.key == destinationKey);

        public bool validSourceExists => sourceUnit.validOutputs.Any(p => p.key == sourceKey);

        public bool validDestinationExists => destinationUnit.validInputs.Any(p => p.key == destinationKey);

        public override bool HandleDependencies()
        {
            // Replace the invalid connection with a valid connection if it can be created instead.
            if (validSourceExists && validDestinationExists && validSource.CanValidlyConnectTo(validDestination))
            {
                validSource.ValidlyConnectTo(validDestination);

                return false;
            }

            // Add the invalid ports to the nodes if need be
            if (!sourceExists)
            {
                sourceUnit.invalidOutputs.Add(new InvalidOutput(sourceKey));
            }

            if (!destinationExists)
            {
                destinationUnit.invalidInputs.Add(new InvalidInput(destinationKey));
            }

            return true;
        }

        
        #endregion
    }
}
