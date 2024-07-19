namespace Platinio.BehaviorTree
{
    public interface IBehaviorTreeControlPort : IBehaviorTreePort
    {
        bool isPredictable { get; }
        bool couldBeEntered { get; }
    }
}