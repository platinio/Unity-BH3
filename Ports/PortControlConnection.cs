using System;
using Unity.VisualScripting;

namespace Platinio.BehaviorTree
{
    public sealed class PortControlConnection : PortConnection<ControlOutput, ControlInput>, IPortConnection
    {
        [Obsolete(Serialization.ConstructorWarning)]
        public PortControlConnection() : base() { }

        public PortControlConnection(ControlOutput source, ControlInput destination) : base(source, destination)
        {
            if (source.hasValidConnection)
            {
                throw new InvalidConnectionException("Control output ports do not support multiple connections.");
            }
        }

        #region Ports

        public override ControlOutput source => sourceUnit.controlOutputs[sourceKey];

        public override ControlInput destination => destinationUnit.controlInputs[destinationKey];

        IOutputPort IConnection<IOutputPort, IInputPort>.source => source;

        IInputPort IConnection<IOutputPort, IInputPort>.destination => destination;

        #endregion

        #region Dependencies

        public override bool sourceExists => sourceUnit.controlOutputs.Contains(sourceKey);

        public override bool destinationExists => destinationUnit.controlInputs.Contains(destinationKey);

        #endregion

        public IGraphElementDebugData CreateDebugData()
        {
            return default;
        }
    }
}