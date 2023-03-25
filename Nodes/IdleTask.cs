using Platinio.GraphCore;
using UnityEngine;

namespace Platinio.BehaviourTree
{
    [GraphCreateMenu("Action/Create Idle")]
    public class IdleTask : ActionNode
    {
        public override string NodeName => "Idle";
        protected override string NodeIconPath => "NodeIcons/Idle";
        public float IdleTime = 3;

        public override void OnEnter()
        {
            IdleTime = 30;
        }

        public override ExecutionStatus OnUpdate()
        {
            IdleTime -= Time.deltaTime;
            return IdleTime > 0? ExecutionStatus.Running : ExecutionStatus.Success;
        }
    }
}

