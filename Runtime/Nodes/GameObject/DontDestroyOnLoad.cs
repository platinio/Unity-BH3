using ArcaneOnyx.GraphCore;
using Unity.VisualScripting;
using UnityEngine;

namespace ArcaneOnyx.BehaviorTree
{
    [GraphCreateMenu("Unity/Game Object/Dont Destroy On Load")]
    public class DontDestroyOnLoad : GameplayNode
    {
        [DoNotSerialize]
        public ValueInput Object { get; private set; }

        public override string NodeName => "Dont Destroy On Load";
        public override string Description => "Do not destroy the target Object when loading a new Scene.";

        protected override void Definition()
        {
            base.Definition();
           
            Object = ValueInput<Object>(nameof(Object), null);
        }

        public override ExecutionStatus OnUpdate()
        {
            UnityEngine.Object.DontDestroyOnLoad(Object.GetValueOrDefault<Object>());
            return ExecutionStatus.Success;
        }
    }
}