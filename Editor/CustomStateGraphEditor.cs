namespace Unity.VisualScripting
{
    [Editor(typeof(StateGraph))]
    public class BehaviourTreeGraphEditor : GraphEditor
    {
        public BehaviourTreeGraphEditor(Metadata metadata) : base(metadata) { }

        private new StateGraph graph => (StateGraph)base.graph;
    }
}