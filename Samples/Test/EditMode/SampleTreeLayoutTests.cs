using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using ArcaneOnyx.BehaviorTree;
using ArcaneOnyx.BehaviorTree.Authoring;
using NUnit.Framework;
using UnityEditor;

namespace ArcaneOnyx.BH3.Samples.Tests
{
    public class SampleTreeLayoutTests
    {
        /// <summary>
        /// Found from this script rather than a fixed path: BH3 is installed somewhere else in every project.
        /// </summary>
        private static string SamplesFolder
        {
            get
            {
                var script = AssetDatabase.FindAssets("t:MonoScript " + nameof(SampleTreeLayoutTests))
                    .Select(AssetDatabase.GUIDToAssetPath)
                    .FirstOrDefault(path => Path.GetFileName(path) == nameof(SampleTreeLayoutTests) + ".cs");

                Assert.That(script, Is.Not.Null, "could not locate this test script in the asset database");

                var editModeTests = Path.GetDirectoryName(script);
                var samples = Path.GetDirectoryName(Path.GetDirectoryName(editModeTests));

                return samples.Replace(Path.DirectorySeparatorChar, '/');
            }
        }

        private static List<BehaviorTreeGraphAsset> SampleTrees()
        {
            var samples = SamplesFolder;

            // Test fixtures are left messy on purpose, the layout tests arrange copies of them.
            var trees = AssetDatabase.FindAssets("t:" + nameof(BehaviorTreeGraphAsset), new[] { samples })
                .Select(AssetDatabase.GUIDToAssetPath)
                .Where(path => !path.StartsWith(samples + "/Test/"))
                .Distinct()
                .Select(AssetDatabase.LoadAssetAtPath<BehaviorTreeGraphAsset>)
                .ToList();

            Assert.That(trees, Is.Not.Empty, "no sample trees found under " + samples);

            return trees;
        }

        private static List<string> TreesWhere(Func<BehaviorTreeGraph, int> countProblems) =>
            SampleTrees()
                .Where(tree => countProblems(tree.graph) > 0)
                .Select(tree => $"{AssetDatabase.GetAssetPath(tree)}: {countProblems(tree.graph)}")
                .ToList();

        [Test]
        public void EveryNodeInTheSampleTreesSitsOnTheCanvasGrid()
        {
            var offTheGrid = TreesWhere(BehaviorTreeLayout.CountNodesOffTheGrid);

            Assert.That(offTheGrid, Is.Empty,
                "these sample trees have nodes off the grid; run bt_layout over them:\n  "
                + string.Join("\n  ", offTheGrid));
        }

        [Test]
        public void NoTwoNodesInTheSampleTreesAreDrawnOverEachOther()
        {
            var overlapping = TreesWhere(BehaviorTreeLayout.CountOverlappingPairs);

            Assert.That(overlapping, Is.Empty,
                "these sample trees draw nodes over each other; run bt_layout over them:\n  "
                + string.Join("\n  ", overlapping));
        }
    }
}
