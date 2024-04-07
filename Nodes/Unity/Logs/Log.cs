using Platinio.GraphCore;
using Unity.VisualScripting;

namespace Platinio.BehaviorTree
{
    [GraphCreateMenu("Unity/Logs/Log")]
    public class Log : GameplayNode
    {
        [Serialize] [Inspectable] private Vector3BlackboardVariable variable;
    }
}

