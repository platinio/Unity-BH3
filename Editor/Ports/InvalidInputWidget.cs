using Unity.VisualScripting;
using UnityEngine;

namespace ArcaneOnyx.BehaviorTree
{
    [Widget(typeof(InvalidInput))]
    public class InvalidInputWidget : InputPortWidget<InvalidInput>
    {
        public InvalidInputWidget(BehaviorTreeCanvas canvas, InvalidInput port) : base(canvas, port) { }

        protected override Texture handleTextureConnected => BoltFlow.Icons.invalidPortConnected?[12];

        protected override Texture handleTextureUnconnected => BoltFlow.Icons.invalidPortUnconnected?[12];

        protected override bool colorIfActive => false;

        protected override bool canStartConnection => false;
    }
}