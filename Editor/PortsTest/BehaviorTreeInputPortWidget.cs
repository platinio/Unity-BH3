using Unity.VisualScripting;

namespace Platinio.BehaviorTree
{
    public abstract class BehaviorTreeInputPortWidget<TPort> : BehaviorTreePortWidget<TPort> where TPort : class, IBehaviorTreeInputPort
    {
        protected BehaviorTreeInputPortWidget(BehaviorTreeCanvas canvas, TPort port) : base(canvas, port) { }

        protected override Edge edge => Edge.Left;
    }
}