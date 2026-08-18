using ArcaneOnyx.GraphCore;
using Unity.VisualScripting;
using UnityEngine;

namespace ArcaneOnyx.BehaviorTree
{
    [GraphCreateMenu("Unity/Game Object/Instante Object")]
    public class InstanteObject : GameplayNode
    {
        [DoNotSerialize]
        public ValueInput Prefab { get; private set; }
        [DoNotSerialize]
        public ValueInput InstantiatePosition { get; private set; }
        [DoNotSerialize]
        public ValueInput Rotation { get; private set; }

        public override string NodeName => "Instantiate Object";
        public override string Description => "Clones the original object";

        protected override void Definition()
        {
            base.Definition();
           
            Prefab = ValueInput<GameObject>(nameof(Prefab), null);
            InstantiatePosition = ValueInput<Vector3>(nameof(InstantiatePosition), Vector3.zero);
            Rotation = ValueInput<Vector3>(nameof(Rotation), Vector3.zero);
        }

        public override ExecutionStatus OnUpdate()
        {
            var prefab = Prefab.GetValueOrDefault<GameObject>();
            Vector3 p = InstantiatePosition.GetValue<Vector3>();
            Vector3 rotation = Rotation.GetValue<Vector3>();

            Object.Instantiate(prefab, p, Quaternion.Euler(rotation));
            return ExecutionStatus.Success;
        }
    }
}