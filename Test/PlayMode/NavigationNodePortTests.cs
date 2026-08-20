using System.Collections;
using System.Collections.Generic;
using NUnit.Framework;
using System.Text.RegularExpressions;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.TestTools;

namespace ArcaneOnyx.BehaviorTree.Tests.PlayMode
{
    /// <summary>
    /// Pins the port contracts of the navigation nodes — the kind of defect where a port's declared type and
    /// the cast in <c>OnUpdate</c> disagree, which no verifier can see because the tree is perfectly well
    /// formed right up until the first tick.
    /// </summary>
    public class NavigationNodePortTests : PlayModeAgentFixture
    {
        /// <summary>
        /// <c>SampleDistance</c> is declared <c>ValueInput&lt;float&gt;</c>, so the boxed value arriving at
        /// <c>OnUpdate</c> is a <c>float</c> — unboxing it as <c>int</c> throws <c>InvalidCastException</c>
        /// on every tick, including with the port's own declared default. The node then can never return
        /// anything, which presents as an agent that simply stops in whatever branch reached it.
        /// </summary>
        [UnityTest]
        public IEnumerator SampleDistance_IsReadAsTheFloatItsPortDeclares()
        {
            var tree = NewTree();
            var graph = tree.graph;

            var node = Add<GenerateRandomNavMeshPosition>(graph, 0.0f, 200.0f);
            FeedFloat(graph, node, node.SampleDistance, 4.0f);
            FeedString(graph, node, node.PositionKey, "probePoint");
            Connect(graph, graph.EntryNode, node);

            var guid = node.guid;
            var machine = Spawn(tree);

            // One real frame is enough: the cast sits in OnUpdate's argument evaluation, so a broken node
            // throws before it can succeed or fail. With no NavMesh in the test scene the healthy outcome is
            // a clean Failure — what matters is that the node exits at all instead of throwing every tick.
            yield return null;
            yield return null;

            Assert.IsTrue(Entered(machine.FlightRecorder, guid),
                "The node never entered — the fixture wiring is wrong, not the cast under test.");
            Assert.GreaterOrEqual(ExitCount(machine.FlightRecorder, guid), 1,
                "The node entered but never exited: its OnUpdate cannot complete, which is exactly what an "
                + "InvalidCastException on a port read looks like from outside.");
        }

        /// <summary>
        /// A <c>NavMeshAgent</c> stops moving at its own <c>stoppingDistance</c>, so
        /// <c>WaitUntilReachNavTargetPosition</c> must judge arrival against it. The old fixed 1mm threshold
        /// only ever completed for agents with <c>stoppingDistance</c> zero; everyone else parked at their
        /// stopping distance and waited forever — which presents as an agent standing next to its
        /// destination, permanently "travelling".
        /// </summary>
        [UnityTest]
        public IEnumerator ArrivalRespectsTheAgentsStoppingDistance()
        {
            // A NavMesh built at runtime, so the test does not depend on a scene with a baked one.
            var floor = new NavMeshBuildSource
            {
                shape = NavMeshBuildSourceShape.Box,
                size = new Vector3(20.0f, 0.1f, 20.0f),
                transform = Matrix4x4.identity,
                area = 0
            };
            var data = UnityEngine.AI.NavMeshBuilder.BuildNavMeshData(
                NavMesh.GetSettingsByID(0), new List<NavMeshBuildSource> { floor },
                new Bounds(Vector3.zero, new Vector3(40.0f, 4.0f, 40.0f)),
                Vector3.zero, Quaternion.identity);
            var navMeshInstance = NavMesh.AddNavMeshData(data);

            try
            {
                var tree = NewTree();
                var graph = tree.graph;

                var sequence = Add<Sequence>(graph, 0.0f, 200.0f);
                Connect(graph, graph.EntryNode, sequence);

                var setDestination = Add<SetNavAgentPosition>(graph, -150.0f, 400.0f);
                var destination = Add<Vector3Literal>(graph, -150.0f, 550.0f);
                SetPrivateField(destination, "value", new Vector3(5.0f, 0.0f, 5.0f));
                destination.Value.ValidlyConnectTo(setDestination.NavPosition);
                Connect(graph, sequence, setDestination);

                var arrive = Add<WaitUntilReachNavTargetPosition>(graph, 150.0f, 400.0f);
                Connect(graph, sequence, arrive);

                var guid = arrive.guid;
                var machine = Spawn(tree, (agentObject, _) =>
                {
                    var navAgent = agentObject.AddComponent<NavMeshAgent>();
                    navAgent.stoppingDistance = 0.6f;
                });

                // ~7m at default agent speed is a couple of seconds; the deadline only bounds the failure
                // case, where the agent has parked at its stopping distance and the node never exits.
                float deadline = Time.time + 10.0f;
                while (Time.time < deadline && ExitCount(machine.FlightRecorder, guid) == 0)
                {
                    yield return null;
                }

                Assert.GreaterOrEqual(ExitCount(machine.FlightRecorder, guid), 1,
                    "The agent stopped at its stoppingDistance but the node kept waiting for a 1mm "
                    + "approach that a stopped agent can never make.");
            }
            finally
            {
                navMeshInstance.Remove();
            }
        }

        /// <summary>
        /// A navigation node whose agent cannot be found fails, rather than throwing on every entry.
        ///
        /// <para>
        /// The three navigation nodes resolved their <c>NavMeshAgent</c> in <c>OnAwake</c> and dereferenced
        /// it without a check. On an agent that has no <c>NavMeshAgent</c> -- a tree dropped onto the wrong
        /// prefab, which is an authoring mistake and a normal one -- the cache was null and the first entry
        /// was a <c>NullReferenceException</c> from inside the node. That takes the branch down and says
        /// nothing about why.
        /// </para>
        /// </summary>
        [UnityTest]
        public IEnumerator ANavNodeWithNoAgentFailsInsteadOfThrowing()
        {
            var tree = NewTree();
            var graph = tree.graph;

            var node = Add<StopNavAgent>(graph, 0.0f, 200.0f);
            Connect(graph, graph.EntryNode, node);

            var guid = node.guid;

            LogAssert.Expect(LogType.Error, new Regex("found no NavMeshAgent"));

            // Spawned bare: the fixture's agent carries a machine, not a NavMeshAgent, which is exactly the
            // situation under test.
            var machine = Spawn(tree);

            yield return null;
            yield return null;

            Assert.IsTrue(Entered(machine.FlightRecorder, guid),
                "The node never entered -- the fixture wiring is wrong, not the guard under test.");
            Assert.GreaterOrEqual(ExitCount(machine.FlightRecorder, guid), 1,
                "The node entered and never exited, which is what an exception thrown inside OnEnter looks "
                + "like from outside.");
        }

    }
}
