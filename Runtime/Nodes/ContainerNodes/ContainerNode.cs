using System.Collections.Generic;
using System.Linq;
using ArcaneOnyx.GraphCore;
using Unity.VisualScripting;

namespace ArcaneOnyx.BehaviorTree
{
    public abstract class ContainerNode : GameplayNode
    {
        [DoNotSerialize] protected List<BehaviorTreeNode> children;

        public virtual bool CanRunParallelChildren => false;
        public virtual int CurrentChildrenIndex => 0;
        public virtual bool CanExecute => true;
        public override int MaxChildrenLimit => MaxTransitionAmount;
        public override bool ShowIcon => true;
        
        public void AddChild(BehaviorTreeNode child, int index)
        {
            if (children == null) children = new List<BehaviorTreeNode>();
            children.Insert(index, child);
        }
        
        public void AddChild(BehaviorTreeNode child)
        {
            if (children == null) children = new List<BehaviorTreeNode>();
            children.Add(child);
        }

        /// <summary>
        /// Empties the child list so it can be rebuilt from the graph's transitions.
        /// <para>
        /// The list is derived data, not authored data — <see cref="BehaviorTreeGraph.OnAwake"/> builds it by
        /// walking the transitions. Appending to it a second time would give a composite two copies of every
        /// branch, and for a Selector that is two copies of every <em>priority</em>: each branch tried twice
        /// before the next is reached.
        /// </para>
        /// </summary>
        public void ClearChildren()
        {
            children?.Clear();
        }

        public virtual void SortChildren()
        {
            if (children == null || children.Count == 0) return;
            children = children.OrderBy(x => x.Position.x).ToList();
        }

        public List<BehaviorTreeNode> GetChildren()
        {
            if (children == null) children = new List<BehaviorTreeNode>();
            return children;
        }

        public override ExecutionStatus OnUpdate()
        {
            if (CanExecute)
            {
                for (int i = 0; i < GetChildren().Count; i++)
                {
                    GetChildren()[i].OnUpdateInternal();
                }
            }

            return ExecutionStatus.Running;
        }

        public override void OnExit()
        {
            base.OnExit();

            //call children on exit by hand if they havent finished yet
            for (int i = 0; i < GetChildren().Count; i++)
            {
                GetChildren()[i].OnNodeExit();
            }
        }
    }
}