using ArcaneOnyx.GraphCore;
using Unity.VisualScripting;
using UnityEngine;

namespace ArcaneOnyx.BehaviorTree
{
    [GraphCreateMenu("Unity/Logs/Log")]
    public class Log : GameplayNode
    {
        [Serialize] [Inspectable] private string logText;

        public override string NodeName => "Log";

        public override ExecutionStatus OnUpdate()
        {
            Debug.Log(logText);
            return ExecutionStatus.Success;
        }

    }
}

