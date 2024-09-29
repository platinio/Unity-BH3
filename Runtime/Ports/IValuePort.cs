using System;

namespace ArcaneOnyx.BehaviorTree
{
    public interface IValuePort : IPort
    {
        Type Type { get; }
    }
}