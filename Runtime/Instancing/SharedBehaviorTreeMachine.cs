using ArcaneOnyx.GraphCore;
using UnityEngine;

namespace ArcaneOnyx.BehaviorTree.Instancing
{
    /// <summary>
    /// The shared-runtime counterpart of <see cref="BehaviorTreeMachine"/>, at demo fidelity. Awake is the
    /// whole pitch: where the old machine calls <c>Object.Instantiate</c> on the tree asset — a serialize
    /// round trip per agent — this one asks for the shared plan (baked once per asset, ever) and allocates
    /// three arrays.
    ///
    /// <para>
    /// Demo fidelity means: no Switch, no pause/teardown hooks, no recorder, no variable scopes, no
    /// sub-trees. It ticks the entry every Update, and it exists so the same asset can be watched running
    /// on both runtimes in one scene.
    /// </para>
    /// </summary>
    [AddComponentMenu("Arcane Onyx/Shared Behavior Tree Machine (Demo)")]
    public sealed class SharedBehaviorTreeMachine : MonoBehaviour
    {
        [SerializeField] private BehaviorTreeGraphAsset behaviorTree;

        public TreeInstance Instance { get; private set; }

        public ExecutionStatus LastRootStatus =>
            Instance == null ? ExecutionStatus.None : Instance.LastStatus[Instance.Plan.EntryIndex];

        public BehaviorTreeGraphAsset BehaviorTree
        {
            get => behaviorTree;
            set => behaviorTree = value;
        }

        private void Awake()
        {
            if (behaviorTree == null) return;

            Instance = new TreeInstance(TreePlan.For(behaviorTree), gameObject);
        }

        private void Update()
        {
            if (Instance == null) return;

            SharedTreeRunner.TickNode(Instance, Instance.Plan.EntryIndex);
        }

        private void OnDestroy()
        {
            if (Instance == null) return;

            // The mirror of BehaviorTreeGraph.OnExit: whatever is still running gets its exit.
            SharedTreeRunner.ExitNode(Instance, Instance.Plan.EntryIndex);
            Instance = null;
        }
    }
}
