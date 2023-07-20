using System.Collections.Generic;
using Unity.VisualScripting;

namespace Platinio.BehaviorTree
{
    [GraphContext(typeof(BehaviorTreeGraph))]
    public class BehaviorTreeGraphContext : GraphContext<BehaviorTreeGraph, BehaviorTreeCanvas>
    {
        public BehaviorTreeGraphContext(GraphReference reference) : base(reference) { }

        public override string windowTitle => "Behavior Tree";

        protected override IEnumerable<ISidebarPanelContent> SidebarPanels()
        {
            yield return new GraphInspectorPanel(this);
            yield return new VariablesPanel(this);
        }
    }
}