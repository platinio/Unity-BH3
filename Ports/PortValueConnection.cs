using System;
using Unity.VisualScripting;

namespace Platinio.BehaviorTree
{
    public sealed class PortValueConnection : PortConnection<ValueOutput, ValueInput>, IPortConnection
    {
        /*
        public class DebugData : UnitConnectionDebugData
        {
            public object lastValue { get; set; }

            public bool assignedLastValue { get; set; }
        }*/

       
        [Obsolete(Serialization.ConstructorWarning)]
        public PortValueConnection() : base() { }

        public PortValueConnection(ValueOutput source, ValueInput destination) : base(source, destination)
        {
            if (destination.hasValidConnection)
            {
                throw new InvalidConnectionException("Value input ports do not support multiple connections.");
            }

            if (!source.Type.IsConvertibleTo(destination.Type, false))
            {
                throw new InvalidConnectionException($"Cannot convert from '{source.Type}' to '{destination.Type}'.");
            }
        }

        #region Ports

        public override ValueOutput source => sourceUnit.valueOutputs[sourceKey];

        public override ValueInput destination => destinationUnit.valueInputs[destinationKey];

        IOutputPort IConnection<IOutputPort, IInputPort>.source => source;

        IInputPort IConnection<IOutputPort, IInputPort>.destination => destination;

        #endregion

        #region Dependencies

        public override bool sourceExists => sourceUnit.valueOutputs.Contains(sourceKey);

        public override bool destinationExists => destinationUnit.valueInputs.Contains(destinationKey);

        #endregion
        /*
        public IGraphElementDebugData CreateDebugData()
        {
            return default;
        }*/
    }
}