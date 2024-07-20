using Unity.VisualScripting;
using UnityEngine;

namespace Platinio.BehaviorTree
{
    [Widget(typeof(InvalidOutput))]
    public class InvalidOutputWidget : OutputPortWidget<InvalidOutput>
    {
        public InvalidOutputWidget(BehaviorTreeCanvas canvas, InvalidOutput port) : base(canvas, port) { }

        protected override Texture handleTextureConnected => BoltFlow.Icons.invalidPortConnected?[12];

        protected override Texture handleTextureUnconnected => BoltFlow.Icons.invalidPortUnconnected?[12];

        protected override bool colorIfActive => false;

        protected override bool canStartConnection => false;
    }
}