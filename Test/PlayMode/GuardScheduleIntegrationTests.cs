using System.Collections;
using ArcaneOnyx.GraphCore;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace ArcaneOnyx.BehaviorTree.Tests.PlayMode
{
    /// <summary>
    /// <see cref="GuardTriggerKind.EveryInterval"/> against real game time.
    ///
    /// <para>
    /// <b>This kind cannot be tested in edit mode at all</b>, and not by oversight:
    /// <c>ReactiveGuard.Now</c> is <c>Application.isPlaying ? Time.time : 0f</c>, so once a guard has
    /// evaluated, <c>secondsSinceEvaluated</c> is permanently <c>0 - 0</c> and no positive interval is ever
    /// due. Every interval in the edit-mode suite is therefore a stand-in for "a trigger that will never
    /// fire" — <c>Interval(5f)</c> and <c>Interval(600f)</c> are interchangeable there, and those tests would
    /// still pass if <c>IsDue</c> for this kind simply returned false. They pin the caching economy, which is
    /// worth pinning, but the interval elapsing is unasserted anywhere.
    /// </para>
    ///
    /// <para>
    /// <c>Deviation</c> and the <c>Reroll</c> that applies it have no coverage in any mode. It exists so that
    /// two hundred agents sharing a 0.2s timer do not land on one frame, and it multiplies its interval by a
    /// random factor — a arithmetic slip there degrades silently into either "never fires" or "every frame",
    /// both of which look like working software until a scene is full.
    /// </para>
    ///
    /// <para>
    /// Time is pinned with <see cref="Time.captureDeltaTime"/> so a frame advances the clock by an exact
    /// amount. Without it these would be frame-rate roulette on a loaded machine, which is how a suite earns
    /// a reputation for flaking and stops being trusted.
    /// </para>
    /// </summary>
    public class GuardScheduleIntegrationTests : PlayModeAgentFixture
    {
        private const float FrameSeconds = 0.05f;

        [TearDown]
        public void ReleaseTheClock()
        {
            // Leaks into every later test in the run if it is not put back.
            Time.captureDeltaTime = 0.0f;
        }

        /// <summary>
        /// An interval trigger fires when the interval elapses — repeatedly, and far less often than every
        /// frame. The first assertion in this project that an interval is ever <em>due</em>.
        /// </summary>
        [UnityTest]
        public IEnumerator AnIntervalTriggerRecomputesWhenTheIntervalElapses()
        {
            var machine = SpawnPolledGuard(GuardTrigger.Interval(0.2f));

            Time.captureDeltaTime = FrameSeconds;

            var guard = RunningNode<ReactiveGuard>(machine);

            // Settle, then measure from a known point so entry evaluations are not counted as scheduled ones.
            for (int frame = 0; frame < 4; frame++)
            {
                yield return null;
            }

            int start = guard.Evaluations;

            // 20 frames x 0.05s = 1.0s of game time, against a 0.2s interval: about five recomputes.
            for (int frame = 0; frame < 20; frame++)
            {
                yield return null;
            }

            int scheduled = guard.Evaluations - start;

            Assert.Greater(scheduled, 1,
                "One second of game time against a 0.2s interval has to recompute several times. If this is "
                + "zero the trigger never fires at all -- which is exactly what an edit-mode test cannot "
                + "distinguish from correct behaviour, because there the clock never moves.");

            Assert.Less(scheduled, 20,
                "and it must not have recomputed on every frame, or the interval is not gating anything and "
                + "the guard is quietly paying the every-frame cost.");

            Assert.That(scheduled, Is.InRange(3, 8),
                $"1.0s / 0.2s is about five recomputes; saw {scheduled}. A number far outside that band means "
                + "the interval is being measured against something other than elapsed game time.");
        }

        /// <summary>
        /// A deviated interval still fires at a sane rate. <c>Reroll</c> has no coverage anywhere, and its
        /// failure modes are both silent: a factor that collapses toward zero turns the cheapest trigger into
        /// an every-frame one, and one that inflates turns a reactive guard into a guard that never reacts.
        /// </summary>
        [UnityTest]
        public IEnumerator ADeviatedIntervalStillFiresWithinItsBand()
        {
            var machine = SpawnPolledGuard(GuardTrigger.Interval(0.2f, 0.1f));

            Time.captureDeltaTime = FrameSeconds;

            var guard = RunningNode<ReactiveGuard>(machine);

            for (int frame = 0; frame < 4; frame++)
            {
                yield return null;
            }

            int start = guard.Evaluations;

            for (int frame = 0; frame < 20; frame++)
            {
                yield return null;
            }

            int scheduled = guard.Evaluations - start;

            // Each interval is rerolled into 0.2s +/- 0.1s, so 1.0s of game time admits roughly 3 to 10
            // recomputes depending on the draws. The band is what matters, not the exact count.
            Assert.That(scheduled, Is.InRange(2, 14),
                $"A 0.2s +/- 0.1s interval over 1.0s should recompute a handful of times; saw {scheduled}. "
                + "Near zero means Reroll is inflating the interval, near twenty means it is collapsing it "
                + "toward every frame -- the two failures the deviation is supposed to be incapable of.");
        }

        /// <summary>
        /// A guard with no triggers evaluates every tick. Documented as the behaviour a guard had before
        /// triggers existed, and covered in edit mode; repeated here because it is the baseline the two tests
        /// above are measured against. If this one did not report roughly one evaluation per frame, a low
        /// count elsewhere would prove nothing about scheduling.
        /// </summary>
        [UnityTest]
        public IEnumerator AGuardWithNoTriggersRecomputesEveryTick()
        {
            var machine = SpawnPolledGuard(trigger: null);

            Time.captureDeltaTime = FrameSeconds;

            var guard = RunningNode<ReactiveGuard>(machine);

            for (int frame = 0; frame < 4; frame++)
            {
                yield return null;
            }

            int start = guard.Evaluations;

            for (int frame = 0; frame < 10; frame++)
            {
                yield return null;
            }

            Assert.That(guard.Evaluations - start, Is.InRange(8, 12),
                "An empty trigger list means always due, so this is the per-frame cost the other schedules "
                + "are being compared against.");
        }

        /// <summary>
        /// Entry -> Repeater -> Selector -> [ guarded branch, a branch that runs forever ].
        ///
        /// <para>
        /// The guard answers false, so its branch is never entered and the Selector settles on the branch
        /// below. That is the arrangement being measured: a Selector resumes at its running child rather than
        /// re-checking the siblings above it, so the guarded branch is reached only through the preemption
        /// scan — which asks with <c>fresh: false</c>, the one path a trigger actually gates. Were the branch
        /// entered and re-entered instead, entry would recompute every time regardless of schedule and the
        /// count would measure nothing.
        /// </para>
        /// </summary>
        private BehaviorTreeMachine SpawnPolledGuard(GuardTrigger trigger)
        {
            var tree = NewTree();
            var graph = tree.graph;

            var repeater = Add<Repeater>(graph, 0.0f, 100.0f);
            var selector = Add<Selector>(graph, 0.0f, 250.0f);

            var guarded = Add<WaitTime>(graph, -400.0f, 400.0f);
            FeedFloat(graph, guarded, guarded.Time, 999.0f);

            var fallback = Add<WaitTime>(graph, 400.0f, 400.0f);
            FeedFloat(graph, fallback, fallback.Time, 999.0f);

            Connect(graph, graph.EntryNode, repeater);
            Connect(graph, repeater, selector);
            Connect(graph, selector, guarded);
            Connect(graph, selector, fallback);

            var read = ReadAgentVariable(graph, "canAttack", -700.0f, 250.0f);

            var guard = Add<BooleanReactiveGuard>(graph, -400.0f, 250.0f);
            guard.UpdateOwner(guarded);
            read.Value.ValidlyConnectTo(guard.Value);

            if (trigger != null) guard.AddTrigger(trigger);

            return Spawn(tree, (_, variables) => variables.declarations.Set("canAttack", false));
        }
    }
}
