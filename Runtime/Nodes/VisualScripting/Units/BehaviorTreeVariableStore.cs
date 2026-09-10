using UnityEngine;

namespace ArcaneOnyx.BehaviorTree
{
    /// <summary>
    /// Resolves which variable store a <see cref="BehaviorTreeVariableKind"/> refers to, from inside a
    /// running flow.
    ///
    /// <para>
    /// Shared by the Set and Get units so the two can never disagree about where a name lives — a get that
    /// looked somewhere other than the matching set would produce a bug that reads like a race and isn't
    /// one. Deliberately a static helper rather than a shared base class: the obvious base,
    /// <c>UnifiedVariableUnit</c>, carries an inherited <c>[SpecialUnit]</c> that hides subclasses from the
    /// node library, and inheritance is not worth re-opening that door for one switch statement.
    /// </para>
    /// </summary>
    // Fully qualified throughout: inside this namespace the bare names ValueInput and ValueOutput are the
    // behavior tree's own port types, not Visual Scripting's, and a `using` does not change that.
    internal static class BehaviorTreeVariableStore
    {
        /// <summary>
        /// The declarations a kind points at, or null when there is nowhere valid to look — an Object kind
        /// with no target, or a scene that is not loaded. Null is a real answer here rather than an error:
        /// the callers report it differently, because failing to read is ordinary and failing to write is not.
        /// </summary>
        public static Unity.VisualScripting.VariableDeclarations Of(
            Unity.VisualScripting.Flow flow,
            BehaviorTreeVariableKind kind,
            Unity.VisualScripting.ValueInput objectPort)
        {
            switch (kind)
            {
                // The script graph's own variables. A script graph has no tree branch scope to offer.
                case BehaviorTreeVariableKind.Graph:
                    return Unity.VisualScripting.Variables.Graph(flow.stack);

                case BehaviorTreeVariableKind.Object:
                    var target = objectPort != null ? flow.GetValue<GameObject>(objectPort) : null;
                    return target != null ? Unity.VisualScripting.Variables.Object(target) : null;

                case BehaviorTreeVariableKind.Scene:
                    return IsSceneUsable(flow) ? Unity.VisualScripting.Variables.Scene(flow.stack.scene) : null;

                case BehaviorTreeVariableKind.Application:
                    return Unity.VisualScripting.Variables.Application;

                case BehaviorTreeVariableKind.Saved:
                    return Unity.VisualScripting.Variables.Saved;

                // None never reaches here from a unit; both resolve the kind first and throw naming
                // themselves. Nowhere to look is still the honest answer for anything else that asks.
                default:
                    return null;
            }
        }

        /// <summary>
        /// Whether the flow's scene can hold variables at all. Asking for scene variables on an unloaded or
        /// invalid scene throws, and a unit is evaluated in the editor for value prediction as well as at
        /// runtime.
        /// </summary>
        public static bool IsSceneUsable(Unity.VisualScripting.Flow flow)
        {
            var scene = flow.stack.scene;

            return scene.HasValue && scene.Value.IsValid() && scene.Value.isLoaded;
        }
    }
}
