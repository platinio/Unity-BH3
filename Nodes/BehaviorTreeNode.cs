using Platinio.AIPerception;
using Platinio.Considerations;
using Platinio.GraphCore;
using Platinio.Share;
using UnityEngine;

namespace Platinio.BehaviorTree
{
    /// <summary>
    /// Base class for all behavior tree nodes
    /// </summary>
    public class BehaviorTreeNode : BaseGraphNode<BehaviorTreeGraph, BehaviorTreeNode, BehaviorTreeTransition>
    {
        public virtual DecisionScoreResult CalculateScore()
        {
            return default;
        }

        public virtual void OnTraverse(DecisionScoreResult decisionScoreResult) { }
        
        protected Vector3 GetPosition(string key)
        {
            if (!Machine.Variables.IsDefined(key))
            {
                Debug.LogError($"key: {key} is not define");
                return Vector3.zero;
            }

            object objectValue = Machine.Variables.Get<object>(key);

            if (objectValue is Vector3 position) return position;
            if (objectValue is Transform t) return t.position;
            if (objectValue is GameObject go) return go.transform.position;
            if (objectValue is IGameEntity entity) return entity.transform.position;
            if (objectValue is TargetInfo targetInfo) return targetInfo.RealPosition;

            return Vector3.zero;
        }
    }

}

