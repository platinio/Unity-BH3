using System;
using System.Collections.Generic;
using System.Linq;
using Unity.VisualScripting;
using UnityEngine;

namespace Platinio.BehaviorTree
{
    public interface IBehaviorTreeNode : IGraphElementWithDebugData
    {
        new BehaviorTreeGraph graph { get; }

        #region Definition

        bool canDefine { get; }

        bool isDefined { get; }

        bool failedToDefine { get; }

        Exception definitionException { get; }

        void Define();

        void EnsureDefined();

        void RemoveUnconnectedInvalidPorts();

        #endregion

        #region Default Values

        Dictionary<string, object> defaultValues { get; }

        #endregion

        #region Ports

        IBehaviorTreePortCollection<BehaviorTreeControlInput> controlInputs { get; }

        IBehaviorTreePortCollection<BehaviorTreeControlOutput> controlOutputs { get; }

        IBehaviorTreePortCollection<BehaviorTreeValueInput> valueInputs { get; }

        IBehaviorTreePortCollection<BehaviorTreeValueOutput> valueOutputs { get; }

        IBehaviorTreePortCollection<BehaviorTreeInvalidInput> invalidInputs { get; }

        IBehaviorTreePortCollection<BehaviorTreeInvalidOutput> invalidOutputs { get; }

        IEnumerable<IBehaviorTreeInputPort> inputs { get; }

        IEnumerable<IBehaviorTreeOutputPort> outputs { get; }

        IEnumerable<IBehaviorTreeInputPort> validInputs { get; }

        IEnumerable<IBehaviorTreeOutputPort> validOutputs { get; }

        IEnumerable<IBehaviorTreePort> ports { get; }

        IEnumerable<IBehaviorTreePort> invalidPorts { get; }

        IEnumerable<IBehaviorTreePort> validPorts { get; }

        void PortsChanged();

        event Action onPortsChanged;

        #endregion

        #region Connections

        IConnectionCollection<IBehaviorTreeRelation, IBehaviorTreePort, IBehaviorTreePort> relations { get; }

        IEnumerable<IBehaviorTreeConnection> connections { get; }

        #endregion

        #region Analysis

        bool isControlRoot { get; }

        #endregion

        #region Widget

        Vector2 position { get; set; }

        #endregion
    }

    public static class XUnit
    {
        public static BehaviorTreeValueInput CompatibleValueInput(this IBehaviorTreeNode unit, Type outputType)
        {
            Ensure.That(nameof(outputType)).IsNotNull(outputType);

            return unit.valueInputs
                .Where(valueInput => ConversionUtility.CanConvert(outputType, valueInput.type, false))
                .OrderBy((valueInput) =>
                {
                    var exactType = outputType == valueInput.type;
                    var free = !valueInput.hasValidConnection;

                    if (free && exactType)
                    {
                        return 1;
                    }
                    else if (free)
                    {
                        return 2;
                    }
                    else if (exactType)
                    {
                        return 3;
                    }
                    else
                    {
                        return 4;
                    }
                }).FirstOrDefault();
        }

        public static BehaviorTreeValueOutput CompatibleValueOutput(this IBehaviorTreeNode unit, Type inputType)
        {
            Ensure.That(nameof(inputType)).IsNotNull(inputType);

            return unit.valueOutputs
                .Where(valueOutput => ConversionUtility.CanConvert(valueOutput.type, inputType, false))
                .OrderBy((valueOutput) =>
                {
                    var exactType = inputType == valueOutput.type;
                    var free = !valueOutput.hasValidConnection;

                    if (free && exactType)
                    {
                        return 1;
                    }
                    else if (free)
                    {
                        return 2;
                    }
                    else if (exactType)
                    {
                        return 3;
                    }
                    else
                    {
                        return 4;
                    }
                }).FirstOrDefault();
        }
    }
}
