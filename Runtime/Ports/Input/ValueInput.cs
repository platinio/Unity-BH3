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

        /// <summary>
        /// Whether this port carries an inline value, which is what lets the canvas draw an editable field on it
        /// rather than a bare handle. Both readers — <c>ValueInputWidget.showInspector</c> and
        /// <c>FetchInspectorMetadata</c> — test this before touching <see cref="_defaultValue"/>, so the key
        /// lookup behind it is guarded.
        /// </summary>
        public bool hasDefaultValue => behaviorTreeNode != null && behaviorTreeNode.defaultValues.ContainsKey(key);

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

        /// <summary>
        /// Whether leaving this port unconnected is legitimate rather than a defect.
        /// <para>
        /// Set at the declaration site by <see cref="SafeToLeaveUnconnected"/>, which is the only place that
        /// knows why.
        /// </para>
        /// </summary>
        public bool safeToLeaveUnconnected { get; private set; }

        /// <summary>
        /// Declares that this port may be left unconnected without the node breaking.
        ///
        /// <para>
        /// Two things make that true, and both are properties of how the <em>node</em> reads the port, which
        /// is why this is stated here and not guessed at elsewhere: the port is read through
        /// <see cref="GetComponent{T}"/>, which returns without ever calling <see cref="GetValue"/> and lets
        /// the node fall back to the machine's own GameObject; or the node does not read the port at all.
        /// </para>
        ///
        /// <para>
        /// Unconditional, unlike <see cref="NullMeansSelf"/>, which silently does nothing when the port's
        /// type is not a component holder. A marker that quietly fails to apply is worse than none, because
        /// the port then gets reported as a defect and the declaration looks like it handled it.
        /// </para>
        /// </summary>
        public ValueInput SafeToLeaveUnconnected()
        {
            safeToLeaveUnconnected = true;
            return this;
        }

        /// <summary>
        /// True when nothing feeds this port, it carries no inline default, and it was not declared safe to
        /// leave alone — so reading it throws <see cref="MissingValuePortInputException"/>.
        ///
        /// <para>
        /// One rule, one place. The canvas badge and <c>bt_verify</c> both ask this rather than each deciding
        /// for itself; the previous arrangement had verification matching on port <em>name</em> against a
        /// hard-coded set, which exempted every port called <c>Target</c> whether or not it was safe.
        /// </para>
        /// </summary>
        public bool IsUnfedRequired => !hasValidConnection && !hasDefaultValue && !safeToLeaveUnconnected;

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

        /// <summary>
        /// Reads this port as <typeparamref name="T"/>, converting the value when its type is not already
        /// <typeparamref name="T"/>.
        ///
        /// <para>
        /// Use this rather than casting <see cref="GetValue"/>. What may feed a port is decided by
        /// <see cref="CanConnectToValid"/> — <c>source.Type.IsConvertibleTo(destination.Type, false)</c> —
        /// which accepts convertible pairs, not just identical ones: an <c>int</c> output into a
        /// <c>float</c> input, a <c>GameObject</c> into a <c>Transform</c>. <see cref="GetValue"/> hands back
        /// the raw boxed object, and <em>C# unboxing does not convert</em>: <c>(float)</c> applied to a boxed
        /// <c>int</c> throws <see cref="InvalidCastException"/>. So every connection the canvas legitimately
        /// created but whose types merely convert used to throw on its first evaluation.
        /// </para>
        ///
        /// <para>
        /// This resolves that by answering the question in one place with the same utility the connection
        /// gate uses, so "what may feed this port" cannot drift between edit time and runtime again.
        /// The exact-type case — overwhelmingly the common one — is a single type check and costs nothing
        /// extra; only a genuinely converting read pays for the conversion, and
        /// <see cref="ConversionUtility"/> caches the type-pair lookup behind it.
        /// </para>
        /// </summary>
        /// <exception cref="MissingValuePortInputException">
        /// Nothing feeds the port and it declares no default — unchanged from <see cref="GetValue"/>.
        /// </exception>
        /// <exception cref="InvalidCastException">
        /// The value cannot become <typeparamref name="T"/> by any conversion. The message names the node,
        /// the port, and both types, because the cause is nearly always a node reading its own port as a
        /// type the port does not declare.
        /// </exception>
        public T GetValue<T>()
        {
            var value = GetValue();

            if (value is T typed) return typed;

            if (TryConvertValue<T>(value, out var converted)) return converted;

            throw new InvalidCastException(
                $"{behaviorTreeNode?.GetType().Name ?? "A node"} read its '{key}' port as {typeof(T).Name}, "
                + $"but the port holds {(value == null ? "null" : value.GetType().Name)} and there is no "
                + $"conversion between the two. The port itself declares {Type.Name}"
                + (typeof(T) == Type
                    ? ", so whatever feeds it is the problem."
                    : $" — reading a {Type.Name} port as {typeof(T).Name} is the node's own bug."));
        }

        /// <summary>
        /// Reads this port as <typeparamref name="T"/> when it can, and returns <c>default</c> when it
        /// cannot — the converting counterpart of <c>GetValue() as T</c>, for the readers that treat an
        /// unusable value as "nothing here" rather than an error.
        ///
        /// <para>
        /// Still throws <see cref="MissingValuePortInputException"/> for an unfed, defaultless port: that is
        /// a missing wire, not a value the node can shrug off, and <c>as</c> never softened it either.
        /// </para>
        /// </summary>
        public T GetValueOrDefault<T>()
        {
            var value = GetValue();

            if (value is T typed) return typed;

            TryConvertValue<T>(value, out var converted);
            return converted;
        }

        /// <summary>
        /// The single conversion rule both typed readers share, so they cannot disagree about what a port
        /// value may become.
        /// </summary>
        private static bool TryConvertValue<T>(object value, out T result)
        {
            // The overwhelmingly common case: the value is already what was asked for. Note this is also
            // true of a destroyed UnityEngine.Object, whose fake null is passed through exactly as a plain
            // cast would have, so lifetime checks downstream keep working.
            if (value is T typed)
            {
                result = typed;
                return true;
            }

            result = default;

            // null is a legitimate answer for a reference or nullable port and never one for a value type.
            // Checked explicitly because `null is T` is false for every T, so a null would otherwise fall
            // through and be reported as a conversion failure it isn't.
            if (value == null) return NullSatisfies<T>();

            if (!ConversionUtility.CanConvert(value, typeof(T), false)) return false;

            try
            {
                var converted = ConversionUtility.Convert(value, typeof(T));

                if (converted is T typedResult)
                {
                    result = typedResult;
                    return true;
                }

                // A conversion can legitimately produce null even from a non-null value: the Unity
                // hierarchy conversion answers null for a destroyed source, which is the same "there is
                // nothing here" every other destroyed-object path in the codebase produces. Without this,
                // a GameObject output feeding a Transform port would throw the moment its object died —
                // and the destroyed case is precisely when a node most needs a null it can test for.
                return converted == null && NullSatisfies<T>();
            }
            catch (InvalidConversionException)
            {
                // CanConvert answers from the type pair alone; the value itself can still refuse — an
                // overflowing numeric narrowing, for instance. Reported as "cannot", which is what the
                // callers of both readers are equipped to handle.
            }

            return false;
        }

        /// <summary>Whether null is an answer <typeparamref name="T"/> can hold at all.</summary>
        private static bool NullSatisfies<T>() =>
            !typeof(T).IsValueType || Nullable.GetUnderlyingType(typeof(T)) != null;

        public T GetComponent<T>() where T : Component
        {
            if (connection == null) return default;
            
            var value = GetValue();
            
            if (value is T component) return component;
            if (value is GameObject go) return go.GetComponent<T>();

            return null;
        }
    }
}
