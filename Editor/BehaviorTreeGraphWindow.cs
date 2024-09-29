using GraphWindow = ArcaneOnyx.GraphCore.GraphWindow;

namespace ArcaneOnyx.BehaviorTree
{
    public class BehaviorTreeGraphWindow : GraphWindow
    {
        protected override void ToggleVariablesPanel(bool enabled)
        {
            ToggleInspector<BehaviorTreeVariablesPanel>(enabled);
        }
    }
}

