using ArcaneOnyx.BehaviorTree;

namespace Unity.VisualScripting
{
    [Descriptor(typeof(BehaviorTreeMachine))]
    public sealed class BehaviorTreeMachineDescriptor : MachineDescriptor<BehaviorTreeMachine, MachineDescription>
    {
        public BehaviorTreeMachineDescriptor(BehaviorTreeMachine target) : base(target) { }
    }
}