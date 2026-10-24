using NUnit.Framework;
using Unity.VisualScripting;
using UnityEngine;

namespace ArcaneOnyx.BehaviorTree.Tests
{
    [TestFixture]
    public class ValueInputWiringTests
    {
        private BehaviorTreeGraph graph;
        private bool wasWiringFixed;
        private GameObject agent;

        [SetUp]
        public void SetUp()
        {
            graph = new BehaviorTreeGraph();
            wasWiringFixed = ValueInput.isWiringFixed;
        }

        [TearDown]
        public void TearDown()
        {
            ValueInput.isWiringFixed = wasWiringFixed;

            if (agent != null) Object.DestroyImmediate(agent);
        }

        [Test]
        public void TheEditorReadsAPortRewiredWhileItRuns()
        {
            var port = Feed(AddSource(1.0f));
            Assert.AreEqual(1.0f, port.GetValue<float>());

            AddSource(2.0f).Value.ValidlyConnectTo(port);

            Assert.AreEqual(2.0f, port.GetValue<float>());
        }

        [Test]
        public void APlayerKeepsTheOutputAPortFirstRead()
        {
            ValueInput.isWiringFixed = true;
            var port = Feed(AddSource(1.0f));
            Assert.AreEqual(1.0f, port.GetValue<float>());

            AddSource(2.0f).Value.ValidlyConnectTo(port);

            Assert.AreEqual(1.0f, port.GetValue<float>());
        }

        [Test]
        public void APlayerRemembersTheWireNotTheValue()
        {
            ValueInput.isWiringFixed = true;
            var source = AddSource(1.0f);
            var port = Feed(source);
            Assert.AreEqual(1.0f, port.GetValue<float>());

            source.Published = 5.0f;

            Assert.AreEqual(5.0f, port.GetValue<float>());
        }

        [Test]
        public void APlayerReadsTheInlineValueOfAnUnwiredPortOnEveryRead()
        {
            ValueInput.isWiringFixed = true;
            var port = AddNode<ValueSink<float>>().Value;
            port.SetDefaultValue(3.0f);

            Assert.AreEqual(3.0f, port.GetValue<float>());
            Assert.AreEqual(3.0f, port.GetValue<float>());
        }

        [Test]
        public void APlayerReportsAnUnfedPortOnEveryRead()
        {
            ValueInput.isWiringFixed = true;
            var port = AddNode<ValueSink<float>>().Value;

            Assert.Throws<MissingValuePortInputException>(() => port.GetValue());
            Assert.Throws<MissingValuePortInputException>(() => port.GetValue());
        }

        [Test]
        public void APlayerFindsTheComponentOnTheRememberedOutput()
        {
            ValueInput.isWiringFixed = true;
            agent = new GameObject(nameof(APlayerFindsTheComponentOnTheRememberedOutput));
            var source = AddNode<ValueSource<GameObject>>();
            source.Published = agent;
            var port = AddNode<ValueSink<GameObject>>().Value;
            source.Value.ValidlyConnectTo(port);

            Assert.AreSame(agent.transform, port.GetComponent<Transform>());
        }

        [Test]
        public void APlayerFindsNoComponentOnAnUnwiredPort()
        {
            ValueInput.isWiringFixed = true;
            var port = AddNode<ValueSink<GameObject>>().Value;

            Assert.IsNull(port.GetComponent<Transform>());
        }

        private ValueSource<float> AddSource(float published)
        {
            var source = AddNode<ValueSource<float>>();
            source.Published = published;

            return source;
        }

        private ValueInput Feed(ValueSource<float> source)
        {
            var port = AddNode<ValueSink<float>>().Value;
            source.Value.ValidlyConnectTo(port);

            return port;
        }

        private T AddNode<T>() where T : BehaviorTreeNode, new()
        {
            var node = new T { Position = new Rect(0.0f, 0.0f, 150.0f, 100.0f) };
            graph.Nodes.Add(node);

            return node;
        }
    }
}
