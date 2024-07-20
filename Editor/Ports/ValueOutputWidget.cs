using Unity.VisualScripting;
using UnityEngine;

namespace Platinio.BehaviorTree
{
    [Widget(typeof(ValueOutput))]
    public class ValueOutputWidget : OutputPortWidget<ValueOutput>
    {
        public ValueOutputWidget(BehaviorTreeCanvas canvas, ValueOutput port) : base(canvas, port)
        {
            color = ValueConnectionWidget.DetermineColor(port.type);
        }

        protected override bool colorIfActive => !BoltFlow.Configuration.animateControlConnections || !BoltFlow.Configuration.animateValueConnections;

        public override Color color { get; }

        protected override Texture handleTextureConnected => BoltFlow.Icons.valuePortConnected?[12];

        protected override Texture handleTextureUnconnected => BoltFlow.Icons.valuePortUnconnected?[12];
    }
}