using System.Collections;
using ArcaneOnyx.GraphCore;
using NUnit.Framework;
using Unity.VisualScripting;
using UnityEngine;
using UnityEngine.TestTools;

namespace ArcaneOnyx.BehaviorTree.Tests.PlayMode
{
    /// <summary>
    /// Version bumping as it happens through a <em>node inside a running tree</em>, rather than through a
    /// direct call to <see cref="AgentVariableWriter"/>.
    ///
    /// <para>
    /// The edit-mode suite covers the writer's own rule — a write of an equal value is dropped and does not
    /// move the version — by calling <c>Write</c> and <c>SetAgentVariable</c> on a bare GameObject. What it
    /// does not cover is that a tree node reaches that rule at all. The node path is longer and has its own
    /// chances to go wrong: <c>SetVariable.OnUpdate</c> pulls two ports, hands them to
    /// <c>GameplayNode.SaveVariable</c>, which switches on <see cref="VariableKind"/> and, for
    /// <see cref="VariableKind.Object"/> only, routes to <c>AgentVariableWriter.On(gameObject)</c> — a
    /// get-or-add against a GameObject the node reaches through its machine.
    /// </para>
    ///
    /// <para>
    /// The equal-value half is not a micro-optimisation. It is the load-bearing claim under
    /// <see cref="GuardTriggerKind.OnKeyChanged"/>: a fact republished every tick must cost its watchers
    /// nothing, or the cheapest trigger silently becomes the most expensive one and every guard in the scene
    /// runs its condition graph every frame. Nothing measured that through a real node until now.
    /// </para>
    /// </summary>
    public class VariableWriteIntegrationTests : PlayModeAgentFixture
    {
        /// <summary>
        /// A node rewriting the same value every tick bumps the version exactly once — on the write that
        /// actually changed it.
        /// </summary>
        [UnityTest]
        public IEnumerator ANodeRewritingTheSameValueBumpsTheVersionOnlyOnce()
        {
            var tree = NewTree();
            var graph = tree.graph;

            // Entry -> Repeater -> Set Variable. The Repeater restarts its child as soon as the child
            // completes, and Set Variable completes on the tick it runs — so the write happens every frame,
            // which is the case under test.
            var repeater = Add<Repeater>(graph, 0.0f, 100.0f);
            var write = WriteNode(graph, "hasTarget", true, 0.0f, 250.0f);

            Connect(graph, graph.EntryNode, repeater);
            Connect(graph, repeater, write);

            var machine = Spawn(tree, (_, variables) => variables.declarations.Set("hasTarget", false));

            for (int frame = 0; frame < 10; frame++)
            {
                yield return null;
            }

            var writer = AgentVariableWriter.On(machine.gameObject);

            Assert.AreEqual(true, Variables.Object(machine.gameObject).Get("hasTarget"),
                "The node wrote the value, so the fact itself is published.");
            Assert.AreEqual(1, writer.VersionOf("hasTarget"),
                "false -> true is one change. Ten further writes of true are the same value, so they are "
                + "dropped and the version must not move -- otherwise every guard watching this key would "
                + "recompute every frame, which is the cost OnKeyChanged exists to avoid.");
        }

        /// <summary>
        /// Alternating values keep bumping. The counter has to mean "changed", not "was written" — and this
        /// is the half that proves it is still counting at all, rather than the previous test passing because
        /// bumping is broken outright.
        /// </summary>
        [UnityTest]
        public IEnumerator NodesWritingDifferentValuesKeepBumpingTheVersion()
        {
            var tree = NewTree();
            var graph = tree.graph;

            var repeater = Add<Repeater>(graph, 0.0f, 100.0f);
            var sequence = Add<Sequence>(graph, 0.0f, 250.0f);
            var writeTrue = WriteNode(graph, "hasTarget", true, -200.0f, 400.0f);
            var writeFalse = WriteNode(graph, "hasTarget", false, 200.0f, 400.0f);

            Connect(graph, graph.EntryNode, repeater);
            Connect(graph, repeater, sequence);
            Connect(graph, sequence, writeTrue);
            Connect(graph, sequence, writeFalse);

            var machine = Spawn(tree, (_, variables) => variables.declarations.Set("hasTarget", false));

            for (int frame = 0; frame < 5; frame++)
            {
                yield return null;
            }

            int version = AgentVariableWriter.On(machine.gameObject).VersionOf("hasTarget");

            Assert.Greater(version, 2,
                "Each pass writes true then false, and both are changes, so the version climbs with the "
                + $"passes rather than settling. Saw {version}.");
        }

        /// <summary>
        /// The two halves joined: a guard scheduled on a key stays asleep while a node rewrites that key with
        /// the same value, and wakes as soon as the value actually changes.
        ///
        /// <para>
        /// The guard reads <c>canAttack</c> but is scheduled on <c>hasTarget</c>. Contrived on purpose — a
        /// trigger says <em>when the guard may recompute</em> and the port says <em>what it would answer</em>,
        /// and they are deliberately separate. Pointing them at different keys is what isolates the schedule:
        /// the condition's answer never moves, so any change in the evaluation count is caused by scheduling
        /// alone.
        /// </para>
        ///
        /// <para>
        /// The measurement relies on <see cref="Selector"/> resuming at its running child rather than
        /// re-checking the siblings above it: once the Selector settles on the writing branch, the guarded
        /// branch above is reached only through the preemption scan, which asks with <c>fresh: false</c> —
        /// the one path triggers actually gate.
        /// </para>
        /// </summary>
        [UnityTest]
        public IEnumerator AGuardScheduledOnAKeyStaysAsleepWhileANodeRewritesItUnchanged()
        {
            var tree = NewTree();
            var graph = tree.graph;

            var outer = Add<Repeater>(graph, 0.0f, 100.0f);
            var selector = Add<Selector>(graph, 0.0f, 250.0f);

            // [0] a branch that never runs, because its guard answers false -- but which is polled for
            // preemption on every tick, which is what we are counting.
            var guarded = Add<WaitTime>(graph, -400.0f, 400.0f);
            FeedFloat(graph, guarded, guarded.Time, 999.0f);

            // [1] the churn: an inner Repeater keeps Set Variable running forever, so the Selector never
            // completes and never re-enters the branch above.
            var inner = Add<Repeater>(graph, 400.0f, 400.0f);
            var write = WriteNode(graph, "hasTarget", true, 400.0f, 550.0f);

            Connect(graph, graph.EntryNode, outer);
            Connect(graph, outer, selector);
            Connect(graph, selector, guarded);
            Connect(graph, selector, inner);
            Connect(graph, inner, write);

            var read = ReadAgentVariable(graph, "canAttack", -700.0f, 250.0f);

            var guard = Add<BooleanReactiveGuard>(graph, -400.0f, 250.0f);
            guard.UpdateOwner(guarded);
            read.Value.ValidlyConnectTo(guard.Value);
            guard.AddTrigger(GuardTrigger.KeyChanged("hasTarget"));

            var machine = Spawn(tree, (_, variables) =>
            {
                variables.declarations.Set("hasTarget", false);
                variables.declarations.Set("canAttack", false);
            });

            for (int frame = 0; frame < 5; frame++)
            {
                yield return null;
            }

            var runningGuard = RunningNode<ReactiveGuard>(machine);
            var writer = AgentVariableWriter.On(machine.gameObject);

            Assert.AreEqual(1, writer.VersionOf("hasTarget"),
                "The node rewrites the same value, so the version settles after the first real change.");

            int evaluationsWhileQuiet = runningGuard.Evaluations;

            for (int frame = 0; frame < 10; frame++)
            {
                yield return null;
            }

            Assert.AreEqual(evaluationsWhileQuiet, runningGuard.Evaluations,
                "Ten frames of the node republishing an unchanged fact must not wake the guard once. If this "
                + "grows, OnKeyChanged is costing a full condition evaluation per frame -- the exact expense "
                + "the version counter exists to prevent.");

            // Now the fact genuinely moves. The node writes true every tick, so writing false guarantees a
            // real change on this frame and another on the next.
            AgentVariableWriter.SetOn(machine.gameObject, "hasTarget", false);

            for (int frame = 0; frame < 3; frame++)
            {
                yield return null;
            }

            Assert.Greater(runningGuard.Evaluations, evaluationsWhileQuiet,
                "and a real change must wake it, or the guard is not asleep but deaf.");
            Assert.Greater(writer.VersionOf("hasTarget"), 1, "the version moved with it.");
        }

        /// <summary>
        /// A <c>Set Variable</c> node writing an agent-scope fact, with both ports fed by literals.
        /// <para>
        /// Both ports have to be connected rather than defaulted: <c>Key</c> is declared bare, and
        /// <c>Value</c> is typed <c>object</c>, which fails <c>SupportsDefaultValue</c> outright — so an
        /// inline value on it is a silent no-op.
        /// </para>
        /// </summary>
        private static SetVariable WriteNode(
            BehaviorTreeGraph graph, string key, bool value, float x, float y)
        {
            var write = Add<SetVariable>(graph, x, y);
            SetPrivateField(write, "VariableKind", VariableKind.Object);

            FeedString(graph, write, write.Key, key);
            FeedBool(graph, write, write.Value, value);

            return write;
        }
    }
}
