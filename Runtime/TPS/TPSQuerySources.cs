#if MODULE_TACTICAL_POSITION_SELECTION_EXIST
using ArcaneOnyx.UnityTacticalPositionSelection;

namespace ArcaneOnyx.BehaviorTree
{
    /// <summary>How both TPS nodes read a query source off their Query port and their saved data.</summary>
    internal static class TPSQuerySources
    {
        /// <summary>
        /// The source, or null when it is a deleted query item: a destroyed asset is Unity-null but not C#-null, so it
        /// would otherwise pass every null check behind an interface.
        /// </summary>
        public static IPositionQuerySource Live(IPositionQuerySource source)
            => source is UnityEngine.Object asset && asset == null ? null : source;

        /// <summary>
        /// The preset a tree saved as the Query port's inline value, from BH3 1.0.10 to 1.0.14 when the port took a
        /// query item; null when there is none. Read before <c>Define</c>, which drops values a port no longer declares.
        /// </summary>
        public static TacticalPositionSelectionQueryItem InlinePickSavedOnThePort(BehaviorTreeNode node, string portKey)
            => node.defaultValues.TryGetValue(portKey, out var picked) ? picked as TacticalPositionSelectionQueryItem : null;
    }
}
#endif
