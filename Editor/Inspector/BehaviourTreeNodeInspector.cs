using Unity.VisualScripting;

namespace Platinio.BehaviourTree
{
    [Inspector(typeof(BehaviourTreeNode))]
    public class BehaviourTreeNodeInspector : ReflectedInspector
    {
        public BehaviourTreeNodeInspector(Metadata metadata) : base(metadata) { }
    }
}