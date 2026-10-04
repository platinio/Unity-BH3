using ArcaneOnyx.BehaviorTree.Authoring;
using UnityEditor;

namespace ArcaneOnyx.BehaviorTree
{
    /// <summary>
    /// Drops everything the canvas has derived from a graph when play mode starts or stops.
    ///
    /// <para>
    /// A play-mode boundary replaces every scene graph, so an entry keyed on an old one can only be stale,
    /// and its <c>CollectionChanged</c> subscription keeps that graph alive. A domain reload used to drop
    /// both for free; with Enter Play Mode Options skipping the reload, this is the only thing that does.
    /// </para>
    ///
    /// <para>
    /// On arrival, not on departure: the canvas still repaints on the way out, so dropping at an Exiting
    /// state only buys a rebuild that the arrival drops again.
    /// </para>
    /// </summary>
    [InitializeOnLoad]
    public static class GraphCachePlayBoundary
    {
        static GraphCachePlayBoundary()
        {
            EditorApplication.playModeStateChanged -= OnPlayModeStateChanged;
            EditorApplication.playModeStateChanged += OnPlayModeStateChanged;
        }

        /// <summary>Public so a test can drive the transition without entering play mode.</summary>
        public static void OnPlayModeStateChanged(PlayModeStateChange change)
        {
            if (change != PlayModeStateChange.EnteredPlayMode && change != PlayModeStateChange.EnteredEditMode)
            {
                return;
            }

            GraphIndex.InvalidateAll();
            NodeProblemCache.Invalidate();
        }
    }
}
