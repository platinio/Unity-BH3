using Unity.VisualScripting;
using IGraphElementWidget = ArcaneOnyx.GraphCore.IGraphElementWidget;

namespace ArcaneOnyx.BehaviorTree
{
    public interface IBehaviorTreeWidget : IGraphElementWidget
    {
        IBehaviorTreeNode behaviorTreeNode { get; }

        Inspector GetPortInspector(IPort port, Metadata metadata);
    }
}