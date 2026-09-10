using System;
using System.Linq;
using NUnit.Framework;
using Unity.VisualScripting;

namespace ArcaneOnyx.BehaviorTree.Tests
{
    /// <summary>
    /// The script graph units offer the tree's kinds, not Unity's.
    ///
    /// <para>
    /// <c>Get BT Variable</c> and <c>Set BT Variable</c> kept <see cref="Unity.VisualScripting.VariableKind"/>
    /// after the tree nodes moved to <see cref="BehaviorTreeVariableKind"/>, on the grounds that a flow graph
    /// really does have flow scratch. It does, but a unit named after the tree was offering a store nothing
    /// in a tree can read and the recorder had to throw away every write to. These pin the units to the
    /// tree's enum, keep the convenient default, and hold the <c>Flow</c>-on-disk migration to the same
    /// outcome the nodes have: visibly unconfigured, not quietly repointed.
    /// </para>
    /// </summary>
    [TestFixture]
    public class BehaviorTreeVariableUnitKindTests
    {
        private static readonly Type[] Units = { typeof(GetBehaviorTreeVariable), typeof(SetBehaviorTreeVariable) };

        private static object KindOf(Unit unit) => unit.GetType().GetProperty("kind").GetValue(unit);

        private static Unit Fresh(Type unitType)
        {
            var unit = (Unit)Activator.CreateInstance(unitType);
            unit.Define();
            return unit;
        }

        [Test]
        public void EveryUnit_UsesTheTreesKind_SoFlowIsNotOnOffer()
        {
            foreach (var unitType in Units)
            {
                Assert.AreEqual(typeof(BehaviorTreeVariableKind), unitType.GetProperty("kind").PropertyType,
                    unitType.Name + " offers Unity's kinds, and with them a Flow store no tree can read.");
            }
        }

        [Test]
        public void AFreshUnit_StartsOnTheAgentsFacts()
        {
            foreach (var unitType in Units)
            {
                Assert.AreEqual(BehaviorTreeVariableKind.Object, KindOf(Fresh(unitType)),
                    unitType.Name + ": Object is what a Function reads and writes nearly every time, and "
                    + "None is reserved for a unit that arrived from disk on a kind that no longer exists.");
            }
        }

        [Test]
        public void AUnitStoredAsFlow_LoadsAsNone()
        {
            foreach (var unitType in Units)
            {
                var authored = Serialization.Serialize((object)Fresh(unitType));

                StringAssert.Contains("\"kind\":\"Object\"", authored.json,
                    unitType.Name + " must store its kind by name, or the rename below has nothing to catch.");

                var onDisk = new SerializationData(
                    authored.json.Replace("\"kind\":\"Object\"", "\"kind\":\"Flow\""),
                    authored.objectReferences);

                Assert.AreEqual(BehaviorTreeVariableKind.None, KindOf((Unit)Serialization.Deserialize(onDisk)),
                    unitType.Name + " saved on Flow was writing to a store the recorder dropped. It should "
                    + "arrive as visibly unconfigured, not quietly repointed at a store its author never chose.");
            }
        }

        [Test]
        public void EveryTreeKind_RoundTripsOnAUnit()
        {
            foreach (var unitType in Units)
            foreach (var kind in Enum.GetValues(typeof(BehaviorTreeVariableKind)).Cast<BehaviorTreeVariableKind>())
            {
                var unit = Fresh(unitType);
                unitType.GetProperty("kind").SetValue(unit, kind);

                var back = (Unit)Serialization.Deserialize(Serialization.Serialize((object)unit));

                Assert.AreEqual(kind, KindOf(back), unitType.Name + ": " + kind + " did not survive a round trip.");
            }
        }
    }
}
