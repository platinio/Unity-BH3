using Unity.VisualScripting;

namespace ArcaneOnyx.BehaviorTree
{
    [Descriptor(typeof(BehaviorTreeGraphAsset))]
    public sealed class BehaviorTreeMacroDescriptor : MacroDescriptor<BehaviorTreeGraphAsset, MacroDescription>
    {
        public BehaviorTreeMacroDescriptor(BehaviorTreeGraphAsset target) : base(target) { }
    }
}