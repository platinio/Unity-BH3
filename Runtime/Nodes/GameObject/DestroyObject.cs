using ArcaneOnyx.GraphCore;
using Unity.VisualScripting;
using UnityEngine;

namespace ArcaneOnyx.BehaviorTree
{
    [GraphCreateMenu("Unity/Game Object/Destroy Object")]
    public class DestroyObject : GameplayNode
    {
        [DoNotSerialize]
        public ValueInput Object { get; private set; }

        public override string NodeName => "Destroy Object";
        public override string Description => "Destroys the input GameObject, component or asset.";

        protected override void Definition()
        {
            base.Definition();
           
            Object = ValueInput<Object>(nameof(Object), null);
        }

        public override ExecutionStatus OnUpdate()
        {
            UnityEngine.Object.Destroy(Object.GetValueOrDefault<Object>());
            return ExecutionStatus.Success;
        }
    }
}