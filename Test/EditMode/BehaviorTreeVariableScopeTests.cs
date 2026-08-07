using NUnit.Framework;
using Unity.VisualScripting;

namespace ArcaneOnyx.BehaviorTree.Tests
{
    /// <summary>
    /// Pins the scoping rules a reusable branch depends on: it reads its own values first, still sees what the
    /// agent supplied, and cannot write anywhere a sibling or its caller can observe.
    /// </summary>
    public class BehaviorTreeVariableScopeTests
    {
        private BehaviorTreeVariableScope root;
        private BehaviorTreeVariableScope branchA;
        private BehaviorTreeVariableScope branchB;

        [SetUp]
        public void SetUp()
        {
            var rootDeclarations = new VariableDeclarations();
            rootDeclarations.Set("agentHealth", 100f);
            rootDeclarations.Set("idleTime", 99f);

            root = new BehaviorTreeVariableScope(rootDeclarations);
            branchA = new BehaviorTreeVariableScope(new VariableDeclarations(), root);
            branchB = new BehaviorTreeVariableScope(new VariableDeclarations(), root);
        }

        [Test]
        public void Read_FindsAgentVariableThroughTheChain()
        {
            Assert.AreEqual(100f, branchA.Get("agentHealth"));
        }

        [Test]
        public void Read_PrefersItsOwnDefaultOverTheSameNameOutside()
        {
            var defaults = new VariableDeclarations();
            defaults.Set("idleTime", 3.5f);
            branchA.SeedDefaults(defaults);

            Assert.AreEqual(3.5f, branchA.Get("idleTime"), "the branch's own default should win");
            Assert.AreEqual(99f, branchB.Get("idleTime"), "a sibling without that default still sees the outer value");
        }

        [Test]
        public void SeedDefaults_DoesNotOverwriteWhatIsAlreadyThere()
        {
            branchA.Set("idleTime", 1f);

            var defaults = new VariableDeclarations();
            defaults.Set("idleTime", 3.5f);
            branchA.SeedDefaults(defaults);

            Assert.AreEqual(1f, branchA.Get("idleTime"));
        }

        [Test]
        public void Write_StaysLocalAndIsInvisibleToSiblingAndCaller()
        {
            branchA.Set("timer", 7f);

            Assert.IsTrue(branchA.IsDefined("timer"), "the branch that wrote it should see it");
            Assert.IsFalse(branchB.IsDefined("timer"), "a branch running beside it must not");
            Assert.IsFalse(root.IsDefined("timer"), "and it must not leak up to the agent");
        }

        [Test]
        public void Write_ShadowsAnOuterNameWithoutChangingIt()
        {
            branchA.Set("agentHealth", 1f);

            Assert.AreEqual(1f, branchA.Get("agentHealth"));
            Assert.AreEqual(100f, root.Get("agentHealth"), "the agent's own value must be untouched");
            Assert.AreEqual(100f, branchB.Get("agentHealth"), "and a sibling must still read the agent's");
        }

        [Test]
        public void Read_ThrowsWithAnActionableMessageWhenNothingDeclaresIt()
        {
            var exception = Assert.Throws<System.InvalidOperationException>(() => branchA.Get("neverDeclared"));

            StringAssert.Contains("neverDeclared", exception.Message);
        }

        [Test]
        public void TryGet_ReportsMissingRatherThanThrowing()
        {
            Assert.IsFalse(branchA.TryGet("neverDeclared", out var value));
            Assert.IsNull(value);
        }

        [Test]
        public void Root_IsReachableFromAnyDepth()
        {
            var nested = new BehaviorTreeVariableScope(new VariableDeclarations(), branchA);

            Assert.AreSame(root, nested.Root);
            Assert.AreEqual(100f, nested.Get("agentHealth"), "depth does not cut a branch off from the agent");
        }

        [Test]
        public void Flatten_LetsTheInnerScopeWinAndKeepsWhatItDoesNotDeclare()
        {
            branchA.Set("idleTime", 3.5f);
            branchA.Set("timer", 7f);

            var flattened = branchA.Flatten();

            Assert.AreEqual(3.5f, flattened.Get("idleTime"), "the branch's own value wins");
            Assert.AreEqual(100f, flattened.Get("agentHealth"), "and it still carries the agent's");
            Assert.AreEqual(7f, flattened.Get("timer"));
        }

        [Test]
        public void Flatten_DoesNotCopyOrMutateAtTheRoot()
        {
            branchA.Set("timer", 7f);
            branchA.Flatten();

            Assert.IsFalse(root.Local.IsDefined("timer"), "flattening must not write back into the caller");
            Assert.AreSame(root.Local, root.Flatten(), "a root scope has nothing to merge, so it copies nothing");
        }

        [Test]
        public void SetWhereDeclared_UpdatesTheScopeThatOwnsTheName()
        {
            branchA.SetWhereDeclared("agentHealth", 55f);

            Assert.AreEqual(55f, root.Get("agentHealth"), "an existing outer name is updated in place");
            Assert.IsFalse(branchA.Local.IsDefined("agentHealth"), "and not shadowed locally");
        }
    }
}
