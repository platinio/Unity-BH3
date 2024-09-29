using ArcaneOnyx.GraphCore;
using UnityEngine;

namespace ArcaneOnyx.BehaviorTree
{
    public interface IPortWidget : IWidget
    {
        IPort port { get; }
        float y { set; }
        Rect handlePosition { get; }
        float GetInnerWidth();
        float GetHeight();
        bool willDisconnect { get; }
    }
}