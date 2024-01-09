using Platinio.GraphCore;
using Unity.VisualScripting;
using UnityEngine;

namespace Platinio.BehaviorTree
{
    [GraphCreateMenu("Gameplay/Wait")]
    public class WaitTime : GameplayNode
    {
        [Serialize] [Inspectable] private bool useBlackboard = false;
        [Serialize] [Inspectable] private bool useRange = false;
        [Serialize] [Inspectable] private float time = 0.0f;
        [Serialize] [Inspectable] private string timeVariableName;
        [Serialize] [Inspectable] private float minTime = 0.0f;
        [Serialize] [Inspectable] private float maxTime = 0.0f;
        [Serialize] [Inspectable] private string minTimeVariableName;
        [Serialize] [Inspectable] private string maxTimeVariableName;

        private float timer = 0.0f;

        public override string NodeName => $"Wait";

        public override void OnEnter()
        {
            timer = GetInitialTimeValue();
        }

        private float GetInitialTimeValue()
        {
            if (useRange) return GetTimeInRange();
            return GetTime();
        }

        private float GetTimeInRange()
        {
            if (useBlackboard)
            {
                float min = VariableDeclarations.Get<float>(minTimeVariableName);
                float max = VariableDeclarations.Get<float>(maxTimeVariableName);

                return Random.Range(min, max);
            }
            
            return Random.Range(minTime, maxTime);
        }

        private float GetTime()
        {
            if (useBlackboard)
            {
                return VariableDeclarations.Get<float>(timeVariableName);
            }

            return time;
        }

        public override ExecutionStatus OnUpdate()
        {
            timer -= Time.deltaTime;
            return timer > 0 ? ExecutionStatus.Running : ExecutionStatus.Success;
        }
    }
}