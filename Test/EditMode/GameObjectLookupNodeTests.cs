using NUnit.Framework;

namespace ArcaneOnyx.BehaviorTree.Tests
{
    /// <summary>
    /// The two scene-lookup nodes disagree about what an unanswered lookup is, and only one of them is safe.
    ///
    /// <para>
    /// <c>GameObject.Find</c> answers null for a null name, but <c>GameObject.FindWithTag</c> throws
    /// <see cref="System.ArgumentException"/> on a null or empty tag. <c>FindGameObjectWithTag</c> declares
    /// its Tag port with no default, so reading a node nobody wired up hits exactly that -- a thrown
    /// exception where the honest answer is "found nothing".
    /// </para>
    ///
    /// <para>
    /// It had never been reachable: the node shipped without a <c>[GraphCreateMenu]</c> entry, so it could
    /// not be created from the canvas at all. Giving it one is what turns its throwing default from dormant
    /// into shipped, which is why the guard lands in the same change.
    /// </para>
    /// </summary>
    [TestFixture]
    public class GameObjectLookupNodeTests
    {
        [Test]
        public void FindingByTagWithNoTagSetAnswersNothingRatherThanThrowing()
        {
            var node = new FindGameObjectWithTag();
            node.Define();

            object found = null;

            Assert.DoesNotThrow(() => found = node.Output.GetPortValue(),
                "an unconnected Tag port reads as null, and FindWithTag throws on a null tag");

            Assert.IsNull(found, "nothing was asked for, so nothing was found");
        }
    }
}
