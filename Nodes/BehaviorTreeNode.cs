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

        public virtual int MaxChildrenLimit => 0;
        
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

