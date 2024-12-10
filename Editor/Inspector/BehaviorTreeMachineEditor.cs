using Unity.VisualScripting;

namespace ArcaneOnyx.BehaviorTree
{
    [Editor(typeof(BehaviorTreeMachine))]
    public class BehaviorTreeMachineEditor : MachineEditor
    {
        public BehaviorTreeMachineEditor(Metadata metadata) : base(metadata) { }
    }
}

