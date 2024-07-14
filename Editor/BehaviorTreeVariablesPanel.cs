using Platinio.BehaviorTree;

namespace Unity.VisualScripting
{
    public class BehaviorTreeVariablesPanel : VariablesPanel
    {
        public BehaviorTreeVariablesPanel(IGraphContext context) : base(context)
        {
            EditTab(context.reference, tabs[0]);
        }
        
        private void EditTab(GraphReference reference, Tab tab)
        {

            if (reference.scriptableObject is BehaviorTreeGraphAsset behaviorTreeGraphAsset)
            {
                var instanceVariables = behaviorTreeGraphAsset.declarations;
                tab.subTabs.Add(new SubTab("Graph.Instance", tab, VariableKind.Graph, instanceVariables, reference.serializedObject, null, "Instance"));
            }

            tab.MakeFirstSubTabCurrent();
        }
    }
}
