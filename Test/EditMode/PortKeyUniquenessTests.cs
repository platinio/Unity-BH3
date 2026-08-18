using System.Text.RegularExpressions;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace ArcaneOnyx.BehaviorTree.Tests
{
    /// <summary>
    /// A port key identifies a port, so two ports may not share one.
    ///
    /// <para>
    /// The four port factories on <see cref="BehaviorTreeNode"/> used to carry
    /// <c>//EnsureUniqueInput(key);</c> — commented out, ported dead from the Unity Visual Scripting
    /// <c>Unit</c> BH3 stopped extending, and read for years as "this is unprotected". It is not:
    /// <c>PortCollection</c> is a <c>KeyedCollection</c>, so the second add throws. The protection was
    /// real, incidental, and untested — which is the part worth fixing, because a change of collection
    /// type would remove it with nothing to notice.
    /// </para>
    ///
    /// <para>
    /// What it protects: <c>defaultValues</c> is keyed by string with no room for a direction or a type,
    /// so one key would have to mean two defaults; and <c>NodePreservation</c> resolves stored connections
    /// with <c>inputs.Single(p =&gt; p.key == key)</c> on every <c>Define()</c>, which throws on two
    /// matches rather than quietly picking one.
    /// </para>
    /// </summary>
    [TestFixture]
    public class PortKeyUniquenessTests
    {
        /// <summary>
        /// The refusal, and the shape it arrives in. <c>Define()</c> catches whatever <c>Definition()</c>
        /// throws, warns and undefines — so the node does not come back holding one of the two ports, it
        /// comes back holding none, which is the only outcome that cannot be mistaken for having worked.
        /// </summary>
        [Test]
        public void ANodeDeclaringTwoPortsUnderOneKeyKeepsNeitherOfThem()
        {
            var node = new DuplicateKeyNode();

            LogAssert.Expect(LogType.Warning, new Regex("Failed to define"));

            node.Define();

            Assert.IsFalse(node.isDefined,
                "a duplicate key is a programming error in Definition(), and Define() undefines on a throw");

            CollectionAssert.IsEmpty(node.valueInputs,
                "half-defined would be worse than not defined: the node would look usable and read wrong");
        }
    }
}
