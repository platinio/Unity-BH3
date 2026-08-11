using System.Collections;
using System.Linq;
using System.Reflection;
using ArcaneOnyx.BehaviorTree.Debugging;
using ArcaneOnyx.GraphCore;
using NUnit.Framework;
using Unity.VisualScripting;
using UnityEngine;
using UnityEngine.TestTools;

namespace ArcaneOnyx.BehaviorTree.Tests.PlayMode
{
    /// <summary>
    /// The recorder end to end, on a real <see cref="BehaviorTreeMachine"/> ticked by the player loop.
    ///
    /// <para>
    /// The edit-mode suite drives nodes directly, which covers what the recorder writes but not that anything
    /// ever hands it to them. Everything load-bearing about the wiring lives outside the nodes: the machine
    /// builds the recorder in <c>Awake</c>, binds it across the graph, opens a tick per <c>Update</c>, and
    /// registers the root variable scope. None of that runs in edit mode, so this is the only place it is
    /// actually checked.
    /// </para>
    ///
    /// <para>
    /// The scenario is the one the whole feature exists for: a high-priority branch gated on a fact, a
    /// low-priority branch gated on its absence, and a sensor flipping the fact mid-run. Reading the
    /// resulting buffer should explain the switch without anyone having to guess.
    /// </para>
    /// </summary>
    public class FlightRecorderEndToEndTests
    {
        private GameObject agent;

        [TearDown]
        public void TearDown()
        {
            if (agent != null) Object.DestroyImmediate(agent);

            BehaviorTreeFlightRecorders.Reset();
            BehaviorTreeFlightRecorders.GloballyEnabled = true;
        }

        [UnityTest]
        public IEnumerator ARecordingExplainsWhyTheAgentSwitchedBranches()
        {
            var tree = BuildZombieTree(out var attack, out var idle, out var attackGuard, out var idleGuard);

            var machine = SpawnAgent(tree, hasTarget: false);

            // Awake has run: the machine should have built a recorder and handed it to the graph.
            Assert.IsNotNull(machine.FlightRecorder, "The machine builds the recorder in Awake.");
            Assert.Contains(machine.FlightRecorder, BehaviorTreeFlightRecorders.Active.ToArray(),
                "and registers it, which is what the multi-agent view lists.");

            // Three frames with no target: Idle should be the branch that holds.
            for (int frame = 0; frame < 3; frame++)
            {
                yield return null;
            }

            var recorder = machine.FlightRecorder;

            Assert.Greater(recorder.Tick, 0, "The machine opens a tick per Update.");
            Assert.IsTrue(Entered(recorder, idle), "With no target, Idle is the branch that runs.");
            Assert.IsFalse(Entered(recorder, attack), "and Attack never starts.");

            Assert.IsTrue(recorder.Events.Any(e =>
                    e.Kind == BehaviorTreeEventKind.NodeSkipped && e.NodeGuid == attack && e.RelatedGuid == attackGuard),
                "Attack was declined entry, and the recording names the guard that declined it.");

            int tickBeforeSensor = recorder.Tick;

            // A sensor spots something. Facts are produced outside the tree, so it publishes the value and
            // tells the recorder who wrote it — the tree only ever reads.
            machine.Variables.declarations.Set("hasTarget", true);
            recorder.ExternalVariableWrite("TargetingSensor", "hasTarget", false, true);

            for (int frame = 0; frame < 3; frame++)
            {
                yield return null;
            }

            Debug.Log(BehaviorTreeRecordingDump.ToJson(recorder));
            Debug.Log($"legend: Attack={attack}  Idle={idle}  AttackGuard={attackGuard}  IdleGuard={idleGuard}");

            // The causal chain, in order.
            var write = recorder.Events.Single(e => e.Kind == BehaviorTreeEventKind.VariableWrite);
            Assert.AreEqual("TargetingSensor", write.Writer);

            // The branch that takes over is the one whose guard flipped -- not the one that yielded. Idle is
            // evicted before it is ever asked again, which is the whole point: it no longer has to know that
            // Attack exists, and in a migrated tree it would carry no guard at all.
            var attackGuardFlip = recorder.Events.Last(e =>
                e.Kind == BehaviorTreeEventKind.GuardEval && e.NodeGuid == attackGuard);
            Assert.IsTrue(attackGuardFlip.Flag, "Attack's guard is hasTarget, so it goes true.");

            var preemption = recorder.Events.Single(e => e.Kind == BehaviorTreeEventKind.NodePreempted);
            Assert.AreEqual(idle, preemption.NodeGuid, "Idle is what lost the slot,");
            Assert.AreEqual(attackGuard, preemption.RelatedGuid,
                "and the recording names the guard that bid for it -- which belongs to Attack, not to Idle. "
                + "That is the inversion the feature exists for: the branch that wants to take over carries "
                + "the condition, rather than every branch below it carrying the negation.");
            Assert.IsNotEmpty(preemption.Key ?? string.Empty, "and names the preemptor, so an explanation can say who took over.");

            Assert.IsEmpty(recorder.Events.Where(e => e.Kind == BehaviorTreeEventKind.NodeAborted).ToArray(),
                "and nothing self-aborted: Idle was taken over, which reads differently to whoever is asking why.");

            Assert.Greater(preemption.Tick, tickBeforeSensor, "The takeover happened after the sensor wrote.");
            Assert.LessOrEqual(attackGuardFlip.Tick, preemption.Tick, "and the guard flipping is what preceded it.");

            Assert.IsTrue(Entered(recorder, attack),
                "Attack takes over once its precondition holds — which is the behaviour a designer would "
                + "otherwise have to infer from watching a capsule move.");
        }

        [UnityTest]
        public IEnumerator TheGlobalSwitchStopsARealAgentRecording()
        {
            BehaviorTreeFlightRecorders.GloballyEnabled = false;

            var tree = BuildZombieTree(out _, out _, out _, out _);
            var machine = SpawnAgent(tree, hasTarget: false);

            for (int frame = 0; frame < 3; frame++)
            {
                yield return null;
            }

            Assert.AreEqual(0, machine.FlightRecorder.Events.Count,
                "A tree still runs normally with recording off; it just stops being watched.");
            Assert.AreEqual(ExecutionStatus.Running, machine.LastExecutionStatus,
                "and the recorder must not have changed what the tree decided.");
        }

        #region Scenario

        /// <summary>
        /// Entry -> Repeater -> Selector -> [ Attack, Idle ], with Attack guarded on <c>hasTarget</c> and Idle
        /// on its negation.
        /// <para>
        /// The Repeater is what makes the switch observable: a Selector whose running child fails runs out of
        /// children and reports Failure, so something has to restart it. Attack sits left of Idle because
        /// child order is canvas X, which is what makes it the higher priority.
        /// </para>
        /// </summary>
        private static BehaviorTreeGraphAsset BuildZombieTree(
            out System.Guid attack, out System.Guid idle, out System.Guid attackGuard, out System.Guid idleGuard)
        {
            var asset = ScriptableObject.CreateInstance<BehaviorTreeGraphAsset>();
            var graph = asset.graph;

            var repeater = Add<Repeater>(graph, 0.0f, 100.0f);
            var selector = Add<Selector>(graph, 0.0f, 250.0f);

            var attackNode = Add<WaitTime>(graph, -900.0f, 400.0f);
            var idleNode = Add<WaitTime>(graph, 900.0f, 400.0f);

            Connect(graph, graph.EntryNode, repeater);
            Connect(graph, repeater, selector);
            Connect(graph, selector, attackNode);
            Connect(graph, selector, idleNode);

            // WaitTime.Time declares no default, so it has to be connected or it throws on the first tick.
            // Long enough that neither branch ever finishes on its own — the only thing that ends one here
            // is a guard.
            FeedFloat(graph, attackNode, attackNode.Time, 999.0f);
            FeedFloat(graph, idleNode, idleNode.Time, 999.0f);

            // One read of hasTarget, feeding both guards. Attack runs while it holds; Idle while it does not.
            var key = Add<StringLiteral>(graph, -600.0f, 0.0f);
            SetPrivateField(key, "value", "hasTarget");

            var read = Add<GetVariable>(graph, -400.0f, 0.0f);
            SetPrivateField(read, "VariableKind", VariableKind.Object);
            key.Value.ValidlyConnectTo(read.Key);

            var attackConditional = Add<BooleanReactiveGuard>(graph, -900.0f, 250.0f);
            attackConditional.UpdateOwner(attackNode);
            read.Value.ValidlyConnectTo(attackConditional.Value);

            var not = Add<Not>(graph, 600.0f, 0.0f);
            read.Value.ValidlyConnectTo(not.Value);

            var idleConditional = Add<BooleanReactiveGuard>(graph, 900.0f, 250.0f);
            idleConditional.UpdateOwner(idleNode);
            not.Result.ValidlyConnectTo(idleConditional.Value);

            attack = attackNode.guid;
            idle = idleNode.guid;
            attackGuard = attackConditional.guid;
            idleGuard = idleConditional.guid;

            return asset;
        }

        /// <summary>
        /// Stands the agent up the way a prefab would: the machine reads its macro in <c>Awake</c>, so the
        /// object is built inactive and switched on once everything it needs is in place.
        /// </summary>
        private BehaviorTreeMachine SpawnAgent(BehaviorTreeGraphAsset tree, bool hasTarget)
        {
            agent = new GameObject("Zombie");
            agent.SetActive(false);

            var machine = agent.AddComponent<BehaviorTreeMachine>();
            var variables = agent.GetComponent<Variables>();
            variables.declarations.Set("hasTarget", hasTarget);

            machine.nest.macro = tree;
            agent.SetActive(true);

            return machine;
        }

        #endregion

        #region Helpers

        private static bool Entered(BehaviorTreeFlightRecorder recorder, System.Guid node)
        {
            return recorder.Events.Any(e => e.Kind == BehaviorTreeEventKind.NodeEnter && e.NodeGuid == node);
        }

        private static T Add<T>(BehaviorTreeGraph graph, float x, float y) where T : BehaviorTreeNode, new()
        {
            var node = new T();
            node.Position = new Rect(new Vector2(x, y), node.StartingSize);
            graph.Nodes.Add(node);

            return node;
        }

        private static void Connect(BehaviorTreeGraph graph, BehaviorTreeNode parent, BehaviorTreeNode child)
        {
            var transition = new BehaviorTreeTransition();
            transition.SetupTransition(parent, child, graph.CountTransitionsFromNode(parent));
            graph.Transitions.Add(transition);
        }

        private static void FeedFloat(BehaviorTreeGraph graph, BehaviorTreeNode owner, ValueInput port, float value)
        {
            var literal = Add<FloatLiteral>(graph, owner.Position.x, owner.Position.y + 150.0f);
            SetPrivateField(literal, "value", value);
            literal.Value.ValidlyConnectTo(port);
        }

        /// <summary>
        /// Literal values and a Get Variable's kind are private serialized fields with no setter — the
        /// inspector writes them through SerializedProperty, and authoring code through reflection. Same
        /// thing the editor-side authoring helpers do.
        /// </summary>
        private static void SetPrivateField(object target, string field, object value)
        {
            var info = target.GetType().GetField(field, BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.IsNotNull(info, $"{target.GetType().Name} has no field '{field}'.");

            info.SetValue(target, value);
        }

        #endregion
    }
}
