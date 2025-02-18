using System.Collections.Generic;
using Unity.VisualScripting;

namespace ArcaneOnyx.VisualScripting
{
    [Analyser(typeof(ScriptGraphOutput))]
    public class ScriptGraphOutputAnalyser : UnitAnalyser<ScriptGraphOutput>
    {
        public ScriptGraphOutputAnalyser(GraphReference reference, ScriptGraphOutput unit) : base(reference, unit) { }

        protected override IEnumerable<Warning> Warnings()
        {
            foreach (var baseWarning in base.Warnings())
            {
                yield return baseWarning;
            }

            if (unit.graph != null)
            {
                foreach (var definitionWarning in UnitPortDefinitionUtility.Warnings(unit.graph, LinqUtility.Concat<IUnitPortDefinition>(unit.graph.controlOutputDefinitions, unit.graph.valueOutputDefinitions)))
                {
                    yield return definitionWarning;
                }
            }
        }
    }
}