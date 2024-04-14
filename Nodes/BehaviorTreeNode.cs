using Platinio.GraphCore;
using UnityEngine;

namespace Platinio.BehaviorTree
{
    /// <summary>
    /// Base class for all behavior tree nodes
    /// </summary>
    public class BehaviorTreeNode : BaseGraphNode<BehaviorTreeGraph, BehaviorTreeNode, BehaviorTreeTransition>
    {
        private BehaviorTreeMachineDebug machineDebug;
        protected BehaviorTreeMachine BehaviorTreeMachine => Machine as BehaviorTreeMachine;
        
        protected virtual Vector3 GetPosition(string key)
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
           
            return Vector3.zero;
        }
        
        protected GameObject GetTargetGameObject(GameObjectBlackboardVariable gameObjectVariable)
        {
            var target = gameObjectVariable.GetValue(BehaviorTreeMachine);
            return target == null ? gameObject : target;
        }

        protected BehaviorTreeMachineDebug GetMachineDebug()
        {
            if (machineDebug == null)
            {
                machineDebug = gameObject.GetComponent<BehaviorTreeMachineDebug>();
            }

            return machineDebug;
        }

        public override void OnNodeEnter()
        {
            base.OnNodeEnter();

#if UNITY_EDITOR
            var debugComponent = GetMachineDebug();
            if (debugComponent == null) return;
            
            debugComponent.PushNodeToCallStack(this);
#endif
        }

        public override void OnNodeExit()
        {
            base.OnNodeExit();
            
#if UNITY_EDITOR
            var debugComponent = GetMachineDebug();
            if (debugComponent == null) return;
            
            debugComponent.PopCallStack();
#endif
        }
    }
}

