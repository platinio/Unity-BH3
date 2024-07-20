using System.Collections.ObjectModel;

namespace Platinio.BehaviorTree
{
    public sealed class PortDefinitionCollection<T> : Collection<T> where T : IPortDefinition { }
}