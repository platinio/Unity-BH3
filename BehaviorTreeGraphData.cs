using Unity.VisualScripting;

namespace Platinio.BehaviorTree
{
    public sealed class BehaviorTreeGraphData : GraphData<BehaviorTreeGraph>, IGraphEventListenerData
    {
        public bool isListening { get; set; }

        public BehaviorTreeGraphData(BehaviorTreeGraph definition) : base(definition)
        {
        }
    }
}