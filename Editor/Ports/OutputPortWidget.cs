using Unity.VisualScripting;

namespace ArcaneOnyx.BehaviorTree
{
    public abstract class OutputPortWidget<TPort> : PortWidget<TPort> where TPort : class, IOutputPort
    {
        protected OutputPortWidget(BehaviorTreeCanvas canvas, TPort port) : base(canvas, port) { }

        protected override Edge edge => Edge.Right;
    }
}