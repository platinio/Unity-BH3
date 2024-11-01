using System;
using System.Collections.Generic;
using System.Linq;
using Unity.VisualScripting;
using UnityEngine;
using UnityObject = UnityEngine.Object;

namespace ArcaneOnyx.BehaviorTree
{
    public sealed class ValueInput : Port<ValueOutput, IOutputPort, PortValueConnection>, IValuePort, IInputPort
    {
        public ValueInput(string key, Type type) : base(key)
        {
            Ensure.That(nameof(type)).IsNotNull(type);

            this.Type = type;
        }

        public Type Type { get; }

        public bool hasDefaultValue => false;//behaviorTreeNode.defaultValues.ContainsKey(key);

        public override IEnumerable<PortValueConnection> validConnections => behaviorTreeNode?.graph?.valueConnections.WithDestination(this) ?? Enumerable.Empty<PortValueConnection>();

        public override IEnumerable<PortInvalidConnection> invalidConnections => behaviorTreeNode?.graph?.invalidConnections.WithDestination(this) ?? Enumerable.Empty<PortInvalidConnection>();

        public override IEnumerable<ValueOutput> validConnectedPorts => validConnections.Select(c => c.source);

        public override IEnumerable<IOutputPort> invalidConnectedPorts => invalidConnections.Select(c => c.source);

        // Use for inspector metadata
        [DoNotSerialize]
        internal object _defaultValue
        {
            get
            {
                return behaviorTreeNode.defaultValues[key];
            }
            set
            {
                behaviorTreeNode.defaultValues[key] = value;
            }
        }

        public bool nullMeansSelf { get; private set; }

        public bool allowsNull { get; private set; }

        public PortValueConnection connection => behaviorTreeNode.graph?.valueConnections.SingleOrDefaultWithDestination(this);

        public override bool hasValidConnection => connection != null;

        public void SetDefaultValue(object value)
        {
            Ensure.That(nameof(value)).IsOfType(value, Type);

            if (!SupportsDefaultValue(Type))
            {
                return;
            }

            if (behaviorTreeNode.defaultValues.ContainsKey(key))
            {
                behaviorTreeNode.defaultValues[key] = value;
            }
            else
            {
                behaviorTreeNode.defaultValues.Add(key, value);
            }
        }

        public override bool CanConnectToValid(ValueOutput port)
        {
            var source = port;
            var destination = this;

            return source.Type.IsConvertibleTo(destination.Type, false);
        }

        public override void ConnectToValid(ValueOutput port)
        {
            var source = port;
            var destination = this;

            destination.Disconnect();

            behaviorTreeNode.graph.valueConnections.Add(new PortValueConnection(source, destination));
        }

        public override void ConnectToInvalid(IOutputPort port)
        {
            ConnectInvalid(port, this);
        }

        public override void DisconnectFromValid(ValueOutput port)
        {
            var connection = validConnections.SingleOrDefault(c => c.source == port);

            if (connection != null)
            {
                behaviorTreeNode.graph.valueConnections.Remove(connection);
            }
        }

        public override void DisconnectFromInvalid(IOutputPort port)
        {
            DisconnectInvalid(port, this);
        }

        public ValueInput NullMeansSelf()
        {
            if (ComponentHolderProtocol.IsComponentHolderType(Type))
            {
                nullMeansSelf = true;
            }

            return this;
        }

        public ValueInput AllowsNull()
        {
            if (Type.IsNullable())
            {
                allowsNull = true;
            }

            return this;
        }

        private static readonly HashSet<Type> typesWithDefaultValues = new HashSet<Type>()
        {
            typeof(Vector2),
            typeof(Vector3),
            typeof(Vector4),
            typeof(Color),
            typeof(AnimationCurve),
            typeof(Rect),
            typeof(Ray),
            typeof(Ray2D),
            typeof(Type),
#if PACKAGE_INPUT_SYSTEM_EXISTS
            typeof(UnityEngine.InputSystem.InputAction),
#endif
        };

        public static bool SupportsDefaultValue(Type type)
        {
            return
                typesWithDefaultValues.Contains(type) ||
                typesWithDefaultValues.Contains(Nullable.GetUnderlyingType(type)) ||
                type.IsBasic() ||
                typeof(UnityObject).IsAssignableFrom(type);
        }

        public override IPort CompatiblePort(IBehaviorTreeNode unit)
        {
            if (unit == this.behaviorTreeNode) return null;

            return unit.CompatibleValueOutput(Type);
        }
        
        public object GetValue()
        {
            if (connection != null)
            {
                var output = connection.source;
                var value = output.GetPortValue();
                
                return value;
            }
            else
            {
                if (behaviorTreeNode.defaultValues.TryGetValue(key, out var value)) return value;
                throw new MissingValuePortInputException(key);
            }
        }
        
    }
}
