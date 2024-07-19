using System;
using Unity.VisualScripting;

namespace Platinio.BehaviorTree
{
    public sealed class BehaviorTreeControlConnection : BehaviorTreeConnection<BehaviorTreeControlOutput, BehaviorTreeControlInput>, IBehaviorTreeConnection
    {
        [Obsolete(Serialization.ConstructorWarning)]
        public BehaviorTreeControlConnection() : base() { }

        public BehaviorTreeControlConnection(BehaviorTreeControlOutput source, BehaviorTreeControlInput destination) : base(source, destination)
        {
            if (source.hasValidConnection)
            {
                throw new InvalidConnectionException("Control output ports do not support multiple connections.");
            }
        }

        #region Ports

        public override BehaviorTreeControlOutput source => sourceUnit.controlOutputs[sourceKey];

        public override BehaviorTreeControlInput destination => destinationUnit.controlInputs[destinationKey];

        IBehaviorTreeOutputPort IConnection<IBehaviorTreeOutputPort, IBehaviorTreeInputPort>.source => source;

        IBehaviorTreeInputPort IConnection<IBehaviorTreeOutputPort, IBehaviorTreeInputPort>.destination => destination;

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