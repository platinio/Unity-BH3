namespace Unity.VisualScripting
{
    [Editor(typeof(StateGraph))]
    public class BehaviorTreeGraphEditor : GraphEditor
    {
        public BehaviorTreeGraphEditor(Metadata metadata) : base(metadata) { }

        private new StateGraph graph => (StateGraph)base.graph;
    }
}