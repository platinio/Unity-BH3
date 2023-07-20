using Unity.VisualScripting;

namespace Platinio.BehaviorTree
{
    [Inspector(typeof(BehaviorTreeNode))]
    public class BehaviorTreeNodeInspector : ReflectedInspector
    {
        public BehaviorTreeNodeInspector(Metadata metadata) : base(metadata) { }
    }
}