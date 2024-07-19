using Unity.VisualScripting;

namespace Platinio.BehaviorTree
{
    public interface IBehaviorTreePortCollection<TPort> : IKeyedCollection<string, TPort> where TPort : IBehaviorTreePort
    {
        TPort Single();
    }
}