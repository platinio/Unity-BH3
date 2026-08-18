using ArcaneOnyx.GraphCore;
using Unity.VisualScripting;
using Object = UnityEngine.Object;

namespace ArcaneOnyx.BehaviorTree
{
    /// <summary>
    /// Reports whether an object reference points at something. The usual case is guarding a branch on
    /// "there is a target at all" before nodes that would otherwise fail silently against a null one.
    /// <para>
    /// Typed as a UnityEngine.Object on purpose: the comparison then honours Unity's destroyed-object
    /// semantics, so a Transform whose GameObject was destroyed reads as null here rather than as a live
    /// reference.
    /// </para>
    /// </summary>
    [GraphCreateMenu("Logic/Is Not Null")]
    public class IsNotNull : GameplayNode
    {
        [DoNotSerialize]
        public ValueInput Value { get; private set; }

        [DoNotSerialize]
        public ValueOutput Result { get; private set; }

        public override string NodeName => "Is Not Null";

        public override string Description => "Outputs true when Value references a live object.";

        public override bool CanBeUsedAsTransitionDestination => false;

        protected override void Definition()
        {
            base.Definition();

            Value = ValueInput<Object>(nameof(Value), null);

            Result = ValueOutput<bool>(nameof(Result), () => Value.GetValueOrDefault<Object>() != null);
        }
    }
}
