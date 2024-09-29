using System;

namespace ArcaneOnyx.BehaviorTree
{
    public interface IValuePortDefinition : IPortDefinition
    {
        Type type { get; }
    }
}