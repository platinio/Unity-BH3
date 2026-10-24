namespace ArcaneOnyx.BehaviorTree.Debugging
{
    public readonly struct BehaviorTreeEventStamp
    {
        public readonly int Tick;
        public readonly int Sequence;
        public readonly int Frame;
        public readonly float Time;
        public readonly int CallSiteId;

        public BehaviorTreeEventStamp(int tick, int sequence, int frame, float time, int callSiteId)
        {
            Tick = tick;
            Sequence = sequence;
            Frame = frame;
            Time = time;
            CallSiteId = callSiteId;
        }
    }
}
