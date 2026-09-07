using System;
using System.Collections.Generic;
using ArcaneOnyx.GraphCore;
using Unity.VisualScripting;

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

    /// <summary>
    /// <see cref="CountingGuard"/>'s reactive twin — the one to reach for whenever a test needs a guard that
    /// keeps watching. A plain <see cref="ConditionalExecution"/> is evaluated at entry and never again, so
    /// any fixture about aborting, re-evaluation, or per-tick cost has to use this one or it is measuring
    /// the doorman and calling it the watchman.
    /// </summary>
    internal sealed class CountingReactiveGuard : ReactiveGuard
    {
        public int Evaluations { get; private set; }

        /// <summary>What <see cref="Evaluate"/> answers. Settable so a test can flip the guard mid-run.</summary>
        public bool Result { get; set; } = true;

        public override string NodeName => "Counting Test Reactive Guard";

        public override bool Evaluate()
        {
            Evaluations++;
            return Result;
        }
    }

    /// <summary>
    /// A leaf that never finishes, and counts its own entries and exits.
    /// <para>
    /// Exists alongside <see cref="ScriptedNode"/> for the sub-tree fixtures specifically. A sub-tree is
    /// <c>Object.Instantiate</c>d per call site, so the node that actually runs is a <em>clone</em> of the one
    /// the test authored — and a double whose result comes from a serialized queue would arrive in the clone
    /// with that queue empty, quietly returning something other than what the test asked for. This one's
    /// result is structural, so the clone behaves identically to the original by construction.
    /// </para>
    /// </summary>
    internal sealed class AlwaysRunningNode : BehaviorTreeNode
    {
        public int EnterCalls { get; private set; }
        public int ExitCalls { get; private set; }

        public override string NodeName => "Always Running Test Node";

        public override void OnEnter() => EnterCalls++;

        public override void OnExit() => ExitCalls++;

        public override ExecutionStatus OnUpdate() => ExecutionStatus.Running;
    }

    /// <summary>
    /// A guard that answers differently every time it is asked — false first, then true.
    ///
    /// <para>
    /// Nothing promises a guard is stable within a frame, and this is the cheapest honest stand-in for the
    /// ones that are not: a <c>RandomChance</c> that re-rolls, or a condition reading a fact another agent
    /// writes between two calls in the same tick. A fixture using <see cref="CountingGuard"/> and flipping
    /// <c>Result</c> by hand cannot express it, because the flip would happen at a moment the test chose
    /// rather than at the moment the engine happens to ask twice.
    /// </para>
    /// </summary>
    internal sealed class UnstableGuard : ConditionalExecution
    {
        public int Evaluations { get; private set; }

        public override string NodeName => "Unstable Test Guard";

        public override bool Evaluate()
        {
            Evaluations++;
            return Evaluations > 1;
        }
    }

    /// <summary>
    /// A node that publishes one value on a <see cref="ValueOutput"/> declared as <typeparamref name="T"/>.
    ///
    /// <para>
    /// Exists because the shipped <see cref="Literal"/> nodes cannot express the case that matters here: a
    /// port carrying a type that merely <em>converts</em> to the consumer's. A test needs to control the
    /// declared output type and the published value independently, and it must not be hostage to whether
    /// some literal's backing field happens to match the port it advertises — that was its own bug, and a
    /// fixture built on it would pass or fail for reasons that have nothing to do with conversion.
    /// </para>
    /// </summary>
    internal sealed class ValueSource<T> : BehaviorTreeNode
    {
        /// <summary>What the port publishes. Settable so one node can serve several assertions.</summary>
        public T Published { get; set; }

        [DoNotSerialize]
        public ValueOutput Value { get; private set; }

        public override string NodeName => $"Test {typeof(T).Name} Source";

        protected override void Definition()
        {
            base.Definition();

            Value = ValueOutput<T>(nameof(Value), () => Published);
        }
    }

    /// <summary>
    /// A node whose <see cref="OnEnter"/> throws, and whose <see cref="OnUpdate"/> would report
    /// <see cref="ExecutionStatus.Success"/> if anything ever ticked it.
    ///
    /// <para>
    /// The <c>Success</c> is the whole point. A node left flagged running after a failed entry gets ticked
    /// rather than re-entered, and the damage is not the exception — it is the branch afterwards reporting
    /// that it finished. <c>WaitTime</c> is the real instance: its timer is assigned in <c>OnEnter</c>, so
    /// a tick after a failed entry counts down from zero and succeeds as though the wait had elapsed.
    /// </para>
    /// </summary>
    internal sealed class ThrowsOnEnterNode : BehaviorTreeNode
    {
        public int UpdateCalls { get; private set; }
        public int ExitCalls { get; private set; }

        public override string NodeName => "Throws On Enter Test Node";

        public override void OnEnter() => throw new InvalidOperationException("OnEnter failed");

        public override void OnExit() => ExitCalls++;

        public override ExecutionStatus OnUpdate()
        {
            UpdateCalls++;
            return ExecutionStatus.Success;
        }
    }

    /// <summary>
    /// Enters normally and throws on the way out. Models the second fault in an unwind: the container is
    /// already exiting the children it entered because a later one failed to enter.
    /// </summary>
    internal sealed class ThrowsOnExitNode : BehaviorTreeNode
    {
        public int EnterCalls { get; private set; }

        /// <summary>
        /// What each tick answers. Running by default, so the node is mid-flight when something exits it;
        /// a composite test sets Success or Failure to make the composite itself the thing that exits it.
        /// </summary>
        public ExecutionStatus Result { get; set; } = ExecutionStatus.Running;

        public override string NodeName => "Throws On Exit Test Node";

        public override void OnEnter() => EnterCalls++;

        public override void OnExit() => throw new InvalidOperationException("OnExit failed");

        public override ExecutionStatus OnUpdate() => Result;
    }

    /// <summary>
    /// A node with one <see cref="ValueInput"/> declared as <typeparamref name="T"/> and nothing else —
    /// <see cref="ValueSource{T}"/>'s counterpart, so a test can stand up any (output type, input type) pair
    /// and ask the two questions the production code asks: may this connect, and can the value be read.
    /// </summary>
    internal sealed class ValueSink<T> : BehaviorTreeNode
    {
        [DoNotSerialize]
        public ValueInput Value { get; private set; }

        public override string NodeName => $"Test {typeof(T).Name} Sink";

        protected override void Definition()
        {
            base.Definition();

            // Declared without a default on purpose: a default would mask an unread connection, and the
            // unfed-port behaviour is itself one of the things these fixtures pin.
            Value = ValueInput<T>(nameof(Value));
        }
    }

    /// <summary>
    /// Declares two ports under one key.
    ///
    /// <para>
    /// Nothing shipped can do this by accident -- every node keys its ports with <c>nameof</c>, so a
    /// collision needs a subclass shadowing a name its base class already used. This double is that shape,
    /// written on purpose, because the protection against it is a property of the collection type rather
    /// than anything visible at the call site: swap <c>PortCollection</c> for a plain list and it is gone
    /// with nothing to notice.
    /// </para>
    /// </summary>
    internal sealed class DuplicateKeyNode : BehaviorTreeNode
    {
        public const string SharedKey = "Shared";

        public override string NodeName => "Duplicate Key Test Node";

        protected override void Definition()
        {
            base.Definition();

            ValueInput<float>(SharedKey);
            ValueInput<float>(SharedKey);
        }

        public override ExecutionStatus OnUpdate() => ExecutionStatus.Success;
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
