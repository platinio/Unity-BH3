using Unity.VisualScripting;

namespace ArcaneOnyx.BehaviorTree
{
    public interface IBehaviorTreeWidget : IGraphElementWidget
    {
        IBehaviorTreeNode behaviorTreeNode { get; }

        Inspector GetPortInspector(IPort port, Metadata metadata);
    }
}