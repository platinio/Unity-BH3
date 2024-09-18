using ArcaneOnyx.Share;
using Platinio.AIPerception;
using UnityEngine;

namespace ArcaneOnyx.BehaviorTree.Sample
{
    public class AISensor : Sensor<GameTargetInfo, AIEntity>
    {
        [SerializeField] private float maxRange;
        [SerializeField] private float visionConeAngle;
        [SerializeField] private float memoryTime;

        public override float MaxRange => maxRange;
        public override float VisionConeAngle => visionConeAngle;
        public override float MemoryTime => memoryTime;
    }
}