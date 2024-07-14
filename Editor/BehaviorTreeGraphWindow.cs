using GraphWindow = Platinio.GraphCore.GraphWindow;

namespace Platinio.BehaviorTree
{
    public class BehaviorTreeGraphWindow : GraphWindow
    {
        protected override void ToggleVariablesPanel(bool enabled)
        {
            ToggleInspector<BehaviorTreeVariablesPanel>(enabled);
        }
    }
}

