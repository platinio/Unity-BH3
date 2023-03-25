using Unity.VisualScripting;

namespace Platinio.BehaviourTree
{
    [Descriptor(typeof(BehaviourTreeGraph))]
    public sealed class BehaviourTreeGraphDescriptor : GraphDescriptor<BehaviourTreeGraph, GraphDescription>
    {
        public BehaviourTreeGraphDescriptor(BehaviourTreeGraph target) : base(target) { }
    }
}