using ArcaneOnyx.GraphCore;
using Unity.VisualScripting;
using UnityEngine;

namespace ArcaneOnyx.BehaviorTree
{
    // The menu path is spelled properly; the class name is not, and stays that way deliberately --
    // the class name is what assets serialize, so correcting it would break every tree holding one
    // unless the deserializer is taught the old name as an alias first.
    [GraphCreateMenu("Unity/Game Object/Instantiate Object")]
    public class InstantiateObject : GameplayNode
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