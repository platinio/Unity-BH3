using System;
using System.Reflection;
using System.Runtime.Serialization;
using NUnit.Framework;

namespace ArcaneOnyx.BehaviorTree.Tests
{
    /// <summary>
    /// A node has to work when its field initialisers never ran.
    ///
    /// <para>
    /// The serializer calls a constructor when the type offers a parameterless one, and materialises the
    /// object directly when it does not — and the direct route runs no field initialisers at all. Fields
    /// marked <c>[DoNotSerialize]</c> are the ones that suffer, because nothing restores them afterwards
    /// either: they arrive null and the first read throws.
    /// <see cref="FormatterServices.GetUninitializedObject"/> below is that route, used deliberately — a node
    /// built this way is the honest model of one the serializer had to materialise.
    /// </para>
    ///
    /// <para>
    /// Two defences, tested here because they fail differently.
    /// <c>NodeTypes_AreConstructibleByTheSerializer</c> keeps every node type on the constructor route, which
    /// is the real fix; reaching a cache through a lazy property is what makes it safe anyway if some type
    /// ever leaves that route again.
    /// </para>
    /// </summary>
    [TestFixture]
    public class DeserializedNodeStateTests
    {
        private static object FieldOf(object node, string name) =>
            typeof(BehaviorTreeNode)
                .GetField(name, BindingFlags.Instance | BindingFlags.NonPublic)
                .GetValue(node);

        /// <summary>
        /// The guard-index cache is reached through a lazy property, so a node that arrives with the field
        /// null answers instead of throwing. This was the shape that threw on 240 nodes across the shipped
        /// trees.
        /// </summary>
        [Test]
        public void ANodeWhoseInitialisersNeverRanStillAnswersForItsGuardIndex()
        {
            var node = (Sequence)FormatterServices.GetUninitializedObject(typeof(Sequence));

            Assert.IsNull(FieldOf(node, "conditionalExecutionIndexCache"),
                "the fixture is only meaningful while the field really did arrive null");

            var graph = new BehaviorTreeGraph();
            graph.Nodes.Add(node);

            // Reading comes first on purpose: clearing would create the dictionary as a side effect and
            // hide whether the read path is the safe one.
            int index = -1;
            Assert.DoesNotThrow(() => index = node.GetConditionalIndex(new CountingGuard()),
                "a node the serializer had to materialise is a normal node, not an edge case");

            Assert.AreEqual(0, index, "no guards are armed on it, so the one asked about counts as first");

            Assert.DoesNotThrow(() => node.ClearConditionalExecutionIndexCache(),
                "clearing a cache that was never created is not an error, it is nothing to do");
        }

        /// <summary>
        /// <c>PlaceHolderNode</c> is why this fixture exists: it was the only node type with no parameterless
        /// constructor, so it was the only one the serializer had to materialise directly.
        /// </summary>
        [Test]
        public void APlaceHolderNodeIsBuiltThroughAConstructorLikeEveryOtherNode()
        {
            Assert.IsNotNull(typeof(PlaceHolderNode).GetConstructor(Type.EmptyTypes),
                "without this the serializer skips every field initialiser on the type");

            var node = Activator.CreateInstance<PlaceHolderNode>();

            var graph = new BehaviorTreeGraph();
            graph.Nodes.Add(node);

            Assert.DoesNotThrow(() => node.GetConditionalIndex(new CountingGuard()));
        }
    }
}
