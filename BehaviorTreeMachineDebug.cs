using System.Collections.Generic;
using Platinio.GraphCore;
using UnityEngine;


namespace Platinio.BehaviorTree
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

