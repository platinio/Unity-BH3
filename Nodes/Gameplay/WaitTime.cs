using Platinio.GraphCore;
using Unity.VisualScripting;
using UnityEngine;

namespace Platinio.BehaviourTree
{
    [GraphCreateMenu("Gameplay/Wait")]
    public class WaitTime : GameplayNode
    {
        [Serialize] [Inspectable] private float m_time = 0.0f;

        private float m_timer = 0.0f;

        public override string NodeName => $"Wait {m_time} sec";

        public override void OnEnter()
        {
            m_timer = m_time;
        }

        public override ExecutionStatus OnUpdate()
        {
            m_timer -= Time.deltaTime;
            return m_timer > 0 ? ExecutionStatus.Running : ExecutionStatus.Success;
        }
    }
}