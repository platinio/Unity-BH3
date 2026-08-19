using NUnit.Framework;
using UnityEngine;

namespace ArcaneOnyx.BehaviorTree.Tests
{
    /// <summary>
    /// Telling "the answer was nothing" apart from "the answer has since been destroyed".
    ///
    /// <para>
    /// Unity's overloaded <c>==</c> reports both as null, and every cache in the debugging panels that
    /// remembers a <c>UnityEngine.Object</c> has to treat them oppositely. These tests are short because the
    /// rule is; they exist because the rule is invisible at the call site, where both cases read as
    /// <c>value == null</c> and look handled.
    /// </para>
    /// </summary>
    [TestFixture]
    public class RememberedTests
    {
        private static ScriptableObject Live() => ScriptableObject.CreateInstance<ScriptableObject>();

        [Test]
        public void ARememberedObjectIsNeitherStaleNorLost()
        {
            var asset = Live();

            try
            {
                var remembered = new Remembered<ScriptableObject>(asset);

                Assert.IsFalse(remembered.IsStale);
                Assert.IsTrue(remembered.TryReuse(out var reused));
                Assert.AreSame(asset, reused);
            }
            finally
            {
                Object.DestroyImmediate(asset);
            }
        }

        [Test]
        public void RememberingNothingIsAnAnswerRatherThanStaleness()
        {
            var remembered = new Remembered<ScriptableObject>(null);

            Assert.IsTrue(remembered.TryReuse(out var reused),
                "'No asset matched that name' is a real answer, and re-deriving it every repaint is the cost "
                + "the cache exists to avoid.");
            Assert.IsNull(reused);
            Assert.IsFalse(remembered.IsStale);
        }

        [Test]
        public void ARememberedObjectThatIsDestroyedGoesStale()
        {
            var asset = Live();
            var remembered = new Remembered<ScriptableObject>(asset);

            Object.DestroyImmediate(asset);

            Assert.IsFalse(remembered.TryReuse(out var reused),
                "Handing this back would throw MissingReferenceException out of OnGUI the moment a panel "
                + "read a member of it.");
            Assert.IsNull(reused, "And the out value is nulled too, so a caller ignoring the bool still cannot.");
            Assert.IsTrue(remembered.IsStale);
        }

        /// <summary>
        /// The trap the type exists for, asserted directly: after destruction the two cases are
        /// indistinguishable by the check every call site would otherwise write.
        /// </summary>
        [Test]
        public void ADestroyedObjectAndNothingAreIndistinguishableByANullCheck()
        {
            var asset = Live();
            var destroyed = new Remembered<ScriptableObject>(asset);

            Object.DestroyImmediate(asset);

            var nothing = new Remembered<ScriptableObject>(null);

            destroyed.TryReuse(out var fromDestroyed);
            nothing.TryReuse(out var fromNothing);

            Assert.IsTrue(fromDestroyed == null, "Unity's operator says null.");
            Assert.IsTrue(fromNothing == null, "And says exactly the same thing here.");

            Assert.AreNotEqual(
                nothing.TryReuse(out _), destroyed.TryReuse(out _),
                "Which is the whole point: only the flag recorded at remembering time separates them, and a "
                + "caller reading the value alone cannot tell.");
        }

        [Test]
        public void TheDefaultIsAnEmptyAnswerRatherThanAStaleOne()
        {
            var remembered = default(Remembered<ScriptableObject>);

            Assert.IsTrue(remembered.TryReuse(out var reused),
                "A cache field that has never been written must not read as stale.");
            Assert.IsNull(reused);
        }
    }
}
