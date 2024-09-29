using System.Collections.Generic;
using ArcaneOnyx.GraphCore;
using UnityEngine;


namespace ArcaneOnyx.BehaviorTree
{
    public class BehaviorTreeMachineDebug : MonoBehaviour
    {
        private Stack<BehaviorTreeNode> callStack = new();
        private Dictionary<BehaviorTreeNode, ExecutionStatus> lastKnowExecutionStatus = new();

        public void PushNodeToCallStack(BehaviorTreeNode node)
        {
            lastKnowExecutionStatus.Remove(node);
            callStack.Push(node);
        }

        public void PopCallStack()
        {
            var node = callStack.Pop();
            lastKnowExecutionStatus[node] = node.LastExecutionStatus;
        }
    }
}

