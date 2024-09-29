using Unity.VisualScripting;
using UnityEngine;

namespace ArcaneOnyx.BehaviorTree
{
    public interface IPortConnectionWidget : GraphCore.IGraphElementWidget
    {
        Color color { get; }
    }
}