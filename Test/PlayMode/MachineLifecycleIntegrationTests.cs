using System.Collections;
using System.Linq;
using ArcaneOnyx.BehaviorTree.Debugging;
using ArcaneOnyx.GraphCore;
using NUnit.Framework;
using Unity.VisualScripting;
using UnityEngine;
using UnityEngine.TestTools;

namespace ArcaneOnyx.BehaviorTree.Tests.PlayMode
{
    /// <summary>
    /// <see cref="BehaviorTreeMachine"/>'s own lifecycle — the work that happens around the tree rather than
    /// inside it.
    ///
    /// <para>
    /// <c>Awake</c> instantiates the macro, switches the nest to the embed, checks for sub-tree recursion,
    /// hands every node the machine, attaches and binds a recorder, builds the root variable scope, and only
    /// then calls <c>OnAwake</c>. <c>OnDestroy</c> detaches the recorder, tears the graph down and destroys
    /// the instance. None of it runs in edit mode and, before these, exactly one test touched any of it —
    /// so a change here would pass the entire edit-mode suite and break every agent in the game
    /// simultaneously.
    /// </para>
    ///
    /// <para>
    /// The instantiate is the load-bearing part and is easy to mistake for an optimisation. It is what makes
    /// one authored asset safe to put on a hundred agents: each machine runs a private clone, so per-node
    /// runtime state cannot be shared. It is also the serialize/deserialize round trip, which is why a port
    /// value that only exists as an inline default on a bare port is gone by the time the tree ticks.
    /// </para>
    /// </summary>
    public class MachineLifecycleIntegrationTests : PlayModeAgentFixture
    {
        private GameObject secondAgent;

        [TearDown]
        public void DestroySecondAgent()
        {
            if (secondAgent != null) Object.DestroyImmediate(secondAgent);

            secondAgent = null;
        }

        /// <summary>
        /// The machine runs a clone and leaves the authored asset alone. Everything else here depends on it:
        /// were the machine to run the asset itself, two agents sharing a tree would share every node's
        /// runtime state, and the first one to tick would corrupt the second.
        /// </summary>
        [UnityTest]
        public IEnumerator TheMachineRunsACloneAndLeavesTheAuthoredAssetAlone()
        {
            var tree = BuildGuardedTree(out _, out _);
            var machine = Spawn(tree, (_, variables) => variables.declarations.Set("hasTarget", false));

            yield return null;

            Assert.IsNotNull(machine.GraphInstance, "Awake instantiates the macro.");
            Assert.AreNotSame(tree, machine.GraphInstance,
                "and the instance must not be the authored asset -- otherwise every agent sharing this tree "
                + "shares its nodes' runtime state.");
            Assert.AreSame(tree, machine.OriginalMacro, "while the original is still remembered as authored.");

            var authoredWait = tree.graph.Nodes.OfType<WaitTime>().First();

            // Deliberately the clone rather than RunningGraph: this assertion is *about* the clone, and
            // reading it through the accessor that abstracts the two apart would stop testing that Awake
            // instantiates anything.
            var runningWait = machine.GraphInstance.graph.Nodes.OfType<WaitTime>().First();

            Assert.AreNotSame(authoredWait, runningWait, "The running nodes are clones,");
            Assert.AreEqual(authoredWait.guid, runningWait.guid,
                "but they keep their guids, which is why a recording taken from a running agent can still be "
                + "read against the asset a designer opens.");
        }

        /// <summary>
        /// Two agents built from one asset behave independently. This is the property that makes a tree a
        /// shared authoring artifact rather than a per-agent one, and it is invisible to any test that spawns
        /// a single agent.
        /// </summary>
        [UnityTest]
        public IEnumerator TwoAgentsSharingOneTreeDoNotShareState()
        {
            var tree = BuildGuardedTree(out var attack, out var idle);

            var seeing = Spawn(tree, (_, variables) => variables.declarations.Set("hasTarget", true));

            secondAgent = new GameObject("Blind");
            secondAgent.SetActive(false);
            var blindMachine = secondAgent.AddComponent<BehaviorTreeMachine>();
            secondAgent.GetComponent<Variables>().declarations.Set("hasTarget", false);
            blindMachine.nest.macro = tree;
            secondAgent.SetActive(true);

            for (int frame = 0; frame < 3; frame++)
            {
                yield return null;
            }

            Assert.AreNotSame(seeing.GraphInstance, blindMachine.GraphInstance,
                "Each machine instantiates its own copy.");

            Assert.IsTrue(Entered(seeing.FlightRecorder, attack),
                "The agent whose fact is true runs the guarded branch,");
            Assert.IsFalse(Entered(seeing.FlightRecorder, idle), "and not the fallback.");

            Assert.IsTrue(Entered(blindMachine.FlightRecorder, idle),
                "while the agent whose fact is false runs the fallback,");
            Assert.IsFalse(Entered(blindMachine.FlightRecorder, attack),
                "and never starts the guarded branch -- two agents, one asset, two different decisions.");
        }

        /// <summary>
        /// Destroying an agent unregisters its recorder. The registry is what the multi-agent debugger lists,
        /// so a machine that outlives its GameObject there is both a leak and a window full of agents that no
        /// longer exist.
        /// </summary>
        [UnityTest]
        public IEnumerator DestroyingAnAgentUnregistersItsRecorder()
        {
            var tree = BuildGuardedTree(out _, out _);
            var machine = Spawn(tree, (_, variables) => variables.declarations.Set("hasTarget", false));

            yield return null;

            var recorder = machine.FlightRecorder;

            Assert.Contains(recorder, BehaviorTreeFlightRecorders.Active.ToArray(),
                "A live agent is registered, which is what the multi-agent view lists.");

            Object.DestroyImmediate(Agent);

            yield return null;

            CollectionAssert.DoesNotContain(BehaviorTreeFlightRecorders.Active.ToArray(), recorder,
                "and a destroyed one is not. OnDestroy detaches it; without that the registry grows for the "
                + "lifetime of the session and lists agents that are gone.");
        }

        /// <summary>
        /// Destroying an agent takes its graph instance out of <see cref="ArcaneOnyx.GraphCore.GraphInstances"/>. The base
        /// machine's Awake registers it there and the base OnDestroy removes it; an override that skipped the
        /// base call left one reference per destroyed agent in the registry for the rest of the session, and
        /// every spawn-and-despawn in a game grew it. A domain reload was the only thing that ever emptied it.
        /// </summary>
        [UnityTest]
        public IEnumerator DestroyingAnAgentReleasesItsGraphInstance()
        {
            var tree = BuildGuardedTree(out _, out _);
            var machine = Spawn(tree, (_, variables) => variables.declarations.Set("hasTarget", false));

            yield return null;

            var before = ArcaneOnyx.GraphCore.GraphInstances.ChildrenOfPooled(machine);
            var registered = before.Count;
            before.Free();

            Assert.AreEqual(1, registered, "Fixture check: a live agent's instance is registered under it.");

            Object.DestroyImmediate(Agent);

            yield return null;

            var after = ArcaneOnyx.GraphCore.GraphInstances.ChildrenOfPooled(machine);
            var remaining = after.Count;
            after.Free();

            Assert.AreEqual(0, remaining,
                "A destroyed agent's instance is gone. Left behind, it pins the reference, its graph data and "
                + "the destroyed machine until the next domain reload -- which never comes with reload disabled.");
        }

        /// <summary>
        /// Entry -> Repeater -> Selector -> [ Attack guarded on hasTarget, Idle unguarded ].
        /// </summary>
        private static BehaviorTreeGraphAsset BuildGuardedTree(out System.Guid attack, out System.Guid idle)
        {
            var asset = NewTree();
            var graph = asset.graph;

            var repeater = Add<Repeater>(graph, 0.0f, 100.0f);
            var selector = Add<Selector>(graph, 0.0f, 250.0f);

            var attackNode = Add<WaitTime>(graph, -400.0f, 400.0f);
            var idleNode = Add<WaitTime>(graph, 400.0f, 400.0f);

            FeedFloat(graph, attackNode, attackNode.Time, 999.0f);
            FeedFloat(graph, idleNode, idleNode.Time, 999.0f);

            Connect(graph, graph.EntryNode, repeater);
            Connect(graph, repeater, selector);
            Connect(graph, selector, attackNode);
            Connect(graph, selector, idleNode);

            var read = ReadAgentVariable(graph, "hasTarget", -700.0f, 250.0f);

            var guard = Add<BooleanReactiveGuard>(graph, -400.0f, 250.0f);
            guard.UpdateOwner(attackNode);
            read.Value.ValidlyConnectTo(guard.Value);

            attack = attackNode.guid;
            idle = idleNode.guid;

            return asset;
        }
    }
}
