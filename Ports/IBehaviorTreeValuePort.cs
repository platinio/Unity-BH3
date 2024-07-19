using System;

namespace Platinio.BehaviorTree
{
    public interface IBehaviorTreeValuePort : IBehaviorTreePort
    {
        Type type { get; }
    }
}