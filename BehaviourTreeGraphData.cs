using Unity.VisualScripting;

namespace Platinio.BehaviourTree
{
    public sealed class BehaviourTreeGraphData : GraphData<BehaviourTreeGraph>, IGraphEventListenerData
    {
        public bool isListening { get; set; }

        public BehaviourTreeGraphData(BehaviourTreeGraph definition) : base(definition)
        {
        }
    }
}