using Unity.VisualScripting;
using UnityEngine;

namespace Platinio.BehaviorTree
{
    public interface IBehaviorTreePortWidget : IWidget
    {
        IBehaviorTreePort port { get; }
        float y { set; }
        Rect handlePosition { get; }
        float GetInnerWidth();
        float GetHeight();
        bool willDisconnect { get; }
    }
}