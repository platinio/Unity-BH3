using System.Collections.Generic;
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

        /// <summary>
        /// Reorders the children after they have been built from the graph's transitions. Does nothing by
        /// default — <see cref="BehaviorTreeGraph.ConvertTransitionNodesIntoTaskNodeChild"/> already delivers
        /// them in priority order, and re-sorting by canvas position here is exactly what used to let a
        /// layout tidy-up change an agent's behaviour.
        /// <para>
        /// Kept as a hook because the random composites are not a fixed order at all:
        /// <see cref="RandomSelector"/> and <see cref="RandomSequence"/> override it to shuffle, and this call
        /// at awake is what gives them their first shuffle.
        /// </para>
        /// </summary>
        public virtual void SortChildren()
        {
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