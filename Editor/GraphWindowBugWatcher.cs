using Unity.VisualScripting;
using UnityEditor;

namespace ArcaneOnyx.BehaviorTree
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

        private static bool windowWasOpen;
        private static bool needsToClearSelection;
        private static bool behaviorTreeIsOpen = false;
        
        private static void WatchActiveContextChangedBug()
        {
            //EditorApplication.update -= Update;
            //EditorApplication.update += Update;
            
            //GraphWindow.activeContextChanged -= OnWindowActiveContextChange;
            //GraphWindow.activeContextChanged += OnWindowActiveContextChange;
            
            //AssemblyReloadEvents.afterAssemblyReload -= WatchActiveContextChangedBug;
            //AssemblyReloadEvents.afterAssemblyReload += WatchActiveContextChangedBug;
        }

        private static void Update()
        {
            if ((GraphWindow.active as object) != null && !GraphWindow.active.hasFocus && behaviorTreeIsOpen)
            {
                windowWasOpen = false;
                behaviorTreeIsOpen = false;
                GraphWindow.active.context.selection.Clear();
                GraphClipboard.groupClipboard.Clear();
            }
            
            if (!windowWasOpen) windowWasOpen = (GraphWindow.active as object) != null && GraphWindow.active.hasFocus;
            if (!behaviorTreeIsOpen) behaviorTreeIsOpen = (GraphCore.GraphWindow.active as object) != null && GraphCore.GraphWindow.active.hasFocus;

            /*
            if (windowWasOpen)
            {
                GraphWindow.active.context.selection.Clear();
                GraphClipboard.groupClipboard.Clear();
            }

            windowWasOpen = (GraphWindow.active as object) != null && GraphWindow.active.hasFocus;*/
        }
    }
}