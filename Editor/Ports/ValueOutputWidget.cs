using Unity.VisualScripting;
using UnityEngine;

namespace ArcaneOnyx.BehaviorTree
{
    [Widget(typeof(ValueOutput))]
    public class ValueOutputWidget : OutputPortWidget<ValueOutput>
    {
        public ValueOutputWidget(BehaviorTreeCanvas canvas, ValueOutput port) : base(canvas, port)
        {
            color = ValueConnectionWidget.DetermineColor(port.Type);
        }

        protected override bool colorIfActive => !BoltFlow.Configuration.animateControlConnections || !BoltFlow.Configuration.animateValueConnections;

        public override Color color { get; }

        protected override Texture handleTextureConnected => BoltFlow.Icons.valuePortConnected?[12];

        protected override Texture handleTextureUnconnected => BoltFlow.Icons.valuePortUnconnected?[12];
    }
}