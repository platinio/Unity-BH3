using ArcaneOnyx.GraphCore;
using Unity.VisualScripting;

namespace ArcaneOnyx.BehaviorTree
{
    /// <summary>
    /// A <see cref="ReactiveGuard"/> that reads its answer from a boolean port — the counterpart to
    /// <see cref="BooleanConditionalExecution"/>, and the only reactive guard in the create menu.
    ///
    /// <para>
    /// The port stays wide open on purpose: a variable read, a whole Visual Scripting graph, or a literal.
    /// <c>hasTarget</c> is a <em>decision</em> designers retune constantly, not a raw fact, and it must
    /// never require a recompile. What is declared instead is <em>when</em> the guard may recompute — see
    /// <see cref="ReactiveGuard"/> — because a trigger that had to be executed to find out whether to skip
    /// executing the condition would cost exactly what it was meant to save.
    /// </para>
    /// </summary>
    [GraphCreateMenu("Add Conditional Execution/Reactive Guard")]
    public class BooleanReactiveGuard : ReactiveGuard
    {
        [DoNotSerialize]
        public ValueInput Value { get; private set; }

        public override string NodeName => "Reactive Guard";

        public override string Description =>
            "Re-checks its condition while the branch runs. Aborts its own branch when it turns false, and "
            + "takes over from a lower-priority branch when it turns true.";

        protected override void Definition()
        {
            base.Definition();

            Value = ValueInput<bool>(nameof(Value), false);
        }

        public override bool Evaluate() => (bool)Value.GetValue();
    }
}
