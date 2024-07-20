using System;
using Unity.VisualScripting;
using UnityEngine;

namespace Platinio.BehaviorTree
{
    /* Implementation note:
     * Using an abstract base class works as a type unification workaround.
     * https://stackoverflow.com/questions/22721763
     * https://stackoverflow.com/a/7664919
     *
     * However, this forces us to use concrete classes for connections
     * instead of interfaces. In other words, no IControlConnection / IValueConnection.
     * If we did use interfaces, there would be ambiguity that needs to be resolved
     * at every reference to the source or destination.
     *
     * However, using a disambiguator hack seems to confuse even recent Mono runtime versions of Unity
     * and breaks its vtable. Sometimes, method pointers are just plain wrong.
     * I'm guessing this is specifically due to InvalidConnection, which actually
     * does unify the types; what the C# warning warned about.
     * https://stackoverflow.com/q/50051657/154502
     *
     * THEREFORE, IUnitConnection has to be implemented at the concrete class level,
     * because at that point the type unification warning is moot, because the type arguments are
     * provided.
     */

    public abstract class PortConnection<TSourcePort, TDestinationPort> : GraphElement<BehaviorTreeGraph>, IConnection<TSourcePort, TDestinationPort>
        where TSourcePort : class, IOutputPort
        where TDestinationPort : class, IInputPort
    {
        
        public IGraphElementDebugData CreateDebugData()
        {
            return default;
        }

        
        [Obsolete(Serialization.ConstructorWarning)]
        protected PortConnection() { }

        public BehaviorTreeGraph graph { get; }
        
        protected PortConnection(TSourcePort source, TDestinationPort destination)
        {
            Ensure.That(nameof(source)).IsNotNull(source);
            Ensure.That(nameof(destination)).IsNotNull(destination);

            if (source.behaviorTreeNode.graph != destination.behaviorTreeNode.graph)
            {
                throw new NotSupportedException("Cannot create connections across graphs.");
            }

            if (source.behaviorTreeNode == destination.behaviorTreeNode)
            {
                throw new InvalidConnectionException("Cannot create connections on the same unit.");
            }

            sourceUnit = source.behaviorTreeNode;
            sourceKey = source.key;
            destinationUnit = destination.behaviorTreeNode;
            destinationKey = destination.key;
        }
        

        #region Ports

        [Serialize]
        protected IBehaviorTreeNode sourceUnit { get; private set; }

        [Serialize]
        protected string sourceKey { get; private set; }

        [Serialize]
        protected IBehaviorTreeNode destinationUnit { get; private set; }

        [Serialize]
        protected string destinationKey { get; private set; }

        [DoNotSerialize]
        public abstract TSourcePort source { get; }

        [DoNotSerialize]
        public abstract TDestinationPort destination { get; }

        #endregion

        #region Dependencies

        public override int dependencyOrder => 1;

        public abstract bool sourceExists { get; }

        public abstract bool destinationExists { get; }

        protected void CopyFrom(PortConnection<TSourcePort, TDestinationPort> source)
        {
            base.CopyFrom(source);
        }

        public override bool HandleDependencies()
        {
            // Replace the connection with an invalid connection if the ports are either missing or incompatible.
            // If the ports are missing, create invalid ports if required.

            var valid = true;
            IOutputPort source;
            IInputPort destination;

            if (!sourceExists)
            {
                if (!sourceUnit.invalidOutputs.Contains(sourceKey))
                {
                    sourceUnit.invalidOutputs.Add(new InvalidOutput(sourceKey));
                }

                source = sourceUnit.invalidOutputs[sourceKey];
                valid = false;
            }
            else
            {
                source = this.source;
            }

            if (!destinationExists)
            {
                if (!destinationUnit.invalidInputs.Contains(destinationKey))
                {
                    destinationUnit.invalidInputs.Add(new InvalidInput(destinationKey));
                }

                destination = destinationUnit.invalidInputs[destinationKey];
                valid = false;
            }
            else
            {
                destination = this.destination;
            }

            if (!source.CanValidlyConnectTo(destination))
            {
                valid = false;
            }

            if (!valid && source.CanInvalidlyConnectTo(destination))
            {
                source.InvalidlyConnectTo(destination);

                // Silence this warning if a unit with a missing type is involved (as it will not have any defined ports).
                // This is to avoid drowning users in warning and error messages if a unit's script goes missing.
                if (source.behaviorTreeNode.GetType() != typeof(MissingType) && destination.behaviorTreeNode.GetType() != typeof(MissingType))
                {
                    Debug.LogWarning($"Could not load connection between '{source.key}' of '{sourceUnit}' and '{destination.key}' of '{destinationUnit}'.");
                }
            }

            return valid;
        }

        #endregion

        #region Analytics

        public override AnalyticsIdentifier GetAnalyticsIdentifier()
        {
            return null;
        }

        #endregion
    }
}
