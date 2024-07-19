using Unity.VisualScripting;

namespace Platinio.BehaviorTree
{
    public abstract class BehaviorTreeOutputPortWidget<TPort> : BehaviorTreePortWidget<TPort> where TPort : class, IBehaviorTreeOutputPort
    {
        protected BehaviorTreeOutputPortWidget(BehaviorTreeCanvas canvas, TPort port) : base(canvas, port) { }

        protected override Edge edge => Edge.Right;
    }
}