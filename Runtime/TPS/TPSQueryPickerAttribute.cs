using System;

namespace ArcaneOnyx.BehaviorTree
{
    /// <summary>
    /// Marks a <c>TacticalPositionSelectionQueryItem</c> field to be drawn as the database-grouped query
    /// dropdown instead of a bare object picker.
    /// </summary>
    /// <remarks>
    /// An attribute rather than a type registration because a type registration cannot work here:
    /// <c>InspectorProvider.ResolveDecoratorType</c> hands every <c>UnityEngine.Object</c>-derived type to
    /// the stock <c>UnityObjectInspector</c> before it consults the <c>[Inspector(typeof(X))]</c> registry,
    /// so an inspector registered for the item type is unreachable. <c>GetDecoratedType</c> checks a
    /// member's attributes first, which is the one sanctioned way past that short-circuit.
    /// </remarks>
    [AttributeUsage(AttributeTargets.Field)]
    public sealed class TPSQueryPickerAttribute : Attribute
    {
    }
}
