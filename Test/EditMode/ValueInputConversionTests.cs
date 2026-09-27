using System;
using NUnit.Framework;
using Unity.VisualScripting;
using UnityEngine;
using Object = UnityEngine.Object;

namespace ArcaneOnyx.BehaviorTree.Tests
{
    /// <summary>
    /// What may feed a port has to be answered the same way at edit time and at runtime.
    ///
    /// <para>
    /// It was not. <see cref="ValueInput.CanConnectToValid"/> — the gate the canvas asks before it will
    /// draw a wire — accepts any pair where <c>source.Type.IsConvertibleTo(destination.Type, false)</c>:
    /// an <c>int</c> output into a <c>float</c> input, a <c>GameObject</c> into a <c>Transform</c>. But
    /// <see cref="ValueInput.GetValue"/> hands back the raw boxed object, and consumers unboxed it with a
    /// plain cast — and <em>C# unboxing does not convert</em>. Every connection that was legal but not
    /// type-identical threw <see cref="InvalidCastException"/> the first time it was evaluated.
    /// </para>
    ///
    /// <para>
    /// So the two halves of one decision disagreed, and the disagreement was invisible until an agent ran.
    /// These tests state the agreement directly: <em>anything the connection gate accepts must be
    /// readable</em>. That is the property, and it is what stops this bug shape coming back in the next
    /// numeric port someone adds.
    /// </para>
    /// </summary>
    [TestFixture]
    public class ValueInputConversionTests
    {
        private BehaviorTreeGraph graph;

        [SetUp]
        public void SetUp() => graph = new BehaviorTreeGraph();

        private T AddNode<T>() where T : BehaviorTreeNode, new()
        {
            // Ports only exist once the node is in a graph: Nodes.Add fires AfterAdd -> Define().
            var node = new T { Position = new Rect(0.0f, 0.0f, 150.0f, 100.0f) };
            graph.Nodes.Add(node);

            return node;
        }

        /// <summary>
        /// Wires a <typeparamref name="TOut"/> output into a <typeparamref name="TIn"/> input, asserting on
        /// the way that the canvas would in fact have permitted it. That assertion is the point: a test that
        /// connected ports the editor rejects would be pinning a case no designer can reach.
        /// </summary>
        private ValueInput Feed<TOut, TIn>(TOut published)
        {
            var source = AddNode<ValueSource<TOut>>();
            var sink = AddNode<ValueSink<TIn>>();

            source.Published = published;

            Assert.IsTrue(sink.Value.CanConnectToValid(source.Value),
                $"This fixture is only meaningful for pairs the editor accepts, and it rejects "
                + $"{typeof(TOut).Name} -> {typeof(TIn).Name}.");

            source.Value.ValidlyConnectTo(sink.Value);

            return sink.Value;
        }

        #region The agreement

        /// <summary>
        /// The headline case, and the one behind findings 1.4 through 1.6: an integer feeding a float port.
        /// Every numeric port in the codebase was reachable this way, and every one of them threw.
        /// </summary>
        [Test]
        public void AnIntegerOutputCanFeedAFloatPort()
        {
            var port = Feed<int, float>(7);

            Assert.AreEqual(7.0f, port.GetValue<float>(),
                "int -> float is an implicit numeric conversion and the canvas allows the wire, so the "
                + "runtime read has to succeed. A plain (float) unbox of a boxed int throws.");
        }

        /// <summary>
        /// The narrowing direction too. The editor accepts it — <c>IsConvertibleTo(..., guaranteed: false)</c>
        /// admits explicit numeric conversions — so the runtime has to honour it rather than throw on a wire
        /// the author was allowed to draw.
        /// </summary>
        [Test]
        public void AFloatOutputCanFeedAnIntegerPort()
        {
            var port = Feed<float, int>(3.7f);

            Assert.AreEqual(4, port.GetValue<int>(),
                "float -> int rounds, which is Convert.ChangeType's rule and the same one the graph "
                + "serializer already applies.");
        }

        /// <summary>
        /// Unity's own hierarchy conversion. A designer wiring a GameObject into a Transform port is doing
        /// something obviously reasonable, the canvas agrees, and it used to be an exception.
        /// </summary>
        [Test]
        public void AGameObjectOutputCanFeedATransformPort()
        {
            var subject = new GameObject("ValueInputConversionTests_Hierarchy");

            try
            {
                var port = Feed<GameObject, Transform>(subject);

                Assert.AreSame(subject.transform, port.GetValue<Transform>(),
                    "GameObject -> Transform is ConversionUtility's UnityHierarchy conversion: it resolves "
                    + "through GetComponent, which is exactly what the author meant by the wire.");
            }
            finally
            {
                Object.DestroyImmediate(subject);
            }
        }

        /// <summary>A user-defined conversion operator, the third family the connection gate admits.</summary>
        [Test]
        public void AVector3OutputCanFeedAVector2Port()
        {
            var port = Feed<Vector3, Vector2>(new Vector3(1.0f, 2.0f, 3.0f));

            Assert.AreEqual(new Vector2(1.0f, 2.0f), port.GetValue<Vector2>(),
                "Vector2 declares an implicit operator from Vector3, but an implicit operator does not "
                + "survive boxing -- so this only works if something invokes it.");
        }

        /// <summary>
        /// An upcast, which always worked, asserted so that adding conversion cannot have broken the case
        /// that was already fine.
        /// </summary>
        [Test]
        public void AnUpcastStillReadsAsTheBaseType()
        {
            var subject = new GameObject("ValueInputConversionTests_Upcast");

            try
            {
                var port = Feed<Transform, Object>(subject.transform);

                Assert.AreSame(subject.transform, port.GetValue<Object>());
            }
            finally
            {
                Object.DestroyImmediate(subject);
            }
        }

        /// <summary>The overwhelmingly common case: no conversion at all, and the value passes straight through.</summary>
        [Test]
        public void AnExactMatchIsUnchanged()
        {
            var port = Feed<float, float>(2.5f);

            Assert.AreEqual(2.5f, port.GetValue<float>());
        }

        #endregion

        #region Ports that accept any source

        [Test]
        public void TheLogNodesOfferAnInlineMessage()
        {
            foreach (var port in new[]
                     {
                         AddNode<DebugLog>().LogText,
                         AddNode<DebugLogWarning>().LogText,
                         AddNode<DebugLogError>().LogText
                     })
            {
                Assert.IsTrue(port.hasDefaultValue,
                    $"{port.behaviorTreeNode.GetType().Name} needs a declared default for the canvas to draw a field.");
                Assert.IsFalse(port.IsUnfedRequired);

                port.SetDefaultValue("typed on the node");
                Assert.AreEqual("typed on the node", port.GetValue());
            }
        }

        [Test]
        public void ALogNodeStillAcceptsAnOutputThatIsNotAString()
        {
            var source = AddNode<ValueSource<float>>();
            var log = AddNode<DebugLog>();
            source.Published = 2.5f;

            Assert.IsTrue(log.LogText.CanConnectToValid(source.Value));
            Assert.AreSame(log.LogText, log.CompatibleValueInput(typeof(float)));

            source.Value.ValidlyConnectTo(log.LogText);

            Assert.AreEqual(2.5f, log.LogText.GetValue());
        }

        [Test]
        public void AnOrdinaryStringPortStillRejectsAnOutputThatIsNotAString()
        {
            var source = AddNode<ValueSource<float>>();
            var sink = AddNode<ValueSink<string>>();

            Assert.IsFalse(sink.Value.CanConnectToValid(source.Value));
        }

        #endregion

        #region What did not change

        /// <summary>
        /// An unfed, defaultless port still throws <see cref="MissingValuePortInputException"/>. Conversion
        /// is about the value's <em>type</em>; it must not quietly turn a missing wire into a zero, which
        /// would hide the one authoring mistake the canvas already reports well.
        /// </summary>
        [Test]
        public void AnUnfedPortStillReportsTheMissingInput()
        {
            var sink = AddNode<ValueSink<float>>();

            Assert.Throws<MissingValuePortInputException>(() => sink.Value.GetValue<float>());
            Assert.Throws<MissingValuePortInputException>(() => sink.Value.GetValueOrDefault<float>(),
                "the forgiving reader forgives an unusable value, not an absent wire");
        }

        /// <summary>
        /// A destroyed object still reads as Unity-null rather than being rejected as unconvertible. Several
        /// nodes test their target with <c>!= null</c> immediately after reading it, and that check only
        /// works if the fake null reaches them.
        /// </summary>
        [Test]
        public void ADestroyedObjectIsPassedThroughAsUnityNull()
        {
            var subject = new GameObject("ValueInputConversionTests_Destroyed");
            var port = Feed<Transform, Transform>(subject.transform);

            Object.DestroyImmediate(subject);

            var read = port.GetValue<Transform>();

            Assert.IsTrue(read == null, "Unity's own comparison has to still recognise this as destroyed.");
        }

        /// <summary>
        /// The same guarantee on the converting path, which is a different code path and got it wrong.
        ///
        /// <para>
        /// A destroyed source reaches <see cref="ValueInput.GetValue{T}"/> as a live C# reference, so it
        /// takes the conversion branch rather than the fast path — and Unity's hierarchy conversion answers
        /// <c>null</c> for it, which <c>null is T</c> then rejects for every <c>T</c>. The read reported
        /// "there is no conversion between the two" about a value that converts perfectly well and merely
        /// happened to be nothing. The destroyed case is exactly when a node most needs the null it tests
        /// for, so throwing there is the worst possible moment to throw.
        /// </para>
        /// </summary>
        [Test]
        public void ADestroyedObjectIsStillUnityNullWhenTheReadHasToConvert()
        {
            var subject = new GameObject("ValueInputConversionTests_DestroyedConverting");
            var port = Feed<GameObject, Transform>(subject);

            Object.DestroyImmediate(subject);

            Transform read = null;

            Assert.DoesNotThrow(() => read = port.GetValue<Transform>(),
                "A GameObject output on a Transform port is a wire the canvas allows, and a destroyed "
                + "object is a normal thing for one to be carrying.");

            Assert.IsTrue(read == null);
        }

        #endregion

        #region What it does when it cannot

        /// <summary>
        /// A read that cannot possibly succeed throws, and the message says which node, which port, and
        /// which two types — because the cause is nearly always a node reading its own port as a type the
        /// port does not declare, and the old <see cref="InvalidCastException"/> named none of that.
        /// </summary>
        [Test]
        public void AnImpossibleReadThrowsAndSaysWhy()
        {
            var port = Feed<string, string>("not a number");

            var exception = Assert.Throws<InvalidCastException>(() => port.GetValue<Vector3>());

            Assert.That(exception.Message, Does.Contain("Value"), "the port key belongs in the message");
            Assert.That(exception.Message, Does.Contain("Vector3"), "and the type that was asked for");
            Assert.That(exception.Message, Does.Contain("String"), "and the type that was actually there");
        }

        /// <summary>
        /// The message distinguishes the two causes, because they need different fixes: a wire the author
        /// should redo, versus a node bug nobody can fix from the canvas.
        /// </summary>
        [Test]
        public void TheMessageBlamesTheNodeWhenItAskedForATypeItsOwnPortDoesNotDeclare()
        {
            var port = Feed<string, string>("still not a number");

            var nodeBug = Assert.Throws<InvalidCastException>(() => port.GetValue<Vector3>());
            Assert.That(nodeBug.Message, Does.Contain("the node's own bug"),
                "the port declares string and the node asked for Vector3, which no wiring can fix");

            // The realistic shape of the other cause: an object-typed output — GetVariable's, for instance —
            // is connectable to a float port because the type pair permits a downcast, and then the variable
            // turns out to hold a string. The node did nothing wrong; its input did.
            var wiringProblem = Assert.Throws<InvalidCastException>(
                () => Feed<object, float>("nope").GetValue<float>());
            Assert.That(wiringProblem.Message, Does.Contain("whatever feeds it is the problem"),
                "here the node read its port at the declared type, so the value is what is wrong");
        }

        /// <summary>
        /// <c>GetValueOrDefault</c> answers <c>default</c> instead of throwing — the converting replacement
        /// for <c>GetValue() as T</c>, kept non-throwing so migrating those readers could not turn a node
        /// that quietly did nothing into one that takes its branch down.
        /// </summary>
        [Test]
        public void TheForgivingReaderAnswersDefaultRatherThanThrowing()
        {
            var port = Feed<string, string>("text");

            Assert.AreEqual(Vector3.zero, port.GetValueOrDefault<Vector3>());
            Assert.IsNull(port.GetValueOrDefault<Transform>());
        }

        /// <summary>
        /// null is a legitimate value for a reference port and never one for a value type. The second half
        /// used to surface as <see cref="NullReferenceException"/> from the unbox, which said nothing about
        /// which port was empty.
        /// </summary>
        [Test]
        public void NullReadsAsNullForAReferencePortAndIsReportedForAValueType()
        {
            var referencePort = Feed<Transform, Transform>(null);
            Assert.IsNull(referencePort.GetValue<Transform>());

            var valuePort = Feed<string, string>(null);
            Assert.Throws<InvalidCastException>(() => valuePort.GetValue<float>());
        }

        #endregion
    }
}
