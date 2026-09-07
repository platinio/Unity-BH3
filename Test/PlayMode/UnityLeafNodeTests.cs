using System.Collections;
using ArcaneOnyx.GraphCore;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace ArcaneOnyx.BehaviorTree.Tests.PlayMode
{
    /// <summary>
    /// The small <c>Unity/*</c> leaf nodes, ticked on a real agent.
    ///
    /// <para>
    /// This family had no functional coverage at all — no test anywhere constructed one, let alone ran one —
    /// and five of them turned out to be broken outright: a node that never declared a port it read, a cast
    /// that threw on its own defaults, an error logged on every success, and a slerp fed <c>0 / 0</c>. Every
    /// one of them would have been caught by the cheapest test imaginable: put the node in a tree, tick it
    /// once with defaults, and see whether anything throws.
    /// </para>
    ///
    /// <para>
    /// So that is what these are. They assert behaviour where behaviour is cheap to observe, but their real
    /// job is the thing the whole folder was missing — that these nodes run at all.
    /// </para>
    /// </summary>
    public class UnityLeafNodeTests : PlayModeAgentFixture
    {
        /// <summary>
        /// <c>Rotate</c> spins the agent it runs on.
        ///
        /// <para>
        /// It could not, before. <c>Definition</c> declared <c>Speed</c> with an <c>int</c> default on a
        /// <c>float</c> port, and <c>SetDefaultValue</c> type-checks — so <c>Definition</c> threw,
        /// <c>Define()</c> caught it and undefined the node, and <c>Rotate</c> reached every graph with no
        /// ports whatsoever. Behind that sat the reported bug: <c>Axis</c> was never declared either, so the
        /// first read of it was a guaranteed <see cref="System.NullReferenceException"/>.
        /// </para>
        /// </summary>
        [UnityTest]
        public IEnumerator RotateTurnsTheAgentItRunsOn()
        {
            var machine = Spawn(RotatingTree(degreesPerSecond: 180.0f));

            yield return Frames(4);

            Assert.AreNotEqual(Quaternion.identity, Agent.transform.rotation,
                "Rotate has to actually turn the transform. A node whose ports failed to define reads nothing "
                + "and does nothing.");

            Assert.AreNotEqual(ExecutionStatus.Exception, machine.LastExecutionStatus,
                "and it has to do it without throwing.");
        }

        /// <summary>
        /// Speed is degrees per <em>second</em>, not per frame.
        ///
        /// <para>
        /// The old <c>Rotate(axis, speed)</c> scaled with frame rate, which is the class of bug that behaves
        /// on the machine it was authored on and differently everywhere else. Asserted as a bound rather than
        /// an exact angle: four frames is a small fraction of a second, so a correctly scaled 180 deg/s turns
        /// well under 90 degrees, while the unscaled version would have turned 720.
        /// </para>
        /// </summary>
        [UnityTest]
        public IEnumerator RotateAppliesItsSpeedPerSecondRatherThanPerFrame()
        {
            Spawn(RotatingTree(degreesPerSecond: 180.0f));

            yield return Frames(4);

            float turned = Quaternion.Angle(Quaternion.identity, Agent.transform.rotation);

            Assert.Less(turned, 90.0f,
                $"Turned {turned} degrees in four frames. Unscaled, 180 deg/s becomes 180 degrees per frame "
                + "and the agent spins at whatever rate the hardware happens to run at.");
        }

        /// <summary>
        /// <c>GenerateRandomNavMeshPosition</c> answers "no position" with no navmesh, rather than throwing.
        ///
        /// <para>
        /// It read its own <c>float</c> sample-distance port with an <c>(int)</c> cast, and unboxing does not
        /// convert — so the node threw <see cref="System.InvalidCastException"/> the first time it sampled,
        /// with its own defaults, before anyone connected anything to it. There is no navmesh in this scene,
        /// so "no position" is the correct answer; the point is that it is an answer at all.
        /// </para>
        /// </summary>
        [UnityTest]
        public IEnumerator GenerateRandomNavMeshPositionReportsNoPositionWhenThereIsNoNavMesh()
        {
            var tree = NewTree();

            // The node sits beside the flow rather than in it -- it is a data node and cannot be a
            // transition destination -- so the tree needs something else to run.
            var wait = Add<WaitTime>(tree.graph, 0.0f, 100.0f);
            Connect(tree.graph, tree.graph.EntryNode, wait);
            Add<GenerateRandomNavMeshPosition>(tree.graph, 300.0f, 100.0f);

            var machine = Spawn(tree);

            yield return Frames(3);

            var node = RunningNode<GenerateRandomNavMeshPosition>(machine);

            Assert.IsFalse((bool)node.HasPosition.GetPortValue(),
                "With no navmesh to sample there is nowhere to go, and saying so is the node's job now that "
                + "it cannot fail a branch.");
            Assert.AreEqual(Vector3.zero, (Vector3)node.Position.GetPortValue(),
                "A position nobody found must not read as somewhere. Exception would mean the node cannot "
                + "read its own default sample distance.");
        }

        /// <summary>
        /// Setting an animator parameter logs nothing.
        ///
        /// <para>
        /// The three type tests were independent <c>if</c>s with no <c>else</c> and no <c>return</c>, so the
        /// "cannot convert" <c>Debug.LogError</c> ran unconditionally — including immediately after a set that
        /// had worked. Unity's test runner fails a test on an unexpected <c>LogError</c>, so this test failing
        /// <em>is</em> the assertion; the explicit one below only states the intent.
        /// </para>
        /// </summary>
        [UnityTest]
        public IEnumerator SettingAnAnimatorValueLogsNothingWhenItSucceeds()
        {
            var tree = NewTree();
            var node = Add<SetAnimatorValue>(tree.graph, 0.0f, 100.0f);

            FeedString(tree.graph, node, node.ValueName, "Speed");
            FeedInteger(tree.graph, node, node.Value, 7);

            Connect(tree.graph, tree.graph.EntryNode, node);

            var machine = Spawn(tree, (agent, _) => agent.AddComponent<Animator>());

            yield return Frames(3);

            Assert.AreEqual(ExecutionStatus.Success, machine.LastExecutionStatus,
                "A set that worked reports Success and says nothing. Every entry of this node used to log an "
                + "error, successful or not.");
        }

        /// <summary>
        /// A value that is genuinely not an animator parameter type still reports it — once. The other half of
        /// the same change: moving the error into an <c>else</c> must not lose it.
        /// </summary>
        [UnityTest]
        public IEnumerator SettingAnAnimatorValueStillReportsATypeItCannotUse()
        {
            var tree = NewTree();
            var node = Add<SetAnimatorValue>(tree.graph, 0.0f, 100.0f);

            FeedString(tree.graph, node, node.ValueName, "Speed");
            FeedString(tree.graph, node, node.Value, "not a number");

            Connect(tree.graph, tree.graph.EntryNode, node);

            LogAssert.Expect(LogType.Error, new System.Text.RegularExpressions.Regex("cannot set 'Speed'"));

            Spawn(tree, (agent, _) => agent.AddComponent<Animator>());

            yield return Frames(3);
        }

        /// <summary>
        /// <c>SetRotation</c> with the duration it ships with snaps to the target instead of writing garbage.
        ///
        /// <para>
        /// <c>Duration</c> defaults to <c>0</c> and <c>OnUpdate</c> computed <c>currentTime / duration</c> —
        /// so on the first tick that is <c>0 / 0</c>, and <c>Quaternion.Slerp</c> with a NaN <c>t</c> writes a
        /// corrupt rotation into the transform on the very tick the node reports Success. A NaN rotation is
        /// worse than a wrong one: it propagates through anything that reads the transform afterwards.
        /// </para>
        /// </summary>
        [UnityTest]
        public IEnumerator SetRotationWithNoDurationSnapsRatherThanCorruptingTheRotation()
        {
            var tree = NewTree();
            var node = Add<SetRotation>(tree.graph, 0.0f, 100.0f);

            FeedVector3(tree.graph, node, node.TargetRotation, new Vector3(0.0f, 90.0f, 0.0f));

            Connect(tree.graph, tree.graph.EntryNode, node);

            Spawn(tree);

            yield return Frames(3);

            var rotation = Agent.transform.rotation;

            Assert.IsFalse(
                float.IsNaN(rotation.x) || float.IsNaN(rotation.y)
                || float.IsNaN(rotation.z) || float.IsNaN(rotation.w),
                "0 / 0 is NaN, and Slerp writes a NaN t straight into the transform. Nothing downstream that "
                + "reads this rotation recovers from that.");

            Assert.Less(Quaternion.Angle(Quaternion.Euler(0.0f, 90.0f, 0.0f), rotation), 1.0f,
                "and a duration of zero means 'face there now', so it should have arrived.");
        }

        #region Fixture

        /// <summary>Entry -&gt; Rotate, spinning about the default axis at the given speed.</summary>
        private BehaviorTreeGraphAsset RotatingTree(float degreesPerSecond)
        {
            var tree = NewTree();
            var node = Add<Rotate>(tree.graph, 0.0f, 100.0f);

            FeedFloat(tree.graph, node, node.Speed, degreesPerSecond);

            Connect(tree.graph, tree.graph.EntryNode, node);

            return tree;
        }

        private static void FeedInteger(BehaviorTreeGraph graph, BehaviorTreeNode near, ValueInput port, int value)
        {
            var literal = Add<IntegerLiteral>(graph, near.Position.x, near.Position.y + 150.0f);
            SetPrivateField(literal, "value", value);
            literal.Value.ValidlyConnectTo(port);
        }

        private static void FeedVector3(
            BehaviorTreeGraph graph, BehaviorTreeNode near, ValueInput port, Vector3 value)
        {
            var literal = Add<Vector3Literal>(graph, near.Position.x, near.Position.y + 150.0f);
            SetPrivateField(literal, "value", value);
            literal.Value.ValidlyConnectTo(port);
        }

        #endregion
    }
}
