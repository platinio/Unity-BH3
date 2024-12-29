using ArcaneOnyx.GraphCore;
using Unity.VisualScripting;
using UnityEngine;

namespace ArcaneOnyx.BehaviorTree
{
    [GraphCreateMenu("Unity/Game Object/Instante")]
    public class Instante : GameplayNode
    {
        [DoNotSerialize]
        public ValueInput Prefab { get; private set; }
        [DoNotSerialize]
        public ValueInput InstantiatePosition { get; private set; }
        [DoNotSerialize]
        public ValueInput Rotation { get; private set; }
        
        
        protected override void Definition()
        {
            base.Definition();
           
            Prefab = ValueInput<GameObject>(nameof(Prefab), null);
            InstantiatePosition = ValueInput<Vector3>(nameof(InstantiatePosition), Vector3.zero);
            Rotation = ValueInput<Vector3>(nameof(Rotation), Vector3.zero);
        }
       
    }
}