using Unity.VisualScripting;
using UnityEngine;

namespace Platinio.BehaviorTree
{
    public interface IPortConnectionWidget : IGraphElementWidget
    {
        Color color { get; }
    }
}