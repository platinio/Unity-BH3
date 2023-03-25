using Platinio.BehaviourTree;

namespace Unity.VisualScripting
{
    [Descriptor(typeof(BehaviourTreeMachine))]
    public sealed class BehaviourTreeMachineDescriptor : MachineDescriptor<BehaviourTreeMachine, MachineDescription>
    {
        public BehaviourTreeMachineDescriptor(BehaviourTreeMachine target) : base(target) { }
    }
}