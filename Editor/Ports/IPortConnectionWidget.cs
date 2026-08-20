using UnityEngine;

namespace ArcaneOnyx.BehaviorTree
{
    public interface IPortConnectionWidget : GraphCore.IGraphElementWidget
    {
        Color color { get; }

        /// <summary>Draws the wire translated by <paramref name="offset"/>, for a sub-tree preview.</summary>
        void DrawConnection(Vector2 offset);
    }
}