using Unity.VisualScripting;
using UnityEngine;

namespace ArcaneOnyx.BehaviorTree
{
    public interface IPortConnectionWidget : IGraphElementWidget
    {
        Color color { get; }
    }
}