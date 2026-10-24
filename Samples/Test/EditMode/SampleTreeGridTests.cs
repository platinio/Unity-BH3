using System.Collections.Generic;
using System.IO;
using System.Linq;
using ArcaneOnyx.BehaviorTree;
using ArcaneOnyx.BehaviorTree.Authoring;
using NUnit.Framework;
using UnityEditor;

namespace ArcaneOnyx.BH3.Samples.Tests
{
    public class SampleTreeGridTests
    {
        /// <summary>
        /// Found from this script rather than a fixed path: BH3 is installed somewhere else in every project.
        /// </summary>
        private static string SamplesFolder
        {
            get
            {
                var script = AssetDatabase.FindAssets("t:MonoScript " + nameof(SampleTreeGridTests))
                    .Select(AssetDatabase.GUIDToAssetPath)
                    .FirstOrDefault(path => Path.GetFileName(path) == nameof(SampleTreeGridTests) + ".cs");

                Assert.That(script, Is.Not.Null, "could not locate this test script in the asset database");

                var editModeTests = Path.GetDirectoryName(script);
                var samples = Path.GetDirectoryName(Path.GetDirectoryName(editModeTests));

                return samples.Replace(Path.DirectorySeparatorChar, '/');
            }
        }

        private static List<BehaviorTreeGraphAsset> SampleTrees() =>
            AssetDatabase.FindAssets("t:" + nameof(BehaviorTreeGraphAsset), new[] { SamplesFolder })
                .Select(AssetDatabase.GUIDToAssetPath)
                .Distinct()
                .Select(AssetDatabase.LoadAssetAtPath<BehaviorTreeGraphAsset>)
                .ToList();

        [Test]
        public void EveryNodeInTheSampleTreesSitsOnTheCanvasGrid()
        {
            var trees = SampleTrees();
            Assert.That(trees, Is.Not.Empty, "no sample trees found under " + SamplesFolder);

            var offTheGrid = trees
                .Where(tree => BehaviorTreeLayout.CountNodesOffTheGrid(tree.graph) > 0)
                .Select(tree => $"{AssetDatabase.GetAssetPath(tree)}: {BehaviorTreeLayout.CountNodesOffTheGrid(tree.graph)}")
                .ToList();

            Assert.That(offTheGrid, Is.Empty,
                "these sample trees have nodes off the grid; run bt_layout over them:\n  " + string.Join("\n  ", offTheGrid));
        }
    }
}
