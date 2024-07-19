using System;

namespace Platinio.BehaviorTree
{
    public interface IBehaviorTreeValuePortDefinition : IBehaviorTreePortDefinition
    {
        Type type { get; }
    }
}