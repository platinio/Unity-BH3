using System;
using System.Collections;
using System.Linq;
using ArcaneOnyx.BehaviorTree;
using ArcaneOnyx.BehaviorTree.Debugging;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace ArcaneOnyx.BH3.Samples.Tests.PlayMode
{
    public sealed class ClickToMoveSceneTests : SampleSceneFixture
    {
        private const float ArrivalDistance = 0.8f;

        private static readonly Guid MoveTo = NodeIn("ClickToMove", "ClickToMove", "MoveTo");
        private static readonly Guid Idle = NodeIn("ClickToMove", "ClickToMove", "Idle");

        private BehaviorTreeMachine agent;

        protected override string SampleName => "ClickToMove";

        [UnitySetUp]
        public IEnumerator FindTheCast()
        {
            agent = GameObject.Find("Agent").GetComponent<BehaviorTreeMachine>();
            yield break;
        }

        [UnityTest]
        public IEnumerator AClickSendsTheAgentWalkingAndItSettlesBackIntoIdle()
        {
            BehaviorTreeFlightRecorder recorder = RecorderOf(agent);
            yield return Frames(5);
            Assert.IsTrue(Entered(recorder, Idle), "With no order the agent idles,");
            Assert.IsFalse(Entered(recorder, MoveTo), "and has nowhere to go.");

            Vector3 there = OnTheNavMesh(agent.transform.position + Vector3.right * 4f);
            int mark = Mark(recorder);
            Click(there, orderNumber: 1);
            yield return Frames(3);
            Assert.IsTrue(EnteredSince(recorder, mark, MoveTo), "A numbered order starts MoveTo,");
            Assert.IsTrue(TakenOverSince(recorder, mark, Idle), "taking the slot from Idle.");

            yield return Until(() => FlatDistance(agent.transform.position, there) < ArrivalDistance, 15f, "The agent arriving");
            yield return Until(() => CompletedOrder() == 1, 2f, "MoveTo reporting the order complete");
            yield return Until(() => EntriesAtLeast(recorder, Idle) >= 2, 2f, "Idle resuming after the trip");
        }

        [UnityTest]
        public IEnumerator ANewClickMidWayRedirectsTheAgent()
        {
            BehaviorTreeFlightRecorder recorder = RecorderOf(agent);
            Vector3 first = OnTheNavMesh(agent.transform.position + Vector3.right * 4f);
            Vector3 second = OnTheNavMesh(agent.transform.position + Vector3.left * 4f);
            yield return Frames(5);

            int mark = Mark(recorder);
            Click(first, orderNumber: 1);
            yield return Seconds(0.5f);
            Click(second, orderNumber: 2);
            yield return Frames(3);
            int tripsStarted = EventsSince(recorder, mark).Count(recorded => recorded.Kind == BehaviorTreeEventKind.NodeEnter && recorded.NodeGuid == MoveTo);
            Assert.AreEqual(2, tripsStarted, "MoveTo gives up the first trip and starts again on the new order.");

            yield return Until(() => FlatDistance(agent.transform.position, second) < ArrivalDistance, 15f, "The agent arriving at the second point");
            yield return Until(() => CompletedOrder() == 2, 2f, "The second order being the one reported complete");
        }

        private void Click(Vector3 point, int orderNumber)
        {
            Publish(agent, ClickToMoveFacts.Destination, point);
            Publish(agent, ClickToMoveFacts.OrderNumber, orderNumber);
        }

        private int CompletedOrder()
        {
            object completed = Fact(agent, ClickToMoveFacts.CompletedOrder);
            return completed == null ? 0 : Convert.ToInt32(completed);
        }
    }
}
