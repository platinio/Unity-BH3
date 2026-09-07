using ArcaneOnyx.GraphCore;
using ArcaneOnyx.UnityTacticalPositionSelection;
using Unity.VisualScripting;

namespace ArcaneOnyx.BehaviorTree
{
    /// <summary>
    /// A query preset as a value node, the way <see cref="StringLiteral"/> carries a string: picked in the
    /// inspector, offered on a port.
    ///
    /// <para>
    /// The counterpart to <see cref="TacticalPositionSelection"/>'s query becoming a port. An inline value
    /// on that port serves the single-consumer case; this literal is for the query a designer wants
    /// <i>visible on the canvas</i> — named by its comment, feeding several selection nodes from one
    /// place, swapped in one edit. The inspector shows the same database-grouped dropdown as the port's
    /// inline value, via the query item type's registered Inspector.
    /// </para>
    ///
    /// <para>
    /// Not to be confused with the TPS module's <c>TacticalPositionSelectionQueryLiteral</c>, a Visual
    /// Scripting unit that <i>assembles</i> a query from generator and evaluators inside a query graph.
    /// This node merely holds a reference to a finished preset.
    /// </para>
    /// </summary>
    [GraphCreateMenu("Literal/TPS Query")]
    public class TPSQueryLiteral : Literal
    {
        [Serialize, Inspectable, TPSQueryPicker] private TacticalPositionSelectionQueryItem value;

        [DoNotSerialize]
        public ValueOutput Value { get; private set; }

        public override string NodeName => string.IsNullOrEmpty(NodeComment) ? "TPS Query Literal" : NodeComment;
        public override bool CanBeUsedAsTransitionDestination => false;

        protected override void Definition()
        {
            base.Definition();

            Value = ValueOutput<TacticalPositionSelectionQueryItem>(nameof(Value), () => value);
        }
    }
}
