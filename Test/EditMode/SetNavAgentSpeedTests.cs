using System.Linq;
using System.Text.RegularExpressions;
using ArcaneOnyx.GraphCore;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace ArcaneOnyx.BehaviorTree.Tests
{
    [TestFixture]
    public class SetNavAgentSpeedTests
    {
        private const float WalkingSpeed = 1.4f;

        private BehaviorTreeGraph graph;
        private GameObject agent;
        private SetNavAgentSpeed setNavAgentSpeed;

        [SetUp]
        public void SetUp()
        {
            graph = new BehaviorTreeGraph();
            agent = new GameObject("Agent");
            setNavAgentSpeed = AddNode<SetNavAgentSpeed>();
        }

        [TearDown]
        public void TearDown() => Object.DestroyImmediate(agent);

        private T AddNode<T>() where T : BehaviorTreeNode, new()
        {
            // Ports only exist once the node is in a graph: Nodes.Add fires AfterAdd -> Define().
            var node = new T { Position = new Rect(0.0f, 0.0f, 150.0f, 100.0f) };
            graph.Nodes.Add(node);

            return node;
        }

        private void FeedTheAgentAndTheSpeed()
        {
            var target = AddNode<ValueSource<GameObject>>();
            var speed = AddNode<ValueSource<float>>();
            target.Published = agent;
            speed.Published = WalkingSpeed;
            target.Value.ValidlyConnectTo(setNavAgentSpeed.Target);
            speed.Value.ValidlyConnectTo(setNavAgentSpeed.Speed);
        }

        [Test]
        public void TheAgentWalksAtTheSpeedItWasGiven()
        {
            NavMeshAgent navAgent = agent.AddComponent<NavMeshAgent>();
            FeedTheAgentAndTheSpeed();

            setNavAgentSpeed.OnEnter();

            Assert.AreEqual(ExecutionStatus.Success, setNavAgentSpeed.OnUpdate());
            Assert.AreEqual(WalkingSpeed, navAgent.speed, 1e-5f);
        }

        [Test]
        public void OnATargetThatCannotWalk_ItFails_AndSaysWhy()
        {
            // The node reports against the object its machine sits on, so this one runs on a machine.
            BehaviorTreeMachine machine = agent.AddComponent<BehaviorTreeMachine>();
            graph.SetMachine(machine);
            setNavAgentSpeed.SetMachine(machine);
            FeedTheAgentAndTheSpeed();
            LogAssert.Expect(LogType.Error, new Regex("found no NavMeshAgent"));

            setNavAgentSpeed.OnEnter();

            Assert.AreEqual(ExecutionStatus.Failure, setNavAgentSpeed.OnUpdate());
        }

        [Test]
        public void TheSpeedMustBeFed_TheTargetMayBeLeftForTheAgentItself()
        {
            var speed = setNavAgentSpeed.valueInputs.First(port => port.key == nameof(SetNavAgentSpeed.Speed));
            var target = setNavAgentSpeed.valueInputs.First(port => port.key == nameof(SetNavAgentSpeed.Target));

            Assert.That(speed.IsUnfedRequired, Is.True, "a speed nobody chose would make every agent walk at a made-up number");
            Assert.That(target.IsUnfedRequired, Is.False);
        }
    }
}
