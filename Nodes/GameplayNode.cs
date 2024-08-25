using System.Collections.Generic;
using Unity.VisualScripting;

namespace Platinio.BehaviorTree
{
    /// <summary>
    /// Base action node for behavior trees
    /// </summary>
    public class GameplayNode : BehaviorTreeNode
    {
        [Serialize] private List<ConditionalExecution> conditionalExecutions = new();

        public virtual bool CanUseConditionalExecutions => true;
        public IReadOnlyCollection<ConditionalExecution> ConditionalExecutions => conditionalExecutions;
        
        public void AddConditionalExecution(ConditionalExecution conditionalExecution)
        {
            conditionalExecutions.Add(conditionalExecution);
        }

        public int GetConditionalIndex(ConditionalExecution conditionalExecution)
        {
            return conditionalExecutions.IndexOf(conditionalExecution);
        }
    }
}