using System.Collections.Generic;
using System.Linq;
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
        protected Vector3 GetPosition(string key)
        {
            if (!Machine.Variables.declarations.IsDefined(key))
            {
                Debug.LogError($"key: {key} is not define");
                return Vector3.zero;
            }

            object objectValue = Machine.Variables.declarations.Get<object>(key);

            if (objectValue is Vector3 position) return position;
            if (objectValue is Transform t) return t.position;
            if (objectValue is GameObject go) return go.transform.position;
            if (objectValue is IGameEntity entity) return entity.transform.position;
            if (objectValue is TargetInfo targetInfo) return targetInfo.RealPosition;
            if (objectValue is List<GameEntity> gameEntities) return gameEntities.FirstOrDefault().transform.position;
            if (objectValue is List<SkillTargetInputVariable> skillTargetInputVariables) return skillTargetInputVariables.FirstOrDefault().GetTargetPosition();
            
            return Vector3.zero;
        }
    }

}

