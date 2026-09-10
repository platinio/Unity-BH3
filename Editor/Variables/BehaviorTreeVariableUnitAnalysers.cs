using System.Collections.Generic;
using Unity.VisualScripting;

namespace ArcaneOnyx.BehaviorTree
{
    /// <summary>
    /// The canvas half of <see cref="VariableKindField"/> for the script graph units: a unit with no store
    /// chosen shows an error on its own node, the way the tree's variable nodes report themselves, instead
    /// of being found out one throw at a time when the graph runs.
    /// </summary>
    internal static class BehaviorTreeVariableUnitWarnings
    {
        public const string NoStore = "No variable store, so this unit has nowhere to look. Pick one in Kind.";

        public static IEnumerable<Warning> For(BehaviorTreeVariableKind kind)
        {
            if (kind == BehaviorTreeVariableKind.None) yield return Warning.Error(NoStore);
        }
    }

    [Analyser(typeof(GetBehaviorTreeVariable))]
    public sealed class GetBehaviorTreeVariableAnalyser : UnitAnalyser<GetBehaviorTreeVariable>
    {
        public GetBehaviorTreeVariableAnalyser(GraphReference reference, GetBehaviorTreeVariable unit)
            : base(reference, unit) { }

        protected override IEnumerable<Warning> Warnings()
        {
            foreach (var warning in base.Warnings()) yield return warning;
            foreach (var warning in BehaviorTreeVariableUnitWarnings.For(unit.kind)) yield return warning;
        }
    }

    [Analyser(typeof(SetBehaviorTreeVariable))]
    public sealed class SetBehaviorTreeVariableAnalyser : UnitAnalyser<SetBehaviorTreeVariable>
    {
        public SetBehaviorTreeVariableAnalyser(GraphReference reference, SetBehaviorTreeVariable unit)
            : base(reference, unit) { }

        protected override IEnumerable<Warning> Warnings()
        {
            foreach (var warning in base.Warnings()) yield return warning;
            foreach (var warning in BehaviorTreeVariableUnitWarnings.For(unit.kind)) yield return warning;
        }
    }
}
