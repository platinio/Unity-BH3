using Unity.VisualScripting;

namespace ArcaneOnyx.BehaviorTree
{
    [Descriptor(typeof(BehaviorTreeGraph))]
    public sealed class BehaviorTreeGraphDescriptor : GraphCore.GraphDescriptor<BehaviorTreeGraph, GraphDescription>
    {
        public BehaviorTreeGraphDescriptor(BehaviorTreeGraph target) : base(target) { }
    }
}