using ArcaneOnyx.GraphCore;
using UnityEngine;

namespace ArcaneOnyx.BehaviorTree.Instancing
{
    /// <summary>
    /// Spec 07 step 4: the machine that does not clone.
    ///
    /// <para>
    /// <see cref="BehaviorTreeMachine.Awake"/> calls <c>Object.Instantiate</c> on the tree asset — a
    /// serialize/deserialize round trip, per agent, compounding with nesting. This one asks for the plan
    /// (baked once per asset, ever) and allocates three arrays. It runs the same node classes, through the
    /// same context-taking methods.
    /// </para>
    ///
    /// <para>
    /// Demo fidelity: no Switch, no recorder, no variable scopes, no sub-trees, and guards are entry-only.
    /// Those are listed in spec 14, not hidden.
    /// </para>
    /// </summary>
    [AddComponentMenu("Arcane Onyx/Shared Behavior Tree Machine (Demo)")]
    public sealed class SharedBehaviorTreeMachine : MonoBehaviour
    {
        [SerializeField] private BehaviorTreeGraphAsset behaviorTree;

        public TreeInstance Instance { get; private set; }

        public BehaviorTreeGraphAsset BehaviorTree
        {
            get => behaviorTree;
            set => behaviorTree = value;
        }

        public ExecutionStatus LastRootStatus =>
            Instance == null ? ExecutionStatus.None : Instance.LastStatus[Instance.Plan.EntryIndex];

        private void Awake()
        {
            if (behaviorTree == null) return;

            Instance = new TreeInstance(TreePlan.For(behaviorTree), gameObject);
            SharedTreeRunner.AwakeAll(Instance);
        }

        private void Update()
        {
            if (Instance == null) return;

            SharedTreeRunner.TickNode(Instance, Instance.Plan.EntryIndex);
        }

        private void OnDestroy()
        {
            if (Instance == null) return;

            SharedTreeRunner.ExitNode(Instance, Instance.Plan.EntryIndex);
            Instance = null;
        }
    }
}
