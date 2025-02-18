using ArcaneOnyxArcaneOnyx.VisualScripting;
using Unity.VisualScripting;

namespace ArcaneOnyx.VisualScripting
{
    [Widget(typeof(ScriptGraphInput))]
    public sealed class ScriptGraphInputWidget : UnitWidget<ScriptGraphInput>
    {
        public ScriptGraphInputWidget(FlowCanvas canvas, ScriptGraphInput unit) : base(canvas, unit) { }

        protected override NodeColorMix baseColor => NodeColorMix.TealReadable;
    }
}