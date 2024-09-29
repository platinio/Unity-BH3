using Unity.VisualScripting;
using UnityEngine;

namespace ArcaneOnyx.BehaviorTree
{
    [Widget(typeof(PortInvalidConnection))]
    public sealed class InvalidConnectionWidget : PortConnectionWidget<PortInvalidConnection>
    {
        public InvalidConnectionWidget(BehaviorTreeCanvas canvas, PortInvalidConnection connection) : base(canvas, connection) { }


        #region Drawing

        public override Color color => UnitConnectionStyles.invalidColor;

        #endregion


        #region Droplets

        protected override bool showDroplets => false;

        protected override Vector2 GetDropletSize() => Vector2.zero;

        protected override void DrawDroplet(Rect position) { }

        #endregion
    }
}