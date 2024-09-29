using ArcaneOnyx.BehaviorTree;

namespace Unity.VisualScripting
{
    [Descriptor(typeof(BehaviorTreeMachine))]
    public sealed class BehaviorTreeMachineDescriptor : ArcaneOnyx.GraphCore.MachineDescriptor<BehaviorTreeMachine, MachineDescription>
    {
        public BehaviorTreeMachineDescriptor(BehaviorTreeMachine target) : base(target) { }
    }
}