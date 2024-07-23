using Unity.VisualScripting;
using GraphWindow = Platinio.GraphCore.GraphWindow;

namespace Platinio.BehaviorTree
{
    public class BehaviorTreeGraphWindow : GraphWindow
    {
        protected override void ToggleVariablesPanel(bool enabled)
        {
            ToggleInspector<BehaviorTreeVariablesPanel>(enabled);
        }

        protected override void OnGUI()
        {
            //if we try to open a flow graph with a behavior tree it will cause some probles
            //the solution for now open the correct window and close this
            if (reference != null && reference.graph is FlowGraph)
            {
                Unity.VisualScripting.GraphWindow.OpenTab(reference);
                Close();
            }
            
            base.OnGUI();
        }
    }
}

