using System.Collections.ObjectModel;

namespace Platinio.BehaviorTree
{
    public sealed class BehaviorTreePortDefinitionCollection<T> : Collection<T> where T : IBehaviorTreePortDefinition { }
}