using Unity.VisualScripting;

namespace ArcaneOnyx.VisualScripting
{
    [Widget(typeof(ScriptGraphOutput))]
    public class ScriptGraphOutputWidget : UnitWidget<ScriptGraphOutput>
    {
        public ScriptGraphOutputWidget(FlowCanvas canvas, ScriptGraphOutput unit) : base(canvas, unit) { }

        protected override NodeColorMix baseColor => NodeColorMix.TealReadable;
    }
}