using System.Collections;
using ArcaneOnyx.BehaviorTree.Debugging;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace ArcaneOnyx.BehaviorTree.Tests.PlayMode
{
    /// <summary>
    /// What graph an agent is running, and who is entitled to answer that question.
    ///
    /// <para>
    /// <b>"Embedded" here means <c>nest.embed</c></b> — a tree authored into the scene on the machine
    /// itself, with no macro asset behind it. That is a different thing from
    /// <see cref="EmbeddedTreeTests"/>, where the tree is a code-built <c>BehaviorTreeGraphAsset</c> handed
    /// over as a macro. Both are supported; only this one has no clone, which is the whole subject.
    /// </para>
    ///
    /// <para>
    /// The debugging surfaces used to answer it by reading <c>GraphInstance</c>, the machine's private clone
    /// of a macro. An embedded agent has no clone, so they concluded it was running nothing — an empty
    /// topology in the why-inspector, and an event log of bare guids in the flight-recorder window.
    /// </para>
    /// </summary>
    public class MachineRunningGraphTests : PlayModeAgentFixture
    {
        private const float Forever = 999.0f;

        /// <summary>
        /// Stands up an agent whose tree lives on the machine rather than in an asset.
        ///
        /// <para>
        /// Built inactive and switched on afterwards for the same reason <c>Spawn</c> is: the machine reads
        /// its nest in <c>Awake</c>. <c>SwitchToEmbed</c> is the same call <c>AwakenTree</c> makes, and it
        /// clears <c>nest.macro</c> — which is precisely the state under test.
        /// </para>
        /// </summary>
        private BehaviorTreeMachine SpawnEmbedded(BehaviorTreeGraph graph)
        {
            var machine = SpawnDormant();

            machine.nest.SwitchToEmbed(graph);
            machine.gameObject.SetActive(true);

            return machine;
        }

        /// <summary>Entry → WaitTime, long enough that it is still running when the assertions happen.</summary>
        private static BehaviorTreeGraphAsset OneWaitingLeaf(out System.Guid leaf)
        {
            var asset = NewTree();
            var wait = Add<WaitTime>(asset.graph, 0.0f, 200.0f);

            Connect(asset.graph, asset.graph.EntryNode, wait);
            FeedFloat(asset.graph, wait, wait.Time, Forever);

            leaf = wait.guid;

            return asset;
        }

        private static IEnumerator Settle()
        {
            for (var frame = 0; frame < 3; frame++)
            {
                yield return null;
            }
        }

        [UnityTest]
        public IEnumerator AnEmbeddedAgentRunsATreeItsClonedAssetCannotDescribe()
        {
            var asset = OneWaitingLeaf(out var leaf);
            var machine = SpawnEmbedded(asset.graph);

            yield return Settle();

            Assert.IsTrue(Entered(machine.FlightRecorder, leaf), "Fixture check: the agent really is running.");

            Assert.IsNull(machine.GraphInstance,
                "The trap, stated rather than implied: an embedded tree is not cloned, so the property every "
                + "debugging surface used to read is null for an agent that is running perfectly well.");

            Assert.IsNotNull(machine.RunningGraph, "While the machine itself has always known what it ticks.");
        }

        [UnityTest]
        public IEnumerator AnEmbeddedAgentGetsATopologyWithItsNodesInIt()
        {
            var asset = OneWaitingLeaf(out var leaf);
            var machine = SpawnEmbedded(asset.graph);

            yield return Settle();

            var topology = BehaviorTreeGraphTopology.From(machine);

            Assert.IsTrue(topology.TryGetNode(leaf, out var node),
                "Without this the why-inspector explains this agent's nodes by guid, the timeline labels its "
                + "lanes with guids, and the flight-recorder window shows every event truncated.");

            Assert.AreEqual("WaitTime", node.TypeName);
        }

        [UnityTest]
        public IEnumerator AMacroBackedAgentAnswersTheSameWayItAlwaysDid()
        {
            var asset = OneWaitingLeaf(out var leaf);
            var machine = Spawn(asset);

            yield return Settle();

            Assert.IsNotNull(machine.GraphInstance, "Fixture check: this is the cloning path.");
            Assert.AreSame(machine.GraphInstance.graph, machine.RunningGraph,
                "For a macro-backed agent the two agree by construction, because AwakenTree switches the nest "
                + "to the clone's graph before reading it back. That is what makes RunningGraph the general "
                + "answer rather than a second one.");

            Assert.IsTrue(BehaviorTreeGraphTopology.From(machine).TryGetNode(leaf, out _));
        }

        [UnityTest]
        public IEnumerator AnAgentThatHasNotWokenIsRunningNothing()
        {
            var machine = SpawnDormant();

            Assert.IsNull(machine.RunningGraph);
            Assert.IsFalse(BehaviorTreeGraphTopology.From(machine).TryGetNode(System.Guid.NewGuid(), out _),
                "An empty topology rather than a throw: the panels ask before play mode as a matter of course.");

            yield return null;
        }
    }
}
