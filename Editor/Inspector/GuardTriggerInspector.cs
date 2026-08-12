using Unity.VisualScripting;

namespace ArcaneOnyx.BehaviorTree
{
    /// <summary>
    /// Draws a <see cref="GuardTrigger"/> in the Blackboard/node inspector.
    ///
    /// <para>
    /// Without this the trigger list renders as <i>"No Inspector for Guard Trigger"</i>. Visual Scripting
    /// resolves an inspector <b>per type</b> and has no fallback for an arbitrary class: <c>[Inspectable]</c>
    /// on the members only says which members are eligible once something is drawing the type, and
    /// <c>[Serialize]</c> only concerns the serializer. Neither registers a drawer.
    /// </para>
    ///
    /// <para>
    /// Unity's own inspector generation does not cover this either. A trigger is stored inside a
    /// <see cref="ReactiveGuard"/>, which is serialized by Visual Scripting's serializer rather than by
    /// Unity's <c>[SerializeField]</c> path, so there is no <c>SerializedProperty</c> for a Unity
    /// <c>PropertyDrawer</c> to bind to. The drawer has to live in Visual Scripting's system.
    /// </para>
    ///
    /// <para>
    /// <see cref="ReflectedInspector"/> is all it takes — the same base
    /// <see cref="BehaviorTreeNodeInspector"/> uses. It reflects over the members and lays them out, which is
    /// the whole requirement here: a kind dropdown and the fields belonging to it.
    /// </para>
    /// </summary>
    [Inspector(typeof(GuardTrigger))]
    public class GuardTriggerInspector : ReflectedInspector
    {
        public GuardTriggerInspector(Metadata metadata) : base(metadata) { }
    }
}
