using System;
using Unity.VisualScripting;

namespace ArcaneOnyx.BehaviorTree
{
    /// <summary>
    /// One input a sub-tree takes, as the calling node remembers it.
    /// <para>
    /// This is a copy of an entry from the sub-tree's required or optional declarations, deliberately stored
    /// on the caller. <c>Definition()</c> runs while a graph is being deserialized and rebuilds every port
    /// from scratch; connections are then resolved by port key, and a key that does not exist yet is dropped
    /// silently. If the ports were declared by reading the sub-tree asset, any load where that asset is not
    /// resolved yet would produce a node with no ports and quietly discard every connection into it — a
    /// failure that depends on import order, so it would appear on one machine and not another.
    /// </para>
    /// <para>
    /// Copying the contract makes port declaration depend on nothing but this node's own serialized data. The
    /// cost is that the copy can fall out of step with the branch, which is the better failure: a comparison
    /// can report it, where a dropped connection cannot.
    /// </para>
    /// </summary>
    [Serializable]
    public class BehaviorTreeGraphParameter
    {
        [Serialize, Inspectable] public string Name { get; set; }

        [Serialize, Inspectable] public Type Type { get; set; }

        /// <summary>
        /// True for a parameter the branch can supply itself. An optional port declares a default and is safe
        /// to leave unconnected; a required one declares none, so leaving it unconnected is the unset-port
        /// case <c>bt_verify</c> already reports rather than a new kind of error.
        /// </summary>
        [Serialize, Inspectable] public bool Optional { get; set; }

        [Serialize, Inspectable] public object DefaultValue { get; set; }

        public BehaviorTreeGraphParameter() { }

        public BehaviorTreeGraphParameter(string name, Type type, bool optional, object defaultValue)
        {
            Name = name;
            Type = type;
            Optional = optional;
            DefaultValue = defaultValue;
        }

        /// <summary>Whether this describes the same input as <paramref name="other"/>, ignoring the default.</summary>
        public bool Matches(BehaviorTreeGraphParameter other)
        {
            return other != null && Name == other.Name && Type == other.Type && Optional == other.Optional;
        }

        public override string ToString() => $"{Name} : {Type?.Name ?? "?"}{(Optional ? " (optional)" : "")}";
    }
}
