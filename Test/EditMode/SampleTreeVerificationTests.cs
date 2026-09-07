using System.Collections.Generic;
using System.Linq;
using ArcaneOnyx.BehaviorTree.Authoring;
using NUnit.Framework;
using UnityEditor;

namespace ArcaneOnyx.BehaviorTree.Tests
{
    /// <summary>
    /// Runs <c>bt_verify</c> over the trees BH3 ships in <c>Sample/</c>, and holds their known problems to
    /// a recorded count.
    ///
    /// <para>
    /// <b>Why this exists.</b> Every other verification test builds its own tree, asserts on it, and deletes
    /// it — so the suite only ever checked content written by the test that checked it. Nothing looked at the
    /// content BH3 actually ships. Twenty-six Functions in the FPS sample could not be entered at all, for
    /// long enough that the seam which would have thrown on them had been deleted in the meantime, and the
    /// suite stayed green throughout. A lint nothing runs over real content is a lint that reports to nobody.
    /// </para>
    ///
    /// <para>
    /// <b>Why it records problems instead of forbidding them.</b> The sample is knowingly broken and the tool
    /// owner chose to leave it: re-authoring twenty-three-unit graphs nobody has run in years is game-content
    /// work, not part of retiring embedded graphs. So asserting "the sample verifies clean" would fail on the
    /// day it was written and be disabled by the week after. Recording the debt instead makes the two things
    /// that matter fail loudly — <b>new</b> breakage, and breakage that was <b>fixed</b> without anyone
    /// lowering the number.
    /// </para>
    /// </summary>
    [TestFixture]
    public class SampleTreeVerificationTests
    {
        /// <summary>
        /// What the shipped samples are known to be wrong about, by kind.
        ///
        /// <para>
        /// <b>These numbers are debt, not a target.</b> Every one of them is something a designer opening the
        /// sample would hit. They are written down so that fixing them is a visible, deliberate edit to this
        /// table rather than something nobody notices either way.
        /// </para>
        /// </summary>
        private static readonly Dictionary<string, int> KnownSampleProblems = new Dictionary<string, int>
        {
            // The FPS sample's graphs have no ScriptGraphInput unit, so FunctionBindingPlan refuses them.
            // Pre-existing and older than this suite: the pre-migration Soldier.asset has none either, and
            // the legacy seam that ran these needed the same unit, so it would have thrown on them too.
            { "unusable Function", 15 },

            // Wires the canvas draws red, mostly hanging off the missing node types below.
            { "invalid connection", 6 },

            // Nodes whose type was deleted at some point, rewritten to MissingType on load. Present in
            // Soldier and Zombie before RunScriptGraph was removed, so not from that.
            { "missing node type", 2 },

            // Nothing reaches or reads these.
            { "orphan node", 2 },

            // Ports that will throw the moment they are read. Was 2 until Set Nav Agent Position's NavPosition
            // gained a default, which fed both.
            { "unset port", 0 },

            // A reactive guard re-checking every tick although its condition declares what it watches.
            { "guard with no triggers", 1 },
        };

        /// <summary>
        /// The shipped sample trees. Located by path rather than by a hard-coded list so that adding a
        /// sample brings it under verification automatically — which is the whole point, since the failure
        /// this test exists for is content nobody thought to check.
        /// </summary>
        private static List<string> SampleTrees()
        {
            return AssetDatabase.FindAssets($"t:{nameof(BehaviorTreeGraphAsset)}")
                .Select(AssetDatabase.GUIDToAssetPath)
                .Where(path => path.Contains("/BH3/Sample/"))
                .Distinct()
                .OrderBy(path => path)
                .ToList();
        }

        /// <summary>
        /// Which known problem a finding is, or null when it is something new. Keyed on the part of each
        /// message that names the failure rather than on the whole string, so rewording a lint does not
        /// read as a regression in the sample.
        /// </summary>
        private static string KindOf(string finding)
        {
            if (finding.Contains("has no ScriptGraphInput unit")) return "unusable Function";
            if (finding.Contains("invalid connection")) return "invalid connection";
            if (finding.Contains("type that no longer exists")) return "missing node type";
            if (finding.Contains("nothing reaches or reads this node")) return "orphan node";
            if (finding.Contains("is unset and will throw")) return "unset port";
            if (finding.Contains("has no triggers")) return "guard with no triggers";

            return null;
        }

        [Test]
        public void ShippedSampleTrees_HaveOnlyTheirKnownProblems()
        {
            var trees = SampleTrees();

            // Never a silent pass. If the samples are not in this project the test has checked nothing, and
            // saying so is the difference between "verified" and "found nothing to verify".
            if (trees.Count == 0)
            {
                Assert.Ignore("No trees found under BH3/Sample — nothing was verified.");
            }

            var findings = BehaviorTreeVerification.Verify(trees.ToArray());

            var unknown = findings.Where(finding => KindOf(finding) == null).ToList();

            Assert.That(unknown, Is.Empty,
                $"The shipped samples report something not in the known-problem table. Either it is new "
                + $"breakage, or a lint's wording changed and KindOf needs to learn it:\n  "
                + string.Join("\n  ", unknown));

            var counted = findings
                .GroupBy(KindOf)
                .ToDictionary(group => group.Key, group => group.Count());

            foreach (var known in KnownSampleProblems)
            {
                counted.TryGetValue(known.Key, out var actual);

                Assert.That(actual, Is.LessThanOrEqualTo(known.Value),
                    $"The shipped samples got worse: '{known.Key}' went from {known.Value} to {actual}. "
                    + "Something in this change broke content BH3 ships.\n  "
                    + string.Join("\n  ", findings.Where(f => KindOf(f) == known.Key)));

                Assert.That(actual, Is.EqualTo(known.Value),
                    $"'{known.Key}' is down from {known.Value} to {actual} in the shipped samples, which is "
                    + "good — lower the number in KnownSampleProblems so the next regression is still "
                    + "caught. A table nobody lowers stops meaning anything.");
            }
        }

        /// <summary>
        /// The sample must at least still load. Separate from the count above because a tree that fails to
        /// deserialize reports one finding and no nodes, which reads as an improvement to anything counting
        /// problems.
        /// </summary>
        [Test]
        public void ShippedSampleTrees_AllLoad()
        {
            var trees = SampleTrees();

            if (trees.Count == 0) Assert.Ignore("No trees found under BH3/Sample — nothing was verified.");

            foreach (var path in trees)
            {
                var asset = AssetDatabase.LoadAssetAtPath<BehaviorTreeGraphAsset>(path);

                Assert.That(asset, Is.Not.Null, $"'{path}' did not load as a behavior tree.");
                Assert.That(asset.graph, Is.Not.Null, $"'{path}' loaded with no graph.");
                Assert.That(asset.graph.Nodes.Count(), Is.GreaterThan(0), $"'{path}' loaded with no nodes.");
            }
        }
    }
}
