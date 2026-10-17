using ArcaneOnyx.BehaviorTree;
using ArcaneOnyx.GraphCore;
using UnityEngine;

namespace ArcaneOnyx.BH3.Samples
{
    [GraphCreateMenu("Samples/Tween/Flash Color")]
    public class FlashColor : GameplayNode
    {
        [Unity.VisualScripting.DoNotSerialize]
        public ValueInput Color { get; private set; }

        [Unity.VisualScripting.DoNotSerialize]
        public ValueInput Duration { get; private set; }

        public override string NodeName => "Flash Color";
        public override string Description => "Tints the agent's renderer with Color and fades back over Duration seconds.";

        private sealed class Memory
        {
            public Renderer Renderer;
            public Color OriginalColor;
            public Tween Flash;

            public bool HasRenderer => Renderer != null;
        }

        protected override void Definition()
        {
            base.Definition();

            Color = ValueInput<Color>(nameof(Color), UnityEngine.Color.red);
            Duration = ValueInput<float>(nameof(Duration), 0.25f);
        }

        public override void OnEnter(BTContext ctx)
        {
            Memory memory = ctx.Memory<Memory>();
            memory.Renderer = ctx.GetComponent<Renderer>();
            if (!memory.HasRenderer) return;

            Color flashColor = ctx.GetValue<Color>(Color);
            float duration = ctx.GetValue<float>(Duration);

            memory.OriginalColor = memory.Renderer.material.color;
            memory.Flash = memory.Renderer.FlashColor(flashColor, duration);
        }

        public override ExecutionStatus OnUpdate(BTContext ctx)
        {
            Memory memory = ctx.Memory<Memory>();
            if (!memory.HasRenderer) return ExecutionStatus.Failure;

            return memory.Flash.IsPlaying ? ExecutionStatus.Running : ExecutionStatus.Success;
        }

        public override void OnExit(BTContext ctx)
        {
            Memory memory = ctx.Memory<Memory>();
            if (!memory.HasRenderer) return;

            memory.Flash.Kill();
            memory.Renderer.material.color = memory.OriginalColor;
        }
    }
}
