using System.Collections.Generic;
using ArcaneOnyx.GraphCore;

namespace ArcaneOnyx.BehaviorTree.Tests
{
    /// <summary>
    /// A leaf <see cref="BehaviorTreeNode"/> whose result is scripted by the test and that records how
    /// many times each lifecycle hook ran. The composite/decorator nodes drive their children purely
    /// through <see cref="BehaviorTreeNode.OnNodeEnter"/> / <c>OnUpdateInternal</c> / <c>OnNodeExit</c>,
    /// none of which touch the <see cref="BehaviorTreeMachine"/>, so a tree assembled from these doubles
    /// can be ticked in edit mode without standing up a Visual Scripting graph or a live machine.
    /// </summary>
    internal sealed class ScriptedNode : BehaviorTreeNode
    {
        private readonly Queue<ExecutionStatus> scriptedResults = new();

        /// <summary>Returned by <see cref="OnUpdate"/> once the scripted queue is exhausted.</summary>
        public ExecutionStatus DefaultResult { get; set; } = ExecutionStatus.Success;

        public int EnterCalls { get; private set; }
        public int ExitCalls { get; private set; }
        public int UpdateCalls { get; private set; }

        public ScriptedNode() { }

        public ScriptedNode(ExecutionStatus defaultResult)
        {
            DefaultResult = defaultResult;
        }

        public override string NodeName => "Scripted Test Node";

        /// <summary>
        /// Queues a sequence of results returned by successive ticks. After the queue empties the node
        /// falls back to <see cref="DefaultResult"/>.
        /// </summary>
        public ScriptedNode Returns(params ExecutionStatus[] results)
        {
            foreach (var result in results)
            {
                scriptedResults.Enqueue(result);
            }

            return this;
        }

        public override void OnEnter() => EnterCalls++;

        public override void OnExit() => ExitCalls++;

        public override ExecutionStatus OnUpdate()
        {
            UpdateCalls++;
            return scriptedResults.Count > 0 ? scriptedResults.Dequeue() : DefaultResult;
        }
    }

    /// <summary>
    /// A guard that counts how often it was asked. <see cref="BooleanConditionalExecution"/> answers from a
    /// port and so cannot say how many times it ran — and "how many times did this evaluate" is the whole
    /// question when the concern is a guard armed onto its owner more than once.
    /// </summary>
    internal sealed class CountingGuard : ConditionalExecution
    {
        public int Evaluations { get; private set; }

        /// <summary>What <see cref="Evaluate"/> answers. Settable so a test can flip the guard mid-run.</summary>
        public bool Result { get; set; } = true;

        public override string NodeName => "Counting Test Guard";

        public override bool Evaluate()
        {
            Evaluations++;
            return Result;
        }
    }

    /// <summary>A <see cref="Condition"/> whose <see cref="Condition.Evaluate"/> returns a fixed value.</summary>
    internal sealed class FixedCondition : Condition
    {
        private readonly bool result;

        public FixedCondition(bool result) => this.result = result;

        public override string NodeName => "Fixed Test Condition";

        public override bool Evaluate() => result;
    }

    internal static class TreeTestExtensions
    {
        /// <summary>Attaches children to a container and returns the container for fluent setup.</summary>
        public static T WithChildren<T>(this T container, params BehaviorTreeNode[] children)
            where T : ContainerNode
        {
            foreach (var child in children)
            {
                container.AddChild(child);
            }

            return container;
        }

        /// <summary>
        /// Enters the node and ticks it until it stops returning <see cref="ExecutionStatus.Running"/>,
        /// then exits it. Mirrors how <see cref="BehaviorTreeMachine"/> drives the root each frame.
        /// A guard cap turns an accidental infinite loop into a readable assertion failure.
        /// </summary>
        public static ExecutionStatus RunToCompletion(this BehaviorTreeNode node, int maxTicks = 64)
        {
            node.OnNodeEnter();

            var status = ExecutionStatus.Running;
            for (int tick = 0; tick < maxTicks; tick++)
            {
                status = node.OnUpdateInternal();
                if (status != ExecutionStatus.Running)
                {
                    node.OnNodeExit();
                    return status;
                }
            }

            node.OnNodeExit();
            return status;
        }
    }
}
