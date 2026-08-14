using System.Linq;
using ArcaneOnyx.VisualScriptingExtension;
using NUnit.Framework;
using Unity.VisualScripting;
using UnityEditor;
using UnityEngine;
using UnityEngine.TestTools.Constraints;
using Is = NUnit.Framework.Is;

namespace ArcaneOnyx.BehaviorTree.Tests
{
    /// <summary>
    /// The evaluation seam's own tests assert the allocation contract against <c>FunctionBinding</c>
    /// directly. That leaves the path agents actually take — a Script Graph Variable staging the branch's
    /// ambient variables — untested, which is exactly where the first version regressed: iterating
    /// <c>VariableDeclarations</c> with <c>foreach</c> boxed its enumerator on every evaluation, defeating
    /// the plan the contract rests on while every existing test stayed green.
    /// </summary>
    public class FunctionBackedScriptGraphVariableTests
    {
        private const string Folder = "Assets/__btfntests";

        private GameObject agent;

        [SetUp]
        public void SetUp()
        {
            if (!AssetDatabase.IsValidFolder(Folder)) AssetDatabase.CreateFolder("Assets", "__btfntests");
            FunctionEvaluator.InvalidateAll();

            agent = new GameObject("Agent");
            agent.AddComponent<Variables>();
        }

        [TearDown]
        public void TearDown()
        {
            if (agent != null) Object.DestroyImmediate(agent);
            if (AssetDatabase.IsValidFolder(Folder)) AssetDatabase.DeleteAsset(Folder);
            FunctionEvaluator.InvalidateAll();
        }

        /// <summary>A Function that returns its single declared input unchanged.</summary>
        private static FunctionGraphAsset EchoFunction(string assetName, string inputKey, System.Type type)
        {
            var function = ScriptableObject.CreateInstance<FunctionGraphAsset>();
            var graph = function.graph;

            var input = new ScriptGraphInput { position = new Vector2(-400.0f, 0.0f) };
            var output = new ScriptGraphOutput { position = new Vector2(400.0f, 0.0f) };
            graph.units.Add(input);
            graph.units.Add(output);

            graph.controlInputDefinitions.Add(new ControlInputDefinition
            {
                key = FunctionGraphAsset.EnterKey, label = FunctionGraphAsset.EnterKey
            });
            graph.controlOutputDefinitions.Add(new ControlOutputDefinition
            {
                key = FunctionGraphAsset.ExitKey, label = FunctionGraphAsset.ExitKey
            });
            graph.valueInputDefinitions.Add(new Unity.VisualScripting.ValueInputDefinition { key =inputKey, label = inputKey, type = type });
            graph.valueOutputDefinitions.Add(new Unity.VisualScripting.ValueOutputDefinition
            {
                key = FunctionGraphAsset.ResultKey, label = FunctionGraphAsset.ResultKey, type = type
            });
            graph.PortDefinitionsChanged();

            input.controlOutputs[FunctionGraphAsset.EnterKey]
                .ValidlyConnectTo(output.controlInputs[FunctionGraphAsset.ExitKey]);
            input.valueOutputs[inputKey].ValidlyConnectTo(output.valueInputs[FunctionGraphAsset.ResultKey]);

            AssetDatabase.CreateAsset(function, $"{Folder}/{assetName}.asset");
            return function;
        }

        private static VariableDeclarations Declarations()
        {
            // Deliberately wider than the Function's contract: a branch's ambient variables are not a call
            // site's argument list, so staging must cope with extras without paying for them.
            var declarations = new VariableDeclarations();
            declarations.Set("scale", 3.0f);
            declarations.Set("unrelatedA", "text");
            declarations.Set("unrelatedB", 7);
            declarations.Set("unrelatedC", Vector3.one);
            return declarations;
        }

        [Test]
        public void FunctionBackedVariable_ReturnsTheStagedArgument()
        {
            var variable = new ScriptGraphVariable();
            variable.SetFunction(EchoFunction("Echo", "scale", typeof(float)));

            var result = variable.GetValue<float>(agent, Declarations());

            Assert.That(result, Is.EqualTo(3.0f));
        }

        [Test]
        public void FunctionBackedVariable_IgnoresVariablesTheFunctionDoesNotDeclare()
        {
            var variable = new ScriptGraphVariable();
            variable.SetFunction(EchoFunction("Extras", "scale", typeof(float)));

            // The three unrelated declarations must not be an error, and must not reach the Function.
            Assert.That(variable.GetValue<float>(agent, Declarations()), Is.EqualTo(3.0f));
        }

        /// <summary>
        /// The regression guard. Staging must not enumerate <c>VariableDeclarations</c> and must not resolve a
        /// port by name — both allocate, and both are invisible to every other test in the suite.
        /// </summary>
        [Test]
        public void FunctionBackedVariable_DoesNotAllocate_WhenStagingArguments()
        {
            var variable = new ScriptGraphVariable();
            variable.SetFunction(EchoFunction("NoAlloc", "scale", typeof(float)));
            var declarations = Declarations();

            for (var i = 0; i < 200; i++) variable.GetValue<float>(agent, declarations);

            Assert.That(() => { variable.GetValue<float>(agent, declarations); },
                UnityEngine.TestTools.Constraints.Is.Not.AllocatingGCMemory(),
                "staging arguments allocated; the usual causes are a foreach over VariableDeclarations " +
                "(its GetEnumerator returns an interface, so the struct enumerator boxes) or a port " +
                "resolved by name in the call path");

            // Keeps the instrument honest — if this stops throwing, the assertion above proves nothing.
            Assert.That(() =>
                {
                    Assert.That(() => { var junk = new byte[256]; },
                        UnityEngine.TestTools.Constraints.Is.Not.AllocatingGCMemory());
                },
                Throws.Exception);
        }

        /// <summary>
        /// A binding is built once and reused, so an edit to the Function has to reach bindings that already
        /// exist. Before invalidation carried a version, clearing the plan cache left live bindings pointing
        /// at the ports they resolved against originally.
        /// </summary>
        [Test]
        public void FunctionBackedVariable_PicksUpAContractChange_AfterInvalidation()
        {
            var function = EchoFunction("Rebind", "scale", typeof(float));
            var variable = new ScriptGraphVariable();
            variable.SetFunction(function);

            var declarations = new VariableDeclarations();
            declarations.Set("scale", 2.0f);
            declarations.Set("boost", 9.0f);

            Assert.That(variable.GetValue<float>(agent, declarations), Is.EqualTo(2.0f));

            // Rewire the Function to return a different declared input.
            function.graph.valueInputDefinitions.Add(new Unity.VisualScripting.ValueInputDefinition
            {
                key = "boost", label = "boost", type = typeof(float)
            });
            function.graph.PortDefinitionsChanged();

            var input = function.graph.units.OfType<ScriptGraphInput>().First();
            var output = function.graph.units.OfType<ScriptGraphOutput>().First();
            input.valueOutputs["boost"].ValidlyConnectTo(output.valueInputs[FunctionGraphAsset.ResultKey]);

            FunctionEvaluator.InvalidateAll();

            Assert.That(variable.GetValue<float>(agent, declarations), Is.EqualTo(9.0f),
                "a live binding kept evaluating against the plan it resolved before the edit");
        }

        [Test]
        public void LegacyScriptGraphPath_IsUnaffected_WhenNoFunctionIsAssigned()
        {
            var variable = new ScriptGraphVariable();

            Assert.That(variable.ReadsFunction, Is.False);
            Assert.That(variable.Function, Is.Null);
        }
    }
}
