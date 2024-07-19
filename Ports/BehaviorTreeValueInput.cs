using System;
using System.Collections.Generic;
using System.Linq;
using Unity.VisualScripting;
using UnityEngine;
using UnityObject = UnityEngine.Object;

namespace Platinio.BehaviorTree
{
    public sealed class BehaviorTreeValueInput : BehaviorTreePort<BehaviorTreeValueOutput, IBehaviorTreeOutputPort, BehaviorTreeValueConnection>, IBehaviorTreeValuePort, IBehaviorTreeInputPort
    {
        public BehaviorTreeValueInput(string key, Type type) : base(key)
        {
            Ensure.That(nameof(type)).IsNotNull(type);

            this.type = type;
        }

        public Type type { get; }

        public bool hasDefaultValue => false;//behaviorTreeNode.defaultValues.ContainsKey(key);

        public override IEnumerable<BehaviorTreeValueConnection> validConnections => behaviorTreeNode?.graph?.valueConnections.WithDestination(this) ?? Enumerable.Empty<BehaviorTreeValueConnection>();

        public override IEnumerable<BehaviorTreeInvalidConnection> invalidConnections => behaviorTreeNode?.graph?.invalidConnections.WithDestination(this) ?? Enumerable.Empty<BehaviorTreeInvalidConnection>();

        public override IEnumerable<BehaviorTreeValueOutput> validConnectedPorts => validConnections.Select(c => c.source);

        public override IEnumerable<IBehaviorTreeOutputPort> invalidConnectedPorts => invalidConnections.Select(c => c.source);

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

        public BehaviorTreeValueConnection connection => behaviorTreeNode.graph?.valueConnections.SingleOrDefaultWithDestination(this);

        public override bool hasValidConnection => connection != null;

        public void SetDefaultValue(object value)
        {
            Ensure.That(nameof(value)).IsOfType(value, type);

            if (!SupportsDefaultValue(type))
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

        public override bool CanConnectToValid(BehaviorTreeValueOutput port)
        {
            var source = port;
            var destination = this;

            return source.type.IsConvertibleTo(destination.type, false);
        }

        public override void ConnectToValid(BehaviorTreeValueOutput port)
        {
            var source = port;
            var destination = this;

            destination.Disconnect();

            behaviorTreeNode.graph.valueConnections.Add(new BehaviorTreeValueConnection(source, destination));
        }

        public override void ConnectToInvalid(IBehaviorTreeOutputPort port)
        {
            ConnectInvalid(port, this);
        }

        public override void DisconnectFromValid(BehaviorTreeValueOutput port)
        {
            var connection = validConnections.SingleOrDefault(c => c.source == port);

            if (connection != null)
            {
                behaviorTreeNode.graph.valueConnections.Remove(connection);
            }
        }

        public override void DisconnectFromInvalid(IBehaviorTreeOutputPort port)
        {
            DisconnectInvalid(port, this);
        }

        public BehaviorTreeValueInput NullMeansSelf()
        {
            if (ComponentHolderProtocol.IsComponentHolderType(type))
            {
                nullMeansSelf = true;
            }

            return this;
        }

        public BehaviorTreeValueInput AllowsNull()
        {
            if (type.IsNullable())
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

        public override IBehaviorTreePort CompatiblePort(IBehaviorTreeNode unit)
        {
            if (unit == this.behaviorTreeNode) return null;

            return unit.CompatibleValueOutput(type);
        }
    }
}
