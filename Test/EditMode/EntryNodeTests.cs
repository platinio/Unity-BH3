using ArcaneOnyx.GraphCore;
using NUnit.Framework;

namespace ArcaneOnyx.BehaviorTree.Tests
{
    /// <summary>
    /// <see cref="Entry"/> is the root every tick passes through, so it must be perfectly transparent:
    /// whatever the tree below it reports is what the machine sees. Anything it added or swallowed would
    /// misreport the state of every tree in the project.
    /// </summary>
    [TestFixture]
    public class EntryNodeTests
    {
        [Test]
        public void EntryWrapsExactlyOneChild()
        {
            Assert.AreEqual(1, new Entry().MaxChildrenLimit,
                "Entry is the single root of the tree, not a composite.");
        }

        [Test]
        public void EntryPassesItsChildsStatusThroughUnchanged()
        {
            foreach (var expected in new[]
                     {
                         ExecutionStatus.Success, ExecutionStatus.Failure, ExecutionStatus.Running
                     })
            {
                var child = new ScriptedNode(expected);
                var entry = new Entry().WithChildren(child);

                entry.OnNodeEnter();

                Assert.AreEqual(expected, entry.OnUpdateInternal(),
                    $"Entry must report its child's {expected} verbatim.");
            }
        }

        [Test]
        public void EntryEntersAndExitsItsChild()
        {
            var child = new ScriptedNode(ExecutionStatus.Running);
            var entry = new Entry().WithChildren(child);

            entry.OnNodeEnter();
            entry.OnUpdateInternal();
            Assert.AreEqual(1, child.EnterCalls, "The root must start the tree below it.");

            entry.OnNodeExit();
            Assert.AreEqual(1, child.ExitCalls, "And tear it down when the tree stops.");
        }

        [Test]
        public void EmptyEntrySucceeds()
        {
            Assert.AreEqual(ExecutionStatus.Success, new Entry().OnUpdate(),
                "A tree with nothing wired to the root has nothing to fail at.");
        }
    }
}
