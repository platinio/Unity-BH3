using System.Collections.Generic;
using System.Linq;
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
        
        private List<TempTargetInfo> temporalTrackedEntities = new();
        public override IEnumerable<TargetInfo<AIEntity>> TrackedEntities => trackedEntities.Concat(temporalTrackedEntities);

        protected override void Update()
        {
            base.Update();
            UpdateTemporalTrackedEntities();
        }

        private void UpdateTemporalTrackedEntities()
        {
            for (int i = temporalTrackedEntities.Count - 1; i >= 0 ; i--)
            {
                var tempTargetInfo = temporalTrackedEntities[i];
                
                tempTargetInfo.RemainingTime -= Time.deltaTime;
                if (tempTargetInfo.RemainingTime < 0)
                {
                    temporalTrackedEntities.Remove(tempTargetInfo);
                }
            }
        }
        
        public void AddTemporalTrackedEntity(AIEntity entity, float time)
        {
            temporalTrackedEntities.Add(new TempTargetInfo(entity, time));
        }

        public override bool IsEntityBeingTracked(GameTargetInfo target)
        {
            if (target.GameEntity == null) return false;
            
            bool isBeingTracked = base.IsEntityBeingTracked(target);
            if (!isBeingTracked) isBeingTracked = temporalTrackedEntities.Where(x => x.GameEntity == target.GameEntity) != null;

            return isBeingTracked;
        }
    }
}