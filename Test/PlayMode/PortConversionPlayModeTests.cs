using System.Collections;
using ArcaneOnyx.GraphCore;
using NUnit.Framework;
using UnityEngine.TestTools;

namespace ArcaneOnyx.BehaviorTree.Tests.PlayMode
{
    /// <summary>
    /// The two wires a designer actually draws that used to break the agent.
    ///
    /// <para>
    /// The edit-mode fixture pins the rule at the port; this one pins that the rule reaches a running tree,
    /// through the same objects an author would use — a literal dragged onto a port, an agent variable read
    /// by <see cref="GetVariable"/>. Both of these were legal to author, produced no warning on the canvas,
    /// and threw <see cref="System.InvalidCastException"/> on the tick that first evaluated them.
    /// </para>
    ///
    /// <para>
    /// That failure mode is the reason these are worth a play-mode test rather than only a unit one: the
    /// exception did not surface where the mistake was made. It surfaced frames later, inside whichever
    /// branch happened to run first, as a tree that stopped.
    /// </para>
    /// </summary>
    public class PortConversionPlayModeTests : PlayModeAgentFixture
    {
        /// <summary>
        /// A Float literal on an <c>int</c> port. The canvas accepts the wire — float to int is an explicit
        /// numeric conversion and <c>CanConnectToValid</c> asks for convertible, not identical — so the
        /// runtime has to accept it too.
        ///
        /// <para>
        /// <c>GenerateRandomNavMeshPosition</c> is the subject because it answers without a navmesh: with
        /// nothing to sample it reports Failure, which is a real answer. What is being tested is that it
        /// reaches an answer at all rather than throwing while reading <c>MaxTries</c>.
        /// </para>
        /// </summary>
        [UnityTest]
        public IEnumerator AFloatLiteralOnAnIntegerPortIsReadRatherThanThrown()
        {
            var tree = NewTree();
            var node = AddNavMeshPosition(tree.graph, 0.0f, 100.0f);

            FeedFloat(tree.graph, node, node.MaxTries, 3.0f);

            Connect(tree.graph, tree.graph.EntryNode, node);

            var machine = Spawn(tree);

            yield return Frames(3);

            Assert.AreEqual(ExecutionStatus.Failure, machine.LastExecutionStatus,
                "There is no navmesh here, so Failure is the right answer. Exception would mean the node "
                + "could not read a port the editor let the author fill in.");
        }

        /// <summary>
        /// An agent variable holding an <c>int</c>, feeding a <c>float</c> port.
        ///
        /// <para>
        /// This is the common one, and the one that is hardest to see coming. <c>GetVariable</c> publishes
        /// <c>object</c>, so it connects to anything; whether the read works then depends on what the
        /// variable happens to hold at that moment. A designer who typed <c>3</c> instead of <c>3.0</c> —
        /// or a script that wrote an int — armed a tree that ran correctly until this branch was reached.
        /// Nothing on the canvas could have shown it.
        /// </para>
        /// </summary>
        [UnityTest]
        public IEnumerator AnAgentVariableHoldingAnIntCanFeedAFloatPort()
        {
            var tree = NewTree();
            var wait = Add<WaitTime>(tree.graph, 0.0f, 100.0f);
            var read = ReadAgentVariable(tree.graph, "WaitSeconds", -250.0f, 100.0f);

            read.Value.ValidlyConnectTo(wait.Time);

            Connect(tree.graph, tree.graph.EntryNode, wait);

            var machine = Spawn(tree, (_, variables) => variables.declarations.Set("WaitSeconds", 5));

            yield return Frames(3);

            Assert.AreEqual(ExecutionStatus.Running, machine.LastExecutionStatus,
                "Five seconds have not passed, so the wait is still running. Exception would mean an int in "
                + "an agent variable cannot drive a float port -- which is most of what agent variables are "
                + "for.");
        }

        // The obvious third case -- an agent variable holding something that is not a number at all -- is
        // deliberately not tested here, and the reason is worth recording.
        //
        // At the port it behaves correctly: the read throws rather than converting "soon" to zero, which is
        // pinned in ValueInputConversionTests. What happens next is not this change's to promise. No node
        // and no graph catches anything a lifecycle hook throws, so the exception escapes into Unity's
        // Update, that frame is abandoned, and the following frame ticks WaitTime with its timer still at
        // its stale value -- so the branch reports Success as though the wait had elapsed. That is
        // pre-existing and was identical under the old InvalidCastException; a test asserting otherwise
        // would be pinning behaviour the codebase does not have. It is reported as a separate finding.

        private static IEnumerator Frames(int count)
        {
            for (int frame = 0; frame < count; frame++)
            {
                yield return null;
            }
        }
    }
}
