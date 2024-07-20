using Unity.VisualScripting;

namespace Platinio.BehaviorTree
{
    public abstract class InputPortWidget<TPort> : PortWidget<TPort> where TPort : class, IInputPort
    {
        protected InputPortWidget(BehaviorTreeCanvas canvas, TPort port) : base(canvas, port) { }

        protected override Edge edge => Edge.Left;
    }
}