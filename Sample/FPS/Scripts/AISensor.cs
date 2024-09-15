using Platinio.AI;
using Platinio.AIPerception;
using UnityEngine;

namespace Platinio.BehaviorTree
{
    public class AISensor : Sensor<AIEntity>
    {
        [SerializeField] private float maxRange;
        [SerializeField] private float visionConeAngle;
        [SerializeField] private float memoryTime;

        public override float MaxRange => maxRange;
        public override float VisionConeAngle => visionConeAngle;
        public override float MemoryTime => memoryTime;
    }
}