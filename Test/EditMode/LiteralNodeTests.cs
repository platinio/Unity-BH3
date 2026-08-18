using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;

namespace ArcaneOnyx.BehaviorTree.Tests
{
    /// <summary>
    /// A literal's port type is a promise, and the backing field is what keeps it.
    ///
    /// <para>
    /// <c>ValueInput.GetValue</c> and <c>ValueOutput.GetPortValue</c> both hand consumers the <em>raw boxed
    /// object</em>, and C# unboxing does not convert: <c>(int)</c> applied to a boxed <c>float</c> throws
    /// <see cref="InvalidCastException"/>, and the implicit <c>Vector3</c>-to-<c>Vector2</c> conversion does
    /// not survive boxing either. So a literal whose field type differs from the type its port declares does
    /// not quietly coerce — it throws at every consumer that reads it the idiomatic way.
    /// </para>
    ///
    /// <para>
    /// Two shipped literals had exactly that: <c>IntegerLiteral</c> stored a <c>float</c> behind a
    /// <c>ValueOutput&lt;int&gt;</c>, and <c>Vector2Literal</c> a <c>Vector3</c> behind a
    /// <c>ValueOutput&lt;Vector2&gt;</c>. Both were copy-paste from their float/Vector3 siblings, and neither
    /// could ever have worked. The sweep below is written over <em>every</em> literal rather than those two,
    /// because the next copy-paste is the thing worth catching.
    /// </para>
    /// </summary>
    [TestFixture]
    public class LiteralNodeTests
    {
        private static readonly Assembly RuntimeAssembly = typeof(BehaviorTreeNode).Assembly;

        private static IEnumerable<Type> RuntimeTypes()
        {
            try
            {
                return RuntimeAssembly.GetTypes();
            }
            catch (ReflectionTypeLoadException ex)
            {
                return ex.Types.Where(t => t != null);
            }
        }

        /// <summary>
        /// The literals that publish a stored constant — the ones with a serialized <c>value</c> field.
        /// <para>
        /// Not every <see cref="Literal"/> is one of these. <c>GetThisGameObject</c> and
        /// <c>GetThisTransform</c> also derive from it, but their getters read the agent through the machine,
        /// so they have no answer at all outside a running one and are not what this fixture is about. The
        /// field is the honest discriminator: a constant literal is exactly a node that stores its value.
        /// </para>
        /// </summary>
        private static Type[] ConstantLiterals() =>
            RuntimeTypes()
                .Where(t => typeof(Literal).IsAssignableFrom(t) && t.IsClass && !t.IsAbstract && t != typeof(Literal))
                .Where(t => t.GetField("value", BindingFlags.Instance | BindingFlags.NonPublic) != null)
                .OrderBy(t => t.Name)
                .ToArray();

        /// <summary>Every <see cref="ValueOutput"/> a defined node exposes, whatever it named the property.</summary>
        private static IEnumerable<ValueOutput> OutputsOf(BehaviorTreeNode node)
        {
            return node.GetType()
                .GetProperties(BindingFlags.Instance | BindingFlags.Public)
                .Where(p => typeof(ValueOutput).IsAssignableFrom(p.PropertyType))
                .Select(p => (ValueOutput)p.GetValue(node))
                .Where(port => port != null);
        }

        private static void SetPrivateField(object target, string field, object value)
        {
            var info = target.GetType().GetField(field, BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.IsNotNull(info, $"{target.GetType().Name} has no field '{field}'.");

            info.SetValue(target, value);
        }

        [Test]
        public void LiteralsExist()
        {
            // So the sweep below cannot pass vacuously on an empty set.
            Assert.IsNotEmpty(ConstantLiterals(), "Expected constant Literal nodes in the runtime assembly.");
        }

        /// <summary>
        /// The general rule, over every literal in the assembly: what the getter returns has to be an
        /// instance of the type the port declares. Nothing else is required of a literal, and nothing less
        /// makes it usable.
        /// </summary>
        [Test]
        public void EveryLiteralPublishesTheTypeItsPortDeclares()
        {
            foreach (var type in ConstantLiterals())
            {
                var node = (BehaviorTreeNode)Activator.CreateInstance(type);
                node.Define();

                foreach (var port in OutputsOf(node))
                {
                    if (!port.supportsFetch) continue;

                    var published = port.GetPortValue();

                    // A reference-typed literal with nothing entered yet is legitimately empty; a value type
                    // never can be, so null there is already a failure of the same rule.
                    if (published == null)
                    {
                        Assert.IsFalse(port.Type.IsValueType,
                            $"{type.Name}.{port.key} declares the value type {port.Type.Name} but published "
                            + "null.");
                        continue;
                    }

                    Assert.IsTrue(port.Type.IsInstanceOfType(published),
                        $"{type.Name}.{port.key} declares {port.Type.Name} but published "
                        + $"{published.GetType().Name}. Consumers read this port with a plain unbox cast, "
                        + "which does not convert -- so every one of them throws InvalidCastException.");
                }
            }
        }

        /// <summary>
        /// <c>IntegerLiteral</c> named specifically, with a value set, because the general rule above passes
        /// on a default-constructed node the moment the field type is right — and the whole failure was that
        /// an authored value came back as the wrong type.
        /// </summary>
        [Test]
        public void AnIntegerLiteralPublishesTheIntItWasGiven()
        {
            var node = new IntegerLiteral();
            SetPrivateField(node, "value", 7);
            node.Define();

            var published = node.Value.GetPortValue();

            Assert.IsInstanceOf<int>(published,
                "the port declares int, so a consumer's (int) cast has to succeed");
            Assert.AreEqual(7, (int)published);
        }

        [Test]
        public void AVector2LiteralPublishesTheVector2ItWasGiven()
        {
            var node = new Vector2Literal();
            SetPrivateField(node, "value", new Vector2(3.0f, 4.0f));
            node.Define();

            var published = node.Value.GetPortValue();

            Assert.IsInstanceOf<Vector2>(published,
                "the port declares Vector2; a boxed Vector3 does not convert to one, implicit operator or not");
            Assert.AreEqual(new Vector2(3.0f, 4.0f), (Vector2)published);
        }

        /// <summary>
        /// The siblings these two were copy-pasted from, asserted so a future "fix" that unifies the family
        /// cannot quietly break the ones that were always correct.
        /// </summary>
        [Test]
        public void TheFloatAndVector3LiteralsStillPublishTheirOwnTypes()
        {
            var floatLiteral = new FloatLiteral();
            SetPrivateField(floatLiteral, "value", 2.5f);
            floatLiteral.Define();

            Assert.IsInstanceOf<float>(floatLiteral.Value.GetPortValue());
            Assert.AreEqual(2.5f, (float)floatLiteral.Value.GetPortValue());

            var vector3Literal = new Vector3Literal();
            SetPrivateField(vector3Literal, "value", new Vector3(1.0f, 2.0f, 3.0f));
            vector3Literal.Define();

            Assert.IsInstanceOf<Vector3>(vector3Literal.Value.GetPortValue());
            Assert.AreEqual(new Vector3(1.0f, 2.0f, 3.0f), (Vector3)vector3Literal.Value.GetPortValue());
        }
    }
}
