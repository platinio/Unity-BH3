namespace ArcaneOnyx.BehaviorTree.Tests
{
    internal sealed class NotATreeGraphAsset : GraphCore.Macro<BehaviorTreeGraph>
    {
        public override BehaviorTreeGraph DefaultGraph() => new BehaviorTreeGraph();
    }
}
