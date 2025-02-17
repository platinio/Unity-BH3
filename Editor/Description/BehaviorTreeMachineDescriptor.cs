using ArcaneOnyx.BehaviorTree;
using Unity.VisualScripting;

namespace ArcaneOnyx.VisualScripting
{
    [Descriptor(typeof(BehaviorTreeMachine))]
    public sealed class BehaviorTreeMachineDescriptor : ArcaneOnyx.GraphCore.MachineDescriptor<BehaviorTreeMachine, MachineDescription>
    {
        public BehaviorTreeMachineDescriptor(BehaviorTreeMachine target) : base(target) { }
    }
}