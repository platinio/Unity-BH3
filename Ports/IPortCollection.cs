using Unity.VisualScripting;

namespace ArcaneOnyx.BehaviorTree
{
    public interface IPortCollection<TPort> : IKeyedCollection<string, TPort> where TPort : IPort
    {
        TPort Single();
    }
}