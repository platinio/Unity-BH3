using System.Collections;
using System.Collections.Generic;
using ArcaneOnyx.GraphCore;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace ArcaneOnyx.BehaviorTree.Tests.PlayMode
{
    /// <summary>
    /// Play-mode coverage for the composite execution model running across real frames of the player
    /// loop — the way <see cref="BehaviorTreeMachine"/> actually ticks the tree (once per Update). The
    /// edit-mode suite already pins the per-tick semantics; these confirm the same behaviour holds when a
    /// branch is advanced one tick per frame instead of in a tight loop. The execution model is
    /// machine-independent (children are driven through OnNodeEnter / OnUpdateInternal / OnNodeExit), so
    /// these tests avoid standing up a DI-wired GameObject and drive the root node directly.
    /// </summary>
    public class BehaviorTreePlayModeTests
    {
        [UnityTest]
        public IEnumerator Sequence_AdvancesAsEachChildFinishes_AndSucceeds()
        {
            // Each child takes one frame: Running on the frame it is entered, Success on the next. The
            // frames here are spent by the children actually working, which is the only thing that may cost
            // one — the sequence itself moves on to the next child within the tick that freed it, so this
            // takes three frames and not four.
            var a = new RecordingPlayNode().Returns(ExecutionStatus.Running, ExecutionStatus.Success);
            var b = new RecordingPlayNode().Returns(ExecutionStatus.Running, ExecutionStatus.Success);
            var sequence = new Sequence();
            sequence.AddChild(a);
            sequence.AddChild(b);

            sequence.OnNodeEnter();

            var status = ExecutionStatus.Running;
            for (int frame = 0; frame < 16 && status == ExecutionStatus.Running; frame++)
            {
                status = sequence.OnUpdateInternal();
                yield return null;
            }

            Assert.AreEqual(ExecutionStatus.Success, status);
            Assert.AreEqual(1, a.EnterCalls);
            Assert.AreEqual(1, b.EnterCalls);
            Assert.AreEqual(1, a.ExitCalls);
            Assert.AreEqual(1, b.ExitCalls);
        }

        [UnityTest]
        public IEnumerator Repeater_KeepsRunningAcrossFrames()
        {
            var child = new RecordingPlayNode { DefaultResult = ExecutionStatus.Success };
            var repeater = new Repeater();
            repeater.AddChild(child);

            repeater.OnNodeEnter();

            for (int frame = 0; frame < 5; frame++)
            {
                Assert.AreEqual(ExecutionStatus.Running, repeater.OnUpdateInternal(),
                    "A Repeater never completes; it should report Running on every frame.");
                yield return null;
            }

            Assert.GreaterOrEqual(child.EnterCalls, 2,
                "The Repeater should have restarted its child at least once across the frames.");
        }

        // Local double — the play-mode assembly does not reference the edit-mode test assembly, so the
        // ScriptedNode equivalent is redefined here.
        private sealed class RecordingPlayNode : BehaviorTreeNode
        {
            private readonly Queue<ExecutionStatus> scriptedResults = new();

            public ExecutionStatus DefaultResult { get; set; } = ExecutionStatus.Success;
            public int EnterCalls { get; private set; }
            public int ExitCalls { get; private set; }

            public override string NodeName => "Recording Play Node";

            public RecordingPlayNode Returns(params ExecutionStatus[] results)
            {
                foreach (var result in results)
                {
                    scriptedResults.Enqueue(result);
                }

                return this;
            }

            public override void OnEnter() => EnterCalls++;
            public override void OnExit() => ExitCalls++;

            public override ExecutionStatus OnUpdate() =>
                scriptedResults.Count > 0 ? scriptedResults.Dequeue() : DefaultResult;
        }
    }
}
