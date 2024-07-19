using Unity.VisualScripting;
using UnityEngine;

namespace Platinio.BehaviorTree
{
    public interface IBehaviorTreeConnectionWidget : IGraphElementWidget
    {
        Color color { get; }
    }
}