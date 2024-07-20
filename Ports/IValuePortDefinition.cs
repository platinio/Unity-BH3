using System;

namespace Platinio.BehaviorTree
{
    public interface IValuePortDefinition : IPortDefinition
    {
        Type type { get; }
    }
}