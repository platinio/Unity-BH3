namespace ArcaneOnyx.BehaviorTree
{
    /// <summary>
    /// A container that wraps exactly one child and rewrites how its result is read.
    ///
    /// <para>
    /// Carries no lifecycle of its own on purpose. It used to enter the child in <c>OnEnter</c>, which put
    /// the "should this child start?" decision in one place and the tick that depended on it in another —
    /// and six of the seven decorators then ticked a child whose entry had been refused. Entry belongs with
    /// the tick, in <see cref="ContainerNode.TickChild"/>, so each subclass reaches for its child exactly
    /// once and gets both halves.
    /// </para>
    /// </summary>
    public class Decorator : ContainerNode
    {
    }
}
