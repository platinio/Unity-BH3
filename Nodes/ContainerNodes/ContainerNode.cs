using System.Collections.Generic;
using System.Linq;
using Platinio.GraphCore;
using Unity.VisualScripting;

namespace Platinio.BehaviorTree
{
    public class ContainerNode : BehaviorTreeNode
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
                foreach (var children in children)
                {
                    children.OnUpdateInternal();
                }
            }

            return ExecutionStatus.Running;
        }
    }
}