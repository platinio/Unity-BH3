using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using ArcaneOnyx.GraphCore;
using NUnit.Framework;
using Unity.VisualScripting;
using UnityEngine;

namespace ArcaneOnyx.BehaviorTree.Tests
{
    /// <summary>
    /// Structural guards for the behavior tree module rather than behaviour tests. They fail when a new
    /// node is added in a way that compiles but breaks at edit/serialization time — the class of bug that
    /// otherwise only shows up as a node the right-click menu refuses to create, or one that silently
    /// disappears from a graph asset on reload.
    ///
    /// Why these rules matter here specifically:
    ///   * The editor's create menu instantiates a node from its [GraphCreateMenu] type via reflection,
    ///     so every menu entry must point at a concrete <see cref="BehaviorTreeNode"/> with a public
    ///     parameterless constructor.
    ///   * A <see cref="Decorator"/> wraps exactly one child; the whole decorator contract assumes that
    ///     single-child shape, which is enforced through MaxChildrenLimit.
    ///   * <see cref="BehaviorTreeMachine"/> reads its Variables component in Awake, so the
    ///     [RequireComponent] guarantee is what keeps that access safe (and matches the README).
    /// </summary>
    [TestFixture]
    public class BehaviorTreeArchitectureTests
    {
        // The shipping runtime assembly — resolved from a production type so the scan never picks up the
        // ScriptedNode / FixedCondition doubles that live in this test assembly.
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

        private static Type[] CreateMenuNodes() =>
            RuntimeTypes()
                .Where(t => t.IsClass && t.GetCustomAttribute<GraphCreateMenu>(false) != null)
                .OrderBy(t => t.Name)
                .ToArray();

        private static Type[] ConcreteDecorators() =>
            RuntimeTypes()
                .Where(t => typeof(Decorator).IsAssignableFrom(t)
                            && t.IsClass && !t.IsAbstract
                            && t != typeof(Decorator))
                .OrderBy(t => t.Name)
                .ToArray();

        [Test]
        public void CreateMenuNodes_Exist()
        {
            // Sanity check so the rules below can't silently pass on an empty set (e.g. an assembly
            // resolution slip that returns no types).
            Assert.IsNotEmpty(CreateMenuNodes(),
                "Expected at least one [GraphCreateMenu] node in the runtime assembly.");
        }

        [Test]
        public void CreateMenuNodes_AreConcreteBehaviorTreeNodes()
        {
            foreach (var type in CreateMenuNodes())
            {
                Assert.IsFalse(type.IsAbstract,
                    $"{type.Name} carries [GraphCreateMenu] but is abstract; the create menu cannot " +
                    $"instantiate it.");
                Assert.IsTrue(typeof(BehaviorTreeNode).IsAssignableFrom(type),
                    $"{type.Name} carries [GraphCreateMenu] but is not a {nameof(BehaviorTreeNode)}.");
            }
        }

        [Test]
        public void CreateMenuNodes_HavePublicParameterlessConstructor()
        {
            foreach (var type in CreateMenuNodes())
            {
                var ctor = type.GetConstructor(
                    BindingFlags.Instance | BindingFlags.Public, null, Type.EmptyTypes, null);

                Assert.IsNotNull(ctor,
                    $"{type.Name} needs a public parameterless constructor so the create menu and Visual " +
                    $"Scripting deserialization can instantiate it.");
            }
        }

        [Test]
        public void Decorators_WrapExactlyOneChild()
        {
            foreach (var type in ConcreteDecorators())
            {
                var decorator = (Decorator)Activator.CreateInstance(type);

                Assert.AreEqual(1, decorator.MaxChildrenLimit,
                    $"{type.Name} is a Decorator and must limit itself to a single child " +
                    $"(MaxChildrenLimit == 1); the decorator contract assumes a one-child shape.");
            }
        }

        [Test]
        public void BehaviorTreeMachine_RequiresVariablesComponent()
        {
            // The machine reads its Variables component in Awake and seeds the "This" key from it;
            // [RequireComponent(typeof(Variables))] is what guarantees the component is present.
            var requireComponents = typeof(BehaviorTreeMachine)
                .GetCustomAttributes(typeof(RequireComponent), true)
                .Cast<RequireComponent>();

            Assert.IsTrue(
                requireComponents.Any(rc =>
                    rc.m_Type0 == typeof(Variables) ||
                    rc.m_Type1 == typeof(Variables) ||
                    rc.m_Type2 == typeof(Variables)),
                $"{nameof(BehaviorTreeMachine)} must [RequireComponent(typeof(Variables))] — it depends on " +
                $"a Variables component being present on the same GameObject.");
        }
    }
}
