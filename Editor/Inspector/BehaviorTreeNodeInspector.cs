using Unity.VisualScripting;

namespace ArcaneOnyx.BehaviorTree
{
    [Inspector(typeof(BehaviorTreeNode))]
    public class BehaviorTreeNodeInspector : ReflectedInspector
    {
        public BehaviorTreeNodeInspector(Metadata metadata) : base(metadata) { }
    }
}