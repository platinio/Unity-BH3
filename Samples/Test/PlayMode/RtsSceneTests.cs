using System;
using System.Collections;
using ArcaneOnyx.BehaviorTree.Debugging;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace ArcaneOnyx.BH3.Samples.Tests.PlayMode
{
    public sealed class RtsSceneTests : SampleSceneFixture
    {
        private static readonly Guid Idle = NodeIn("RTS", "Worker", "Idle");
        private static readonly Guid Gather = NodeIn("RTS", "Worker", "Gather");
        private static readonly Guid Build = NodeIn("RTS", "Worker", "Build");

        private Carrier worker;
        private Treasury treasury;

        protected override string SampleName => "RTS";

        [UnitySetUp]
        public IEnumerator FindTheCast()
        {
            worker = TheOnly<Carrier>();
            treasury = TheOnly<Treasury>();
            yield break;
        }

        [UnityTest]
        public IEnumerator AWorkerOrderedToGatherBringsWoodHomeAndSetsOffAgain()
        {
            BehaviorTreeFlightRecorder recorder = RecorderOf(worker);
            yield return Frames(5);
            Assert.IsTrue(Entered(recorder, Idle), "An unordered worker idles.");

            ResourceNode tree = ResourceNode.Nearest(worker.transform.position, Resource.Wood);
            int woodBefore = treasury.Wood;
            int mark = Mark(recorder);
            Publish(worker, RtsFacts.Resource, tree.gameObject);
            Publish(worker, RtsFacts.GatherOrder, 1);
            yield return Frames(3);
            Assert.IsTrue(EnteredSince(recorder, mark, Gather), "A numbered gather order starts the Gather branch,");
            Assert.IsTrue(TakenOverSince(recorder, mark, Idle), "taking the slot from Idle.");

            yield return Until(() => treasury.Wood > woodBefore, 90f, "Wood arriving at the treasury");
            yield return Until(() => EntriesAtLeast(recorder, Gather) >= 2, 10f, "The worker setting off for a second trip on its own");
        }

        [UnityTest]
        public IEnumerator AWorkerOrderedToBuildRaisesTheSite()
        {
            BehaviorTreeFlightRecorder recorder = RecorderOf(worker);
            BuildingKind kind = TheOnly<BuildingPlacer>().Kinds[0];
            Vector3 spot = OnTheNavMesh(worker.transform.position + Vector3.right * 4f);
            Building site = UnityEngine.Object.Instantiate(kind.Prefab, spot, Quaternion.identity);
            Assert.IsFalse(site.IsBuilt, "A fresh site is unbuilt.");

            int mark = Mark(recorder);
            Publish(worker, RtsFacts.Site, site.gameObject);
            Publish(worker, RtsFacts.BuildOrder, 1);
            yield return Frames(3);
            Assert.IsTrue(EnteredSince(recorder, mark, Build), "A numbered build order starts the Build branch.");

            yield return Until(() => site.IsBuilt, kind.BuildSeconds + 30f, "The site being finished");
        }
    }
}
