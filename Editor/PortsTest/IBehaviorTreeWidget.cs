using Unity.VisualScripting;

namespace Platinio.BehaviorTree
{
    public interface IBehaviorTreeWidget : IGraphElementWidget
    {
        IBehaviorTreeNode behaviorTreeNode { get; }

        Inspector GetPortInspector(IBehaviorTreePort port, Metadata metadata);
    }
}