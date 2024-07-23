using Unity.VisualScripting;
using UnityEditor;

namespace Platinio.BehaviorTree
{
   /*
    * When changing tabs there may be moments when an incorrect window type, tries to open the incorrect graph type
    * there should a better solution, but for now we are constantly monitoring that the graph window is using the correct type
    * if it doesnt, it closes the tab and opens a new one
    */
    
    [InitializeOnLoad]
    public class GraphWindowBugWatcher
    {
        static GraphWindowBugWatcher()
        {
            WatchActiveContextChangedBug();
        }

        private static void WatchActiveContextChangedBug()
        {
            GraphWindow.activeContextChanged -= OnWindowActiveContextChange;
            GraphWindow.activeContextChanged += OnWindowActiveContextChange;
            
            AssemblyReloadEvents.afterAssemblyReload -= WatchActiveContextChangedBug;
            AssemblyReloadEvents.afterAssemblyReload += WatchActiveContextChangedBug;
        }

        private static void OnWindowActiveContextChange(IGraphContext context)
        {
            if (context.reference.graph is BehaviorTreeGraph)
            {
                GraphWindow.active.Close();
                GraphCore.GraphWindow.OpenTab<BehaviorTreeGraphWindow>(context.reference);
            }
        }
    }
}