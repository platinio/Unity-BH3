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
            yield return new BehaviorTreeBreakpointsPanel(this);

            // Opens on the right, opposite the others — it is read alongside them and the timeline rather
            // than instead of them. See BehaviorTreeVariableWatchPanel.preferredAnchor.
            yield return new BehaviorTreeVariableWatchPanel(this);
        }

        /// <summary>
        /// The scrubber goes under the canvas rather than beside it: its axis is time, and a sidebar column
        /// would turn a timeline into a list of events.
        /// </summary>
        protected override IEnumerable<ISidebarPanelContent> BottomPanels()
        {
            yield return new BehaviorTreeTimelinePanel(this);
        }
    }
}