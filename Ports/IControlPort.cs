namespace Platinio.BehaviorTree
{
    public interface IControlPort : IPort
    {
        bool isPredictable { get; }
        bool couldBeEntered { get; }
    }
}