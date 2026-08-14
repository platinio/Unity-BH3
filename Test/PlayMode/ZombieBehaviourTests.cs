using System.Collections;
using ArcaneOnyx.GraphCore;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace ArcaneOnyx.BehaviorTree.Tests.PlayMode
{
    /// <summary>
    /// A whole agent, driven through its whole life: idle, spot, chase, swing, lose interest.
    ///
    /// <para>
    /// Everything else in this assembly tests one seam. This tests that an AI a designer would actually build
    /// produces the behaviour they would actually expect — which is a different question, and the one that
    /// usually goes unanswered until someone presses play and watches a capsule. A suite of green unit tests
    /// is entirely compatible with an agent that stands still.
    /// </para>
    ///
    /// <para>
    /// The tree is the canonical BH3 shape and the arrangement the preemption feature exists for:
    /// </para>
    /// <code>
    /// Selector
    ///   [0] Attack   guard: targetInRange   takes over: yes   stops own branch: NO  (committed swing)
    ///   [1] Chase    guard: hasTarget       takes over: yes   stops own branch: yes
    ///   [2] Idle     no guard at all
    /// </code>
    /// <para>
    /// <b>Idle carrying no guard is the whole point.</b> Before preemption, a fallback had to spell out the
    /// negation of everything above it — <c>Not(hasTarget) AND Not(targetInRange) AND ...</c> — and be edited
    /// again every time a branch was added. Here the branch that wants control carries the condition, so Idle
    /// knows nothing about Attack or Chase and adding a fourth branch above it changes nothing about it.
    /// </para>
    /// </summary>
    public class ZombieBehaviourTests : PlayModeAgentFixture
    {
        private const float FrameSeconds = 0.05f;

        /// <summary>How long the swing takes once it starts. Six frames at the pinned timestep.</summary>
        private const float SwingSeconds = 0.3f;

        private System.Guid attack;
        private System.Guid chase;
        private System.Guid idle;

        [TearDown]
        public void ReleaseTheClock()
        {
            Time.captureDeltaTime = 0.0f;
        }

        /// <summary>
        /// The full arc, in the order a designer would describe it. Written as one test rather than five
        /// because the interesting content is the <em>transitions</em> — each phase is only meaningful as
        /// something the previous phase led to, and splitting them would mean re-driving the agent into each
        /// state and asserting on a fixture instead of on a life.
        /// </summary>
        [UnityTest]
        public IEnumerator AZombieIdlesThenChasesThenSwingsThenLosesInterest()
        {
            var machine = SpawnZombie();

            Time.captureDeltaTime = FrameSeconds;

            var recorder = machine.FlightRecorder;

            // --- Nothing about. Idle holds, and neither of the branches above it starts. ---
            yield return Frames(3);

            Assert.IsTrue(Entered(recorder, idle), "With no target the fallback runs.");
            Assert.IsFalse(Entered(recorder, chase), "Chase has no reason to start,");
            Assert.IsFalse(Entered(recorder, attack), "and neither does Attack.");

            // --- A target appears. Chase outranks Idle and takes the slot. ---
            int spotted = Mark(recorder);
            PublishFact(machine, "hasTarget", true);

            yield return Frames(3);

            Assert.IsTrue(EnteredSince(recorder, spotted, chase), "Chase starts once there is something to chase,");
            Assert.IsTrue(TakenOverSince(recorder, spotted, idle),
                "and Idle loses the slot to it -- taken over, not self-aborted, because Idle carries no "
                + "condition of its own to stop holding.");
            Assert.IsFalse(EnteredSince(recorder, spotted, attack), "Attack still has no reason to start.");

            // --- The target comes into range. Attack outranks Chase. ---
            int inRange = Mark(recorder);
            PublishFact(machine, "targetInRange", true);

            yield return Frames(2);

            Assert.IsTrue(EnteredSince(recorder, inRange, attack), "Attack takes over the moment it is eligible,");
            Assert.IsTrue(TakenOverSince(recorder, inRange, chase), "and Chase is the branch that lost the slot.");

            // --- The target steps back out mid-swing. The swing is committed and must finish. ---
            int steppedOut = Mark(recorder);
            PublishFact(machine, "targetInRange", false);

            yield return Frames(2);

            Assert.IsFalse(AbortedSince(recorder, steppedOut, attack),
                "Attack's guard has Stops Its Own Branch off, so a precondition that stops holding must not "
                + "kill it. This is the committed swing: it bids for control the moment the target is in "
                + "range, and once the animation is under way it finishes regardless. That combination -- "
                + "preempts, does not self-abort -- is unreachable with any single flag, which is why the two "
                + "capabilities are separate.");
            Assert.IsFalse(TakenOverSince(recorder, steppedOut, attack),
                "and nothing below it may take the slot back while it is still swinging.");

            // --- The swing finishes on its own. Chase is still eligible, so it resumes. ---
            int swingEnd = Mark(recorder);

            yield return Frames(8);

            Assert.IsTrue(EnteredSince(recorder, swingEnd, chase),
                "Once the swing completes the zombie falls back to the highest-priority branch still eligible, "
                + "which is Chase -- hasTarget is still true.");

            // --- The target is lost. Chase does abort, because its guard does stop its own branch. ---
            int lost = Mark(recorder);
            PublishFact(machine, "hasTarget", false);

            yield return Frames(3);

            Assert.IsTrue(AbortedSince(recorder, lost, chase),
                "Chase's guard has Stops Its Own Branch on, so losing the target kills it mid-run -- the "
                + "opposite of the swing, and the reason the two capabilities are configured per guard.");
            Assert.IsTrue(EnteredSince(recorder, lost, idle),
                "and the zombie drops back to Idle, which never had to know any of this happened.");
        }

        /// <summary>
        /// Priority is the transition index, not the order facts arrive. Both branches become eligible on the
        /// same frame, and the higher-priority one has to win.
        /// </summary>
        [UnityTest]
        public IEnumerator TheHighestPriorityEligibleBranchWinsWhenTwoBecomeEligibleAtOnce()
        {
            var machine = SpawnZombie();

            Time.captureDeltaTime = FrameSeconds;

            yield return Frames(3);

            var recorder = machine.FlightRecorder;
            int mark = Mark(recorder);

            // Both facts land before the tree ticks again, so Chase and Attack are eligible together.
            PublishFact(machine, "hasTarget", true);
            PublishFact(machine, "targetInRange", true);

            yield return Frames(2);

            Assert.IsTrue(EnteredSince(recorder, mark, attack),
                "Attack sits at index 0, so it wins the slot outright.");
            Assert.IsFalse(EnteredSince(recorder, mark, chase),
                "and Chase never runs at all -- the scan stops at the first eligible child rather than "
                + "letting the lower-priority branch start and be interrupted a frame later.");
        }

        #region The zombie

        /// <summary>
        /// Entry -&gt; Repeater -&gt; Selector -&gt; [ Attack, Chase, Idle ].
        /// <para>
        /// The Repeater matters: a Selector whose running child ends runs out of children and reports a
        /// result, and a machine stops ticking a tree that finished. Without it this agent would live for one
        /// swing.
        /// </para>
        /// </summary>
        private BehaviorTreeMachine SpawnZombie()
        {
            var tree = NewTree();
            var graph = tree.graph;

            var repeater = Add<Repeater>(graph, 0.0f, 100.0f);
            var selector = Add<Selector>(graph, 0.0f, 250.0f);

            // Laid out left to right so the picture agrees with the priorities.
            var attackNode = Add<WaitTime>(graph, -900.0f, 500.0f);
            var chaseNode = Add<WaitTime>(graph, 0.0f, 500.0f);
            var idleNode = Add<WaitTime>(graph, 900.0f, 500.0f);

            // The swing ends by itself; chasing and idling do not.
            FeedFloat(graph, attackNode, attackNode.Time, SwingSeconds);
            FeedFloat(graph, chaseNode, chaseNode.Time, 999.0f);
            FeedFloat(graph, idleNode, idleNode.Time, 999.0f);

            Connect(graph, graph.EntryNode, repeater);
            Connect(graph, repeater, selector);
            Connect(graph, selector, attackNode);
            Connect(graph, selector, chaseNode);
            Connect(graph, selector, idleNode);

            // Attack: bids for the slot, but will not be thrown out of its own swing.
            Guard(graph, attackNode, "targetInRange", -900.0f, 350.0f, stopsItsOwnBranch: false);

            // Chase: bids, and also dies the moment its precondition stops holding.
            Guard(graph, chaseNode, "hasTarget", 0.0f, 350.0f, stopsItsOwnBranch: true);

            // Idle: nothing. Deliberately.

            attack = attackNode.guid;
            chase = chaseNode.guid;
            idle = idleNode.guid;

            return Spawn(tree, (_, variables) =>
            {
                variables.declarations.Set("hasTarget", false);
                variables.declarations.Set("targetInRange", false);
            });
        }

        /// <summary>
        /// A reactive guard reading an agent fact and scheduled on it — the shape
        /// <c>BehaviorTreeAuthoring.GuardOnVariable</c> produces.
        /// </summary>
        private static void Guard(
            BehaviorTreeGraph graph, BehaviorTreeNode owner, string key, float x, float y, bool stopsItsOwnBranch)
        {
            var read = ReadAgentVariable(graph, key, x - 250.0f, y);

            var guard = Add<BooleanReactiveGuard>(graph, x, y);
            guard.UpdateOwner(owner);
            read.Value.ValidlyConnectTo(guard.Value);
            guard.SetCapabilities(abortsOwner: stopsItsOwnBranch, preempts: true);
            guard.AddTrigger(GuardTrigger.KeyChanged(key));
        }

        private static IEnumerator Frames(int count)
        {
            for (int frame = 0; frame < count; frame++)
            {
                yield return null;
            }
        }

        #endregion
    }
}
