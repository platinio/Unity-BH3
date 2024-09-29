using System.Collections.ObjectModel;

namespace ArcaneOnyx.BehaviorTree
{
    public sealed class PortDefinitionCollection<T> : Collection<T> where T : IPortDefinition { }
}