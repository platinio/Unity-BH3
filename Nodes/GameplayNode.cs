using Platinio.AIPerception;
using Platinio.Share;
using UnityEngine;

namespace Platinio.BehaviorTree
{
    /// <summary>
    /// Base action node for behavior trees
    /// </summary>
    public class GameplayNode : BehaviorTreeNode
    {
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