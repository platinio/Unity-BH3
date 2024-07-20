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

        IPortCollection<ControlInput> controlInputs { get; }

        IPortCollection<ControlOutput> controlOutputs { get; }

        IPortCollection<ValueInput> valueInputs { get; }

        IPortCollection<ValueOutput> valueOutputs { get; }

        IPortCollection<InvalidInput> invalidInputs { get; }

        IPortCollection<InvalidOutput> invalidOutputs { get; }

        IEnumerable<IInputPort> inputs { get; }

        IEnumerable<IOutputPort> outputs { get; }

        IEnumerable<IInputPort> validInputs { get; }

        IEnumerable<IOutputPort> validOutputs { get; }

        IEnumerable<IPort> ports { get; }

        IEnumerable<IPort> invalidPorts { get; }

        IEnumerable<IPort> validPorts { get; }

        void PortsChanged();

        event Action onPortsChanged;

        #endregion

        #region Connections

        IConnectionCollection<IPortRelation, IPort, IPort> relations { get; }

        IEnumerable<IPortConnection> connections { get; }

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
        public static ValueInput CompatibleValueInput(this IBehaviorTreeNode unit, Type outputType)
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

        public static ValueOutput CompatibleValueOutput(this IBehaviorTreeNode unit, Type inputType)
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
