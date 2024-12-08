using ArcaneOnyx.Share;

namespace ArcaneOnyx.BehaviorTree.Sample
{
    [System.Serializable]
    public class TempTargetInfo : GameTargetInfo
    {
        public float RemainingTime;

        public TempTargetInfo(AIEntity gameEntity, float time)
        {
            this.gameEntity = gameEntity;
            RemainingTime = time;
        }
    }
}