using Unity.VisualScripting;
using UnityEngine;

namespace Platinio.BehaviorTree
{
    [Widget(typeof(BehaviorTreeValueInput))]
    public class BehaviorTreeValueInputWidget : BehaviorTreeInputPortWidget<BehaviorTreeValueInput>
    {
        public BehaviorTreeValueInputWidget(BehaviorTreeCanvas canvas, BehaviorTreeValueInput port) : base(canvas, port)
        {
            color = ValueConnectionWidget.DetermineColor(port.type);
        }

        protected override bool showInspector => port.hasDefaultValue && !port.hasValidConnection;

        protected override bool colorIfActive => !BoltFlow.Configuration.animateControlConnections || !BoltFlow.Configuration.animateValueConnections;

        public override Color color { get; }

        protected override Texture handleTextureConnected => BoltFlow.Icons.valuePortConnected?[12];

        protected override Texture handleTextureUnconnected => BoltFlow.Icons.valuePortUnconnected?[12];

        public override Metadata FetchInspectorMetadata()
        {
            if (port.hasDefaultValue)
            {
                return metadata["_defaultValue"].Cast(port.type);
            }
            else
            {
                return null;
            }
        }
    }
}