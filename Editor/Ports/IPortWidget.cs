using Unity.VisualScripting;
using UnityEngine;

namespace Platinio.BehaviorTree
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