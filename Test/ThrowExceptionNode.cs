using System;
using ArcaneOnyx.GraphCore;

namespace ArcaneOnyx.BehaviorTree.Test
{
    [GraphCreateMenu("Test/Throw Exception")]
    public class ThrowExceptionNode : GameplayNode
    {
        public override string NodeName => "ThrowExceptionNode";

        public override ExecutionStatus OnUpdate()
        {
            throw new Exception("This is a test exception");
        }
    }
}
