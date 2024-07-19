namespace Platinio.BehaviorTree
{
    public interface IBehaviorTreePortDefinition
    {
        string key { get; }
        string label { get; }
        string summary { get; }
        bool hideLabel { get; }
        bool isValid { get; }
    }
}