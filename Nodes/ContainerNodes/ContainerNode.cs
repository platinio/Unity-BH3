using System.Collections.Generic;
using System.Linq;
using Platinio.GraphCore;

namespace Platinio.BehaviorTree
{
    public class ContainerNode : BehaviorTreeNode
    {
        protected List<BehaviorTreeNode> m_children;
        
        public virtual int MaxChildren => MaxTransitionAmount;
        public virtual bool CanRunParallelChildren => false;
        public virtual int CurrentChildrenIndex => 0;
        public virtual bool CanExecute => true;

        public void AddChild(BehaviorTreeNode child, int index)
        {
            if (m_children == null) m_children = new List<BehaviorTreeNode>();
            m_children.Insert(index, child);
        }
        
        public void AddChild(BehaviorTreeNode child)
        {
            if (m_children == null) m_children = new List<BehaviorTreeNode>();
            m_children.Add(child);
        }

        public virtual void SortChildren()
        {
            if (m_children == null || m_children.Count == 0) return;
            m_children = m_children.OrderBy(x => x.Position.x).ToList();
        }

        public List<BehaviorTreeNode> GetChildren()
        {
            if (m_children == null) m_children = new List<BehaviorTreeNode>();
            return m_children;
        }

        public override ExecutionStatus OnUpdate()
        {
            if (CanExecute)
            {
                foreach (var children in m_children)
                {
                    children.OnUpdate();
                }
            }

            return ExecutionStatus.Running;
        }
    }
}