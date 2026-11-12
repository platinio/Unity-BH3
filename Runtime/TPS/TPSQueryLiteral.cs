using ArcaneOnyx.GraphCore;
using ArcaneOnyx.UnityTacticalPositionSelection;
using Unity.VisualScripting;

namespace ArcaneOnyx.BehaviorTree
{
    /// <summary>
    /// A query preset as a value node, the way <see cref="StringLiteral"/> carries a string: picked in the
    /// inspector, offered on a port as an <see cref="IPositionQuerySource"/>.
    ///
    /// <para>
    /// The way to give the TPS nodes a query picked from a database: their Query port
    /// takes any query source and so has no inline value. The literal is <i>visible on the canvas</i> — named by
    /// its comment, feeding several selection nodes from one place, swapped in one edit. The inspector shows a
    /// database-grouped dropdown.
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

            Value = ValueOutput<IPositionQuerySource>(nameof(Value), PickedQuery);
        }

        private IPositionQuerySource PickedQuery() => TPSQuerySources.Live(value);
    }
}
