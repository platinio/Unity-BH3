using Unity.VisualScripting;

namespace ArcaneOnyx.BehaviorTree
{
    public class BehaviorTreeVariablesPanel : ArcaneOnyx.GraphCore.VariablesPanel
    {
        public BehaviorTreeVariablesPanel(GraphCore.IGraphContext context) : base(context)
        {
            EditTab(context.reference, tabs[0]);
        }
        
        private void EditTab(GraphCore.GraphReference reference, Tab tab)
        {

            if (reference.scriptableObject is BehaviorTreeGraphAsset behaviorTreeGraphAsset)
            {
                var instanceVariables = behaviorTreeGraphAsset.declarations;
                tab.subTabs.Add(new SubTab("Graph.Instance", tab, VariableKind.Graph, instanceVariables, reference.serializedObject, null, "Instance"));
               
                tab.subTabs.Add(new SubTab("Graph.Required", tab, VariableKind.Graph,
                    behaviorTreeGraphAsset.requiredDeclarations, reference.serializedObject, null, "Required",
                    "An agent running this tree must declare these. Nothing reads them at runtime they are " +
                    "the contract a branch publishes so a missing variable is caught before play."));

                tab.subTabs.Add(new SubTab("Graph.Optional", tab, VariableKind.Graph,
                    behaviorTreeGraphAsset.optionalDeclarations, reference.serializedObject, null, "Optional",
                    "Defaults this tree supplies for itself when the agent does not declare them."));
            }

            // NOTE: VariablesPanel rebuilds this tab's sub tabs on entering play mode, keeping only
            // Graph.Runtime, so Required and Optional are edit mode only. That is deliberate — in play mode
            // the panel should show the values the machine is actually reading, not the contract.
            tab.MakeFirstSubTabCurrent();
        }
    }
}
