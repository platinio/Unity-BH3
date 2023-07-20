using Unity.VisualScripting;

namespace Platinio.BehaviorTree
{
    [Descriptor(typeof(BehaviorTreeGraphAsset))]
    public sealed class BehaviorTreeMacroDescriptor : MacroDescriptor<BehaviorTreeGraphAsset, MacroDescription>
    {
        public BehaviorTreeMacroDescriptor(BehaviorTreeGraphAsset target) : base(target) { }
    }
}