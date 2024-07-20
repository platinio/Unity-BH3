using System;

namespace Platinio.BehaviorTree
{
    public interface IValuePort : IPort
    {
        Type type { get; }
    }
}