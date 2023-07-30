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
            switch (Operation)
            {
                case ConditionOperation.Greater:
                    return Vector3.Distance(GetPosition(PositionKey), transform.position) < DistanceValue;
                case ConditionOperation.Less:
                    return Vector3.Distance(GetPosition(PositionKey), transform.position) > DistanceValue;
            }

            return false;
        }
    }
}

