using System.Collections;
using ArcaneOnyx.GraphCore;
using NUnit.Framework;
using Unity.VisualScripting;
using UnityEngine;
using UnityEngine.TestTools;

namespace ArcaneOnyx.BehaviorTree.Tests.PlayMode
{
    /// <summary>
    /// Nodes whose whole behaviour is a clock: <see cref="WaitTime"/> counting down
    /// <c>Time.deltaTime</c>, and <see cref="Cooldown"/> comparing against <c>Time.time</c>.
    ///
    /// <para>
    /// <b>Neither had its timing asserted anywhere.</b> <c>WaitTime</c> appears throughout the suite purely
    /// as scaffolding — the flight-recorder fixture feeds it <c>999f</c> specifically so it never finishes —
    /// so nothing in the project checks that a Wait ever completes. Break the subtraction in
    /// <c>OnUpdate</c> and the entire edit-mode suite stays green.
    /// </para>
    ///
    /// <para>
    /// <c>Cooldown</c> is sharper still. <c>DecoratorNodeTests</c> covers the blocking half with the comment
    /// <i>"Edit mode advances no game time between these two runs, so the cooldown is still active"</i> — the
    /// test depends on the clock being frozen. That makes the recharge observable but leaves the
    /// <em>expiry</em> — the half a designer is actually relying on — untested by construction.
    /// </para>
    ///
    /// <para>
    /// <see cref="Time.captureDeltaTime"/> pins a frame to an exact slice of game time, so these assert on
    /// elapsed time rather than on how busy the machine was.
    /// </para>
    /// </summary>
    public class NodeTimingIntegrationTests : PlayModeAgentFixture
    {
        private const float FrameSeconds = 0.05f;

        [TearDown]
        public void ReleaseTheClock()
        {
            Time.captureDeltaTime = 0.0f;
        }

        /// <summary>
        /// A Wait runs for its duration and then succeeds — and the machine stops ticking once it has, which
        /// is the <c>lastExecutionStatus</c> latch doing its job.
        /// </summary>
        [UnityTest]
        public IEnumerator AWaitRunsForItsDurationAndThenSucceeds()
        {
            var tree = NewTree();
            var graph = tree.graph;

            // Deliberately no Repeater: the root completing is the observable here.
            var wait = Add<WaitTime>(graph, 0.0f, 100.0f);
            FeedFloat(graph, wait, wait.Time, 0.25f);

            Connect(graph, graph.EntryNode, wait);

            var machine = Spawn(tree);

            // 0.10s elapsed against a 0.25s wait.
            for (int frame = 0; frame < 2; frame++)
            {
                Time.captureDeltaTime = FrameSeconds;
                yield return null;
            }

            Assert.AreEqual(ExecutionStatus.Running, machine.LastExecutionStatus,
                "Two frames is 0.10s of game time, so a 0.25s wait must still be running -- a Wait that "
                + "succeeds immediately would satisfy every other test in the project.");

            // Past 0.25s.
            for (int frame = 0; frame < 6; frame++)
            {
                yield return null;
            }

            Assert.AreEqual(ExecutionStatus.Success, machine.LastExecutionStatus,
                "and once the duration has elapsed it must finish. Nothing else in the suite asserts that a "
                + "Wait ever completes; the flight-recorder fixture feeds it 999f precisely so it does not.");
        }

        /// <summary>
        /// The machine stops ticking a tree that finished. <c>Update</c> returns early once
        /// <c>lastExecutionStatus</c> is Success or Failure, which is why a tree without a Repeater under
        /// Entry runs exactly once — the single most common way a hand-authored tree appears dead.
        /// </summary>
        [UnityTest]
        public IEnumerator AFinishedTreeIsNotTickedAgain()
        {
            var tree = NewTree();
            var graph = tree.graph;

            var wait = Add<WaitTime>(graph, 0.0f, 100.0f);
            FeedFloat(graph, wait, wait.Time, 0.05f);

            Connect(graph, graph.EntryNode, wait);

            var machine = Spawn(tree);

            Time.captureDeltaTime = FrameSeconds;

            for (int frame = 0; frame < 5; frame++)
            {
                yield return null;
            }

            Assert.AreEqual(ExecutionStatus.Success, machine.LastExecutionStatus);

            int entriesWhenFinished = EnterCount(machine.FlightRecorder, wait.guid);

            for (int frame = 0; frame < 5; frame++)
            {
                yield return null;
            }

            Assert.AreEqual(entriesWhenFinished, EnterCount(machine.FlightRecorder, wait.guid),
                "A completed tree is latched off. If the node is re-entered, the machine is restarting a "
                + "finished tree and every 'runs once then goes quiet' diagnosis in the docs is wrong.");
        }

        /// <summary>
        /// A Cooldown blocks its child while recharging and <em>lets it through again once the duration
        /// elapses</em>. The second half is what edit mode cannot see.
        ///
        /// <para>
        /// The observable is a variable the child writes, not the recorder's entry count. That is deliberate:
        /// <c>Cooldown.OnUpdate</c> ticks its child by calling <c>OnUpdateInternal</c> directly, and
        /// <c>OnUpdateInternal</c> does not enter a node that is not running. So "the child ran" and "the
        /// child was entered" are separate facts here, and only the first one is what a cooldown expiring
        /// means. Counting entries would measure the bookkeeping instead of the behaviour.
        /// </para>
        /// </summary>
        [UnityTest]
        public IEnumerator ACooldownBlocksItsChildThenLetsItThroughAgain()
        {
            var machine = SpawnCooldownTree(0.3f);

            Time.captureDeltaTime = FrameSeconds;

            yield return null;

            var writer = AgentVariableWriter.On(machine.gameObject);

            Assert.AreEqual(true, Variables.Object(machine.gameObject).Get("marker"),
                "A cooldown that has never fired lets the child through immediately.");

            // Put the marker back so any later run is visible as a fresh change.
            AgentVariableWriter.SetOn(machine.gameObject, "marker", false);
            int afterReset = writer.VersionOf("marker");

            // 0.10s elapsed, well inside the 0.3s recharge.
            for (int frame = 0; frame < 2; frame++)
            {
                yield return null;
            }

            Assert.AreEqual(afterReset, writer.VersionOf("marker"),
                "While recharging the child must not run at all -- the decorator reports Failure without "
                + "ticking it.");

            // Past the recharge.
            for (int frame = 0; frame < 10; frame++)
            {
                yield return null;
            }

            Assert.Greater(writer.VersionOf("marker"), afterReset,
                "Once the duration has elapsed the child has to run again. If this never moves, the cooldown "
                + "latched permanently -- which no edit-mode test can distinguish from correct behaviour, "
                + "because DecoratorNodeTests relies on the clock being frozen to observe the blocking half.");
        }

        /// <summary>
        /// A Cooldown enters its child on every pass, not only the first.
        ///
        /// <para>
        /// <c>Cooldown.OnUpdate</c> reaches its child through <c>OnUpdateInternal</c>, which runs
        /// <c>OnUpdate</c> and nothing else — it does not enter a node that is not running. The only place the
        /// child is ever entered is <c>Cooldown.OnEnter</c>, and that returns early while recharging. Since
        /// the Repeater above exits and re-enters the decorator every frame, every entry after the first
        /// lands during the recharge and is refused, so by the time the cooldown expires there is no path
        /// left that enters the child at all.
        /// </para>
        ///
        /// <para>
        /// The consequence is not bookkeeping. <c>OnEnter</c> is where a node establishes its per-run state:
        /// a <see cref="WaitTime"/> child sets its timer there, so it would wait the full duration on the
        /// first pass and then complete instantly on every pass afterwards; an animation node re-triggers its
        /// animator there; a navigation node re-issues its destination. A child that is updated but never
        /// entered runs with whatever state it finished the previous pass holding.
        /// </para>
        /// </summary>
        [UnityTest]
        public IEnumerator ACooldownEntersItsChildOnEveryPass()
        {
            var machine = SpawnCooldownTree(0.2f);

            Time.captureDeltaTime = FrameSeconds;

            // 1.0s of game time against a 0.2s recharge: several passes.
            for (int frame = 0; frame < 20; frame++)
            {
                yield return null;
            }

            var child = RunningNode<SetVariable>(machine);

            Assert.Greater(EnterCount(machine.FlightRecorder, child.guid), 1,
                "The child must be entered on each pass. Entered exactly once across a run spanning several "
                + "expiries means it is being updated without OnEnter ever running again -- so a Wait child "
                + "never resets its timer and an animation child never re-triggers.");
        }

        /// <summary>
        /// Entry -> Repeater -> Cooldown -> Set Variable, with the marker starting false.
        /// </summary>
        private BehaviorTreeMachine SpawnCooldownTree(float duration)
        {
            var tree = NewTree();
            var graph = tree.graph;

            var repeater = Add<Repeater>(graph, 0.0f, 100.0f);
            var cooldown = Add<Cooldown>(graph, 0.0f, 250.0f);

            var child = Add<SetVariable>(graph, 0.0f, 400.0f);
            SetPrivateField(child, "VariableKind", VariableKind.Object);
            FeedString(graph, child, child.Key, "marker");
            FeedBool(graph, child, child.Value, true);

            FeedFloat(graph, cooldown, cooldown.Duration, duration);

            Connect(graph, graph.EntryNode, repeater);
            Connect(graph, repeater, cooldown);
            Connect(graph, cooldown, child);

            return Spawn(tree, (_, variables) => variables.declarations.Set("marker", false));
        }

        /// <summary>
        /// A node that keeps count of the two per-frame hooks, and finishes when told to.
        /// </summary>
        private sealed class HookCountingNode : BehaviorTreeNode
        {
            public int LateUpdates;
            public int FixedUpdates;
            public bool Finish;

            public override string NodeName => "Hook Counting Test Node";

            public override ExecutionStatus OnUpdate() =>
                Finish ? ExecutionStatus.Success : ExecutionStatus.Running;

            public override void OnLateUpdate() => LateUpdates++;

            public override void OnFixedUpdate() => FixedUpdates++;
        }

        /// <summary>
        /// The late and fixed hooks reach a node while it is running, and stop when the tree finishes.
        ///
        /// <para>
        /// Two defects, and each hid the other. <c>BehaviorTreeGraph</c> forwarded both hooks with a filter
        /// that skipped nodes whose status was <c>Running</c> and called them on every idle one, so a running
        /// node heard nothing. And <c>LateUpdate</c>/<c>FixedUpdate</c> on the machine had no halt check at
        /// all, so they kept forwarding to a tree that <c>Update</c> had already latched off.
        /// </para>
        ///
        /// <para>
        /// Nothing caught either, because no node in the repo overrides either hook — the first one written
        /// would have found it fires only while its own branch is not running, and then carries on after the
        /// tree is over. Latent is the cheapest time to fix it, and it is why this test has to bring its own
        /// node.
        /// </para>
        /// </summary>
        [UnityTest]
        public IEnumerator TheFrameHooksReachARunningNodeAndStopWhenTheTreeFinishes()
        {
            var tree = NewTree();
            var graph = tree.graph;

            var counter = Add<HookCountingNode>(graph, 0.0f, 100.0f);
            Connect(graph, graph.EntryNode, counter);

            var machine = Spawn(tree);

            // The authored node is not the one that runs -- the machine ticks a clone of the whole graph.
            var running = RunningNode<HookCountingNode>(machine);

            for (int frame = 0; frame < 3; frame++)
            {
                yield return null;
            }

            // FixedUpdate runs on the physics clock, not the frame clock, so a handful of rendered
            // frames does not guarantee one has happened. Wait for the thing being counted.
            yield return new WaitForFixedUpdate();
            yield return new WaitForFixedUpdate();

            Assert.AreEqual(ExecutionStatus.Running, running.LastExecutionStatus,
                "The node is not running, so what follows would not be measuring a running node.");

            // Counted from here, not from zero. The machine is spawned partway through a frame, so the
            // node can collect a hook or two before its first tick has given it a status at all --
            // which would satisfy "greater than zero" without the running node ever being reached.
            int lateWhileRunning = running.LateUpdates;
            int fixedWhileRunning = running.FixedUpdates;

            for (int frame = 0; frame < 3; frame++)
            {
                yield return null;
            }

            yield return new WaitForFixedUpdate();
            yield return new WaitForFixedUpdate();

            Assert.Greater(running.LateUpdates, lateWhileRunning,
                "A node that is running received no OnLateUpdate. The graph is handing the hook to the "
                + "nodes that are not running.");
            Assert.Greater(running.FixedUpdates, fixedWhileRunning,
                "A node that is running received no OnFixedUpdate, for the same reason.");

            running.Finish = true;

            // One tick to return Success, and one more so a still-forwarding LateUpdate would show up.
            yield return null;
            yield return null;

            Assert.AreEqual(ExecutionStatus.Success, machine.LastExecutionStatus,
                "The tree did not finish, so the rest of this test would prove nothing about a finished one.");

            int lateWhenFinished = running.LateUpdates;
            int fixedWhenFinished = running.FixedUpdates;

            for (int frame = 0; frame < 5; frame++)
            {
                yield return null;
            }

            Assert.AreEqual(lateWhenFinished, running.LateUpdates,
                "The machine kept handing out LateUpdate after the tree finished. Update stops at the halt "
                + "and the other two callbacks have to agree with it.");
            Assert.AreEqual(fixedWhenFinished, running.FixedUpdates,
                "The machine kept handing out FixedUpdate after the tree finished.");
        }

        /// <summary>
        /// Entry -> Repeater -> Selector -> [ guarded WaitTime, the counting node ]. The guarded branch has
        /// the higher priority and its guard is false to begin with, so the counting node is what runs;
        /// publishing the fact makes the guard pass and the Selector take the branch away from it.
        /// </summary>
        private static BehaviorTreeGraphAsset PreemptibleHookTree()
        {
            var asset = NewTree();
            var graph = asset.graph;

            var repeater = Add<Repeater>(graph, 0.0f, 100.0f);
            var selector = Add<Selector>(graph, 0.0f, 250.0f);

            var preemptor = Add<WaitTime>(graph, -900.0f, 400.0f);
            var counter = Add<HookCountingNode>(graph, 900.0f, 400.0f);

            Connect(graph, graph.EntryNode, repeater);
            Connect(graph, repeater, selector);

            // Connection order is priority order, so the guarded branch is connected first: it is the one
            // that must be able to take over, and a Selector only ever preempts in favour of an earlier child.
            Connect(graph, selector, preemptor);
            Connect(graph, selector, counter);

            FeedFloat(graph, preemptor, preemptor.Time, 999.0f);

            var read = ReadAgentVariable(graph, "hasTarget", -600.0f, 0.0f);

            var guard = Add<BooleanReactiveGuard>(graph, -900.0f, 250.0f);
            guard.UpdateOwner(preemptor);
            read.Value.ValidlyConnectTo(guard.Value);
            guard.AddTrigger(GuardTrigger.KeyChanged("hasTarget"));

            return asset;
        }

        /// <summary>
        /// A node whose branch was taken away from it stops receiving the frame hooks.
        ///
        /// <para>
        /// This is the case that separates the two signals a filter could use, and it is why the filter asks
        /// <c>IsRunning</c> rather than <c>LastExecutionStatus == Running</c>. <c>LastExecutionStatus</c> is
        /// written only by a tick, and nothing resets it on the way out — so a Selector preempting a branch
        /// calls <c>victim.OnNodeExit()</c> with no final tick and leaves the victim reading <c>Running</c>
        /// for as long as it exists. A status filter passes that test forever.
        /// </para>
        ///
        /// <para>
        /// What that buys in practice: a preempted MoveTo would go on steering its agent from
        /// <c>OnFixedUpdate</c> while the branch that owned it was over and another branch was driving. The
        /// assertions below deliberately check the stale status <em>as well as</em> the hook counts, so the
        /// test says out loud why the obvious filter is wrong rather than merely failing with it.
        /// </para>
        /// </summary>
        [UnityTest]
        public IEnumerator APreemptedNodeStopsReceivingTheFrameHooks()
        {
            var machine = Spawn(
                PreemptibleHookTree(), (_, variables) => variables.declarations.Set("hasTarget", false));

            var counter = RunningNode<HookCountingNode>(machine);

            for (int frame = 0; frame < 3; frame++)
            {
                yield return null;
            }

            yield return new WaitForFixedUpdate();
            yield return new WaitForFixedUpdate();

            Assert.IsTrue(counter.IsRunning,
                "The guarded branch was not refused, so the counting node never ran and there is nothing to "
                + "preempt.");

            var lateBefore = counter.LateUpdates;
            var fixedBefore = counter.FixedUpdates;

            Assert.Greater(lateBefore, 0, "Fixture check: it has to have been receiving hooks to stop.");

            PublishFact(machine, "hasTarget", true);

            for (int frame = 0; frame < 3; frame++)
            {
                yield return null;
            }

            yield return new WaitForFixedUpdate();
            yield return new WaitForFixedUpdate();

            Assert.IsFalse(counter.IsRunning,
                "The Selector did not take the branch over, so nothing here is about a preempted node.");

            Assert.AreEqual(ExecutionStatus.Running, counter.LastExecutionStatus,
                "The trap, asserted rather than described: the node is not running, and its last status still "
                + "says Running -- because a preemption exits it without a final tick and nothing resets it.");

            var lateAfterPreemption = counter.LateUpdates;
            var fixedAfterPreemption = counter.FixedUpdates;

            for (int frame = 0; frame < 5; frame++)
            {
                yield return null;
            }

            yield return new WaitForFixedUpdate();
            yield return new WaitForFixedUpdate();

            Assert.AreEqual(lateAfterPreemption, counter.LateUpdates,
                "A preempted node kept receiving OnLateUpdate. The filter is reading the last status, which a "
                + "preemption leaves saying Running forever.");
            Assert.AreEqual(fixedAfterPreemption, counter.FixedUpdates,
                "A preempted node kept receiving OnFixedUpdate -- the one that would keep a preempted MoveTo "
                + "steering an agent its branch no longer owns.");
        }

    }
}
