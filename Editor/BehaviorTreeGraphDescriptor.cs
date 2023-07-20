using Unity.VisualScripting;

namespace Platinio.BehaviorTree
{
    [Descriptor(typeof(BehaviorTreeGraph))]
    public sealed class BehaviorTreeGraphDescriptor : GraphDescriptor<BehaviorTreeGraph, GraphDescription>
    {
        public BehaviorTreeGraphDescriptor(BehaviorTreeGraph target) : base(target) { }
    }
}