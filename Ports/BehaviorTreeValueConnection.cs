using System;
using Unity.VisualScripting;

namespace Platinio.BehaviorTree
{
    public sealed class BehaviorTreeValueConnection : BehaviorTreeConnection<BehaviorTreeValueOutput, BehaviorTreeValueInput>, IBehaviorTreeConnection
    {
        /*
        public class DebugData : UnitConnectionDebugData
        {
            public object lastValue { get; set; }

            public bool assignedLastValue { get; set; }
        }*/

       
        [Obsolete(Serialization.ConstructorWarning)]
        public BehaviorTreeValueConnection() : base() { }

        public BehaviorTreeValueConnection(BehaviorTreeValueOutput source, BehaviorTreeValueInput destination) : base(source, destination)
        {
            if (destination.hasValidConnection)
            {
                throw new InvalidConnectionException("Value input ports do not support multiple connections.");
            }

            if (!source.type.IsConvertibleTo(destination.type, false))
            {
                throw new InvalidConnectionException($"Cannot convert from '{source.type}' to '{destination.type}'.");
            }
        }

        #region Ports

        public override BehaviorTreeValueOutput source => sourceUnit.valueOutputs[sourceKey];

        public override BehaviorTreeValueInput destination => destinationUnit.valueInputs[destinationKey];

        IBehaviorTreeOutputPort IConnection<IBehaviorTreeOutputPort, IBehaviorTreeInputPort>.source => source;

        IBehaviorTreeInputPort IConnection<IBehaviorTreeOutputPort, IBehaviorTreeInputPort>.destination => destination;

        #endregion

        #region Dependencies

        public override bool sourceExists => sourceUnit.valueOutputs.Contains(sourceKey);

        public override bool destinationExists => destinationUnit.valueInputs.Contains(destinationKey);

        #endregion

        public IGraphElementDebugData CreateDebugData()
        {
            return default;
        }
    }
}