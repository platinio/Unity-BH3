using System;
using System.Collections.Generic;
using ArcaneOnyx.GraphCore;
using Unity.VisualScripting;
using UnityEngine;

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

        /// <summary>
        /// Ticks a child, entering it first if it is not already running.
        ///
        /// <para>
        /// <b>Entering a child is the tick's job, not the parent's <c>OnEnter</c>'s.</b> A container that
        /// entered its children once and then ticked them every frame afterwards had no way to notice that
        /// an entry had been <em>refused</em>: a guard that says no at the door leaves the child not running,
        /// and the next frame's tick found that guard passing and ran <c>OnUpdate</c> on a node whose
        /// <c>OnEnter</c> never did. That is the stale-state hazard — a <c>WaitTime</c> counting down a timer
        /// it never set, a <c>SetRotation</c> slerping from a pose it never captured — and the branch reports
        /// <em>Success</em> at the end of it, which is far harder to diagnose than a branch that stops.
        /// </para>
        ///
        /// <para>
        /// Asking <see cref="BaseGraphNode{TGraph,TNode,TNodeTransition}.IsRunning"/> is the whole rule, and
        /// it is the one <see cref="Cooldown"/> already followed by hand while the other six decorators did
        /// not. Because entry now happens here and nowhere else, a child is offered the door exactly once per
        /// tick: the refusal and the tick that follows it are one decision rather than two that can disagree.
        /// </para>
        ///
        /// <para>
        /// Deliberately not used by the three containers that already answer this question for themselves.
        /// <see cref="Selector"/> and <see cref="Sequence"/> keep a <c>callOnEnter</c> flag, which says
        /// something <c>IsRunning</c> cannot — <em>this child is next</em>, as distinct from <em>this child
        /// is live</em>. <see cref="Parallel"/> enters all of its children before ticking any of them, and a
        /// child that failed is retired by its status array rather than re-offered, so routing it through
        /// here would interleave entries with ticks and change the order siblings observe each other in,
        /// for no correctness gain.
        /// </para>
        /// </summary>
        protected static ExecutionStatus TickChild(BehaviorTreeNode child)
        {
            if (!child.IsRunning) child.OnNodeEnter();

            return child.OnUpdateInternal();
        }

        /// <summary>
        /// Enters every child, and if one of them throws, exits the ones that already entered before letting
        /// the exception out.
        ///
        /// <para>
        /// A container that enters several children at once has a half-open window in the middle of that
        /// loop. Without the unwind, a child that entered before the failure is stranded: this container's
        /// own entry failed, so it is not running, so <see cref="OnNodeExit"/> returns early and the
        /// <see cref="OnExit"/> sweep that would have exited the children never happens. The child then sits
        /// marked running for the rest of the session — and a later re-entry calls <c>OnNodeEnter</c> on it a
        /// second time with no exit in between, which is the double-entry the composites warn about by name:
        /// a Wait resets its timer, an animation restarts.
        /// </para>
        ///
        /// <para>
        /// The exception is rethrown, not swallowed. Entering children is the container's whole job, so a
        /// container that could not do it has not entered either — this only makes sure it leaves nothing
        /// behind on the way out.
        /// </para>
        /// </summary>
        protected void EnterChildren()
        {
            var toEnter = GetChildren();
            int entered = 0;

            try
            {
                for (; entered < toEnter.Count; entered++)
                {
                    toEnter[entered].OnNodeEnter();
                }
            }
            catch
            {
                // Only the ones that actually got in. The child that threw did not enter -- BaseGraphNode
                // clears its running flag on that path -- and OnNodeExit would do nothing for it anyway.
                for (int i = 0; i < entered; i++)
                {
                    // The sweep has to finish even if an exit throws: a second fault must not strand the
                    // siblings this method exists to release, and the entry exception is the diagnosis.
                    try { toEnter[i].OnNodeExit(); }
                    catch (Exception exitException) { Debug.LogException(exitException); }
                }

                throw;
            }
        }

        public override ExecutionStatus OnUpdate()
        {
            if (CanExecute)
            {
                var toTick = GetChildren();

                for (int i = 0; i < toTick.Count; i++)
                {
                    TickChild(toTick[i]);
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