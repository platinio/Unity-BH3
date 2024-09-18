using Unity.VisualScripting;

namespace ArcaneOnyx.BehaviorTree
{
    public sealed class BehaviorTreeGraphData : GraphData<BehaviorTreeGraph>, IGraphEventListenerData
    {
        public bool isListening { get; set; }

        public BehaviorTreeGraphData(BehaviorTreeGraph definition) : base(definition)
        {
        }
    }
}