using Platinio.GraphCore;
using Unity.VisualScripting;
using UnityEngine;

namespace Platinio.BehaviorTree
{
    [GraphCreateMenu("Unity/Logs/Log")]
    public class Log : GameplayNode
    {
        [Serialize] [Inspectable] private string logText;

        public override string NodeName => "Log";

        public override void OnEnter()
        {
            Debug.Log(logText);
        }

        public override ExecutionStatus OnUpdate() => ExecutionStatus.Success;

    }
}

