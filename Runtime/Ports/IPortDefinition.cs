namespace ArcaneOnyx.BehaviorTree
{
    public interface IPortDefinition
    {
        string key { get; }
        string label { get; }
        string summary { get; }
        bool hideLabel { get; }
        bool isValid { get; }
    }
}