using System.Collections;
using NUnit.Framework;
using UnityEngine.TestTools;

namespace ArcaneOnyx.BehaviorTree.Tests.PlayMode
{
    /// <summary>
    /// What a cache keyed on a flight recorder may and may not assume when an agent changes tree.
    ///
    /// <para>
    /// The flight-recorder window builds a guid-to-node index and keeps it while "nothing has changed". Which
    /// facts count as change is a decision about the runtime, not about the window, and it is wrong in a way
    /// nobody would notice: a stale index does not throw, it quietly prints truncated guids instead of node
    /// names for the rest of the agent's life.
    /// </para>
    ///
    /// <para>
    /// So the premises that decision rests on are pinned here, where they live. The window's own guard is not
    /// reachable from a test — it is private to an <c>EditorWindow</c> — but it cannot be right if these are
    /// not true.
    /// </para>
    /// </summary>
    public class RecorderIdentityAcrossSwitchTests : PlayModeAgentFixture
    {
        private const float Forever = 999.0f;

        /// <summary>A tree with no sub-trees, so switching between two of these registers no new call site.</summary>
        private static BehaviorTreeGraphAsset LeafOnlyTree()
        {
            var asset = NewTree();
            var wait = Add<WaitTime>(asset.graph, 0.0f, 200.0f);

            Connect(asset.graph, asset.graph.EntryNode, wait);
            FeedFloat(asset.graph, wait, wait.Time, Forever);

            return asset;
        }

        private static IEnumerator Settle()
        {
            for (var frame = 0; frame < 3; frame++)
            {
                yield return null;
            }
        }

        /// <summary>
        /// Why the window cannot key on the recorder alone. The recorder is attached once in <c>Awake</c> and
        /// deliberately outlives every switch, so that one recording spans the swap instead of restarting at
        /// it — which means "same recorder" says nothing about "same tree".
        /// </summary>
        [UnityTest]
        public IEnumerator SwitchingTreesKeepsTheSameRecorder()
        {
            var machine = Spawn(LeafOnlyTree());

            yield return Settle();

            var before = machine.FlightRecorder;

            machine.Switch(LeafOnlyTree());

            yield return Settle();

            Assert.AreSame(before, machine.FlightRecorder);
        }

        /// <summary>
        /// And why keying on the graph instance works. <c>Switch</c> releases the old clone and
        /// <c>AwakenTree</c> makes a new one, so the identity of the thing the index was built from is
        /// exactly what changes.
        /// </summary>
        [UnityTest]
        public IEnumerator SwitchingTreesReplacesTheGraphInstance()
        {
            var machine = Spawn(LeafOnlyTree());

            yield return Settle();

            var before = machine.GraphInstance;
            Assert.IsNotNull(before, "Fixture check.");

            machine.Switch(LeafOnlyTree());

            yield return Settle();

            Assert.IsNotNull(machine.GraphInstance);
            Assert.AreNotSame(before, machine.GraphInstance,
                "An index built from the old instance points at nodes ReleaseTree has already destroyed.");
        }

        /// <summary>
        /// The case that makes the call-site count insufficient on its own. Only a
        /// <c>RunBehaviorTreeGraphNode</c> registers a call site, so two trees with no sub-trees swap without
        /// moving the count at all — a boss changing phase, which is the use case <c>Switch</c>'s own doc
        /// names.
        /// </summary>
        [UnityTest]
        public IEnumerator SwitchingBetweenSubTreeFreeTreesDoesNotMoveTheCallSiteCount()
        {
            var machine = Spawn(LeafOnlyTree());

            yield return Settle();

            var before = machine.FlightRecorder.CallSites.Count;

            machine.Switch(LeafOnlyTree());

            yield return Settle();

            Assert.AreEqual(before, machine.FlightRecorder.CallSites.Count,
                "So a guard watching only this would never notice the agent was running a different tree.");
        }
    }
}
