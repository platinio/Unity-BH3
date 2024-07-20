using Unity.VisualScripting;

namespace Platinio.BehaviorTree
{
    public interface IPortCollection<TPort> : IKeyedCollection<string, TPort> where TPort : IPort
    {
        TPort Single();
    }
}