using System.Reflection;
using Unity.VisualScripting;

namespace ArcaneOnyx.BehaviorTree
{
    /// <summary>
    /// Undefining a name, which <see cref="VariableDeclarations"/> itself will not do.
    /// </summary>
    /// <remarks>
    /// The type can define a name, answer for one and clear the lot, but has no public removal — the backing
    /// collection is private. Reflected once, statically: the lookup is the expensive half and the field
    /// never changes.
    /// <para>
    /// Deliberately not the way agent scope is undefined. That goes through
    /// <see cref="AgentVariableWriter.RemoveAgentVariable"/>, which pairs this with the version bump reactive
    /// guards compare against — the pairing is the whole reason that type exists.
    /// </para>
    /// </remarks>
    internal static class VariableDeclarationsExtensions
    {
        private static readonly FieldInfo CollectionField =
            typeof(VariableDeclarations).GetField("collection", BindingFlags.NonPublic | BindingFlags.Instance);

        /// <returns>Whether the name was there to remove.</returns>
        public static bool Undefine(this VariableDeclarations declarations, string key)
        {
            if (declarations == null || string.IsNullOrEmpty(key)) return false;
            if (!declarations.IsDefined(key)) return false;

            var collection = CollectionField?.GetValue(declarations) as VariableDeclarationCollection;

            return collection != null && collection.Remove(key);
        }
    }
}
