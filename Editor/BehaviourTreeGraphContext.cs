using System.Collections.Generic;
using Unity.VisualScripting;

namespace Platinio.BehaviourTree
{
    [GraphContext(typeof(BehaviourTreeGraph))]
    public class BehaviourTreeGraphContext : GraphContext<BehaviourTreeGraph, BehaviourTreeCanvas>
    {
        public BehaviourTreeGraphContext(GraphReference reference) : base(reference) { }

        public override string windowTitle => "Behaviour Tree";

        protected override IEnumerable<ISidebarPanelContent> SidebarPanels()
        {
            yield return new GraphInspectorPanel(this);
            yield return new VariablesPanel(this);
        }
    }
}