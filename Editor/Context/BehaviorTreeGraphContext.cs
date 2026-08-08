using System.Collections.Generic;
using Unity.VisualScripting;

namespace ArcaneOnyx.BehaviorTree
{
    [GraphContext(typeof(BehaviorTreeGraph))]
    public class BehaviorTreeGraphContext : GraphCore.GraphContext<BehaviorTreeGraph, BehaviorTreeCanvas>
    {
        public BehaviorTreeGraphContext(GraphCore.GraphReference reference) : base(reference) { }

        public override string windowTitle => "Behavior Tree";

        protected override IEnumerable<ISidebarPanelContent> SidebarPanels()
        {
            yield return new GraphCore.GraphInspectorPanel(this);
            yield return new BehaviorTreeVariablesPanel(this);
            yield return new BehaviorTreeWhyPanel(this);
        }
    }
}