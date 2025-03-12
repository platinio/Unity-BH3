using System.Linq;
using ArcaneOnyx.VisualScripting;
using Unity.VisualScripting;

namespace ArcaneOnyx.VisualScripting
{
    [Descriptor(typeof(ScriptGraphInput))]
    public class ScriptGraphInputDescriptor : UnitDescriptor<ScriptGraphInput>
    {
        public ScriptGraphInputDescriptor(ScriptGraphInput unit) : base(unit) { }

        protected override void DefinedPort(IUnitPort port, UnitPortDescription description)
        {
            base.DefinedPort(port, description);

            var definition = unit.graph.validPortDefinitions.OfType<IUnitInputPortDefinition>().SingleOrDefault(d => d.key == port.key);

            if (definition != null)
            {
                description.label = definition.Label();
                description.summary = definition.summary;

                if (definition.hideLabel)
                {
                    description.showLabel = false;
                }
            }
        }
    }
}