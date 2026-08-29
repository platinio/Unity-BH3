using ArcaneOnyx.GraphCore;
using Unity.VisualScripting;
using UnityEngine;

namespace ArcaneOnyx.BehaviorTree
{
    [GraphCreateMenu("Unity/Transform/Rotate")]
    public class Rotate : GameplayNode
    {
        [DoNotSerialize]
        public ValueInput Target { get; private set; }
        
        [DoNotSerialize]
        public ValueInput Speed { get; private set; }
        
        [DoNotSerialize]
        public ValueInput Axis { get; private set; }
       
        /// <summary>
        /// What used to be three instance fields. Migrated (spec 07 step 3) — the resolved transform is
        /// this agent's, and so are the values captured at entry.
        /// </summary>
        private sealed class Memory
        {
            public Transform Target;
            public float Speed;
            public Vector3 Axis;
        }

        public override string NodeName => "Rotate";
        public override string Description => "Spins the transform around Axis at Speed degrees per second";

        protected override void Definition()
        {
            base.Definition();

            Target = ValueInput<Object>(nameof(Target), null);

            // 0.0f, not 0. SetDefaultValue type-checks the default against the port, so a boxed int on a
            // float port threw inside Definition -- and Define() catches that and undefines the whole node.
            // This one character is why Rotate had no working ports at all, not merely a missing Axis.
            Speed = ValueInput<float>(nameof(Speed), 0.0f);

            // Declared, or OnEnter's read of it is a guaranteed NullReferenceException and the node breaks
            // its branch the first time anything enters it. Defaults to up, the axis a character turns about.
            Axis = ValueInput<Vector3>(nameof(Axis), Vector3.up);
        }
        
        public override void OnEnter(BTContext ctx)
        {
            var memory = ctx.Memory<Memory>();

            memory.Target = ctx.GetComponent<Transform>(Target);
            memory.Speed = ctx.GetValue<float>(Speed);
            memory.Axis = ctx.GetValue<Vector3>(Axis);
        }

        public override ExecutionStatus OnUpdate(BTContext ctx)
        {
            var memory = ctx.Memory<Memory>();

            // Per second, not per frame. Without deltaTime the same tree spins at a rate set by the machine
            // it runs on, which is the bug that only shows up on someone else's hardware. FaceTarget already
            // scales its speed this way.
            memory.Target.Rotate(memory.Axis, memory.Speed * Time.deltaTime);
            return ExecutionStatus.Running;
        }
    }
}