using Platinio.GraphCore;
using Unity.VisualScripting;
using UnityEngine;

namespace Platinio.BehaviorTree
{
    public enum ConditionOperation
    {
        Greater,
        Less
    }

    [GraphCreateMenu("Conditions/Distance")]
    public class Distance : Condition
    {
        [Serialize, Inspectable] private string PositionKey;
        [Serialize, Inspectable] private ConditionOperation Operation;
        [Serialize, Inspectable] private float DistanceValue;

        public override string NodeName => "Distance Condition";

        public override bool Evaluate()
        {
            float d = Vector3.Distance(GetPosition(PositionKey), transform.position);
            switch (Operation)
            {
                case ConditionOperation.Greater:
                    return d > DistanceValue;
                case ConditionOperation.Less:
                    return d < DistanceValue;
            }

            return false;
        }
    }
}

