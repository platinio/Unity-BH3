using Unity.VisualScripting;

namespace Platinio.BehaviourTree
{
    [Descriptor(typeof(BehaviourTreeGraphAsset))]
    public sealed class BehaviourTreeMacroDescriptor : MacroDescriptor<BehaviourTreeGraphAsset, MacroDescription>
    {
        public BehaviourTreeMacroDescriptor(BehaviourTreeGraphAsset target) : base(target) { }
    }
}